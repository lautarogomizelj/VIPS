using System;
using System.Collections.Generic;         // Para List<T>
using System.Data;                        // Para DataTable, DataRow
using System.Globalization;               // Para CultureInfo
using System.IO;                          // Para MemoryStream (si usás PDFs)
using System.Linq;                         // Para .Contains y otras operaciones LINQ
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.ObjectPool;
using PdfSharpCore.Pdf;       // Para PdfDocument
using PdfSharpCore.Drawing;   // Para XGraphics, XFont, XRect, XBrushes, XStringFormats
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using VIPS.Web.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Text.Json;


namespace VIPS.Web.Services
{
    public class RouteService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly OrderService _orderService;
        private readonly FleetService _fleetService;
        private readonly EmailService _emailService;


        // URL de VROOM en la VM o host remoto
        private const string VroomUrl = "http://172.16.244.155:3000/solve";

        public RouteService(HttpClient httpClient, IConfiguration configuration, FleetService fleetService, OrderService orderService, EmailService emailService)
        {
            _configuration = configuration;
            _httpClient = httpClient;
            _orderService = orderService;
            _fleetService = fleetService;
            _emailService = emailService;
        }

        public List<RouteViewModel> ObtenerRutas(string columna, string orden)
        {
            try
            {
                var columnasPermitidas = new[] { "idRuta", "patente", "r.fechaCreacion", "cantidadPedidos", "estado" };
                if (!columnasPermitidas.Contains(columna))
                    throw new ArgumentException("Columna no válida");



                // Orden seguro
                var ordenSeguro = (orden?.ToUpper() == "DESC") ? "DESC" : "ASC";

                var query = $@"SELECT r.idRuta, u.usuario as conductor, f.patente,r.fechaCreacion, r.fechaInicio ,r.fechaFin,(SELECT COUNT(*) FROM RutaPedidos RP WHERE RP.idRuta = r.idRuta) AS cantidadPedidos, er.descripcion AS estado, f.idCamion FROM Ruta r INNER JOIN Flota f ON r.idCamion = f.idCamion INNER JOIN EstadoRuta er ON er.idEstadoRuta = r.idEstadoRuta LEFT JOIN Usuario u on u.idUsuario = r.idUsuario order by {columna} {ordenSeguro}";


                using var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
                connection.Open();

                using var cmd = new SqlCommand(query, connection);

                using var adapter = new SqlDataAdapter(cmd);

                var dataTable = new DataTable();
                adapter.Fill(dataTable);




                var lista = new List<RouteViewModel>();

                foreach (DataRow row in dataTable.Rows)
                {


                    lista.Add(new RouteViewModel
                    {
                        IdRuta = Convert.ToInt32(row["idRuta"]),
                        Patente = row["patente"].ToString(),
                        FechaCreacion = Convert.ToDateTime(row["fechaCreacion"]),
                        CantPedidos = Convert.ToInt32(row["cantidadPedidos"]),
                        Estado = row["estado"].ToString(),
                        IdCamion = Convert.ToInt32(row["idCamion"]),
                        Conductor = row["conductor"] != DBNull.Value ? row["conductor"].ToString() : (String?)null,
                        FechaInicio = row["fechaInicio"] != DBNull.Value ? Convert.ToDateTime(row["fechaInicio"]) : (DateTime?)null,
                        FechaFin = row["fechaFin"] != DBNull.Value ? Convert.ToDateTime(row["fechaFin"]) : (DateTime?)null



                    });
                }


                return lista;
            }
            catch (Exception ex)
            {
                return new List<RouteViewModel>();
            }
        }


        // Método principal
        public async Task<ResultadoOperacionRuta> GenerateRoutesAsync()
        {
            try
            {
                // 1) Armar JSON de solicitud
                // Traer vehículos disponibles
                var vehiculos = await _fleetService.GetVehiculosDisponiblesAsync();
                var vehiclesJson = vehiculos.Select(v => new
                {
                    id = v.IdCamion,
                    profile = "car",
                    start = new[] { v.Longitud, v.Latitud },
                    capacity = new[] { (int)v.CapacidadPeso, (int)v.CapacidadVolumen }, // ENTEROS
                    dimensions = new[] { v.Largo, v.Ancho, v.Alto }
                }).ToList();

                // Traer pedidos pendientes o reprogramados
                var pedidos = await _orderService.GetPedidosPendientesAsync();
                // Mapear a JSON para VROOM, incluyendo peso y volumen
                var jobsJson = pedidos.Select(p => new
                {
                    id = p.IdPedido,
                    location = new[] { p.Longitud, p.Latitud },
                    amount = new[] {
                        (int)p.Peso,                             // peso en kg
                        (int)p.Largo * (int)p.Ancho * (int)p.Alto           // volumen en m³
                    }
                }).ToList();

                // Armar JSON final
                var request = new
                {
                    vehicles = vehiclesJson,
                    jobs = jobsJson,
                    parameters = new
                    {
                        time_limit = 3600,
                        vehicle_speed = 60,
                        service_time = 300
                    }
                };

                // 2) Llamar a la API VROOM
                var response = await _httpClient.PostAsJsonAsync(VroomUrl, request);

                Console.WriteLine($"Status Code: {response.StatusCode}");

                // Leer el contenido como string para imprimirlo
                var resultadoJsonString1 = await response.Content.ReadAsStringAsync();

                // Imprimir el JSON completo en la consola
                Console.WriteLine("JSON devuelto por VROOM:");
                Console.WriteLine(resultadoJsonString1);

                if (!response.IsSuccessStatusCode)
                {
                    return new ResultadoOperacionRuta
                    {
                        Exito = false,
                        Mensaje = "Error al llamar a VROOM",
                        CodigoError = (int)response.StatusCode
                    };
                }

                var resultadoJsonString = await response.Content.ReadAsStringAsync();

                // 3) Leer la respuesta JSON
                var resultadoJson = await response.Content.ReadFromJsonAsync<VroomResult>();

                if (resultadoJson == null)
                {
                    return new ResultadoOperacionRuta
                    {
                        Exito = false,
                        Mensaje = "No se pudo deserializar la respuesta de VROOM"
                    };
                }

                // Verificar si VROOM devolvió un error (code != 0)
                if (resultadoJson.Code != 0)
                {
                    return new ResultadoOperacionRuta
                    {
                        Exito = false,
                        Mensaje = "VROOM devolvió error: " + resultadoJson.Code,
                        CodigoError = resultadoJson.Code,
                        CantidadPedidosPendientes = resultadoJson.Unassigned?.Count ?? 0
                    };
                }




                // 4) Guardar rutas y pedidos en SQL
                await GuardarRutasYPedidosAsync(resultadoJson);

                var pedidosAsignados = resultadoJson.Routes.SelectMany(r => r.Steps).Count(s => s.Type == "job");


                // 5) Retornar objeto resultadoOperacionRuta
                return new ResultadoOperacionRuta
                {
                    Exito = true,
                    Mensaje = "Rutas generadas correctamente",
                    CantidadRutasGeneradas = resultadoJson.Summary.Routes,
                    CantidadPedidos = pedidosAsignados,
                    CantidadVehiculos = resultadoJson.Routes.Select(r => r.Vehicle).Distinct().Count()
                };
            }
            catch (Exception ex)
            {
                return new ResultadoOperacionRuta
                {
                    Exito = false,
                    Mensaje = ex.Message,
                    CodigoError = -1
                };
            }
        }

        public async Task GuardarRutasYPedidosAsync(VroomResult resultadoJson)
        {
            if (resultadoJson == null) throw new ArgumentNullException(nameof(resultadoJson));
            if (resultadoJson.Routes == null) throw new Exception("No hay rutas en el resultado");



            using var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
            await connection.OpenAsync();

            using var transaction = connection.BeginTransaction();

            try
            {
                var idEstadoRuta = RetornarIdEstadoRuta();

                // 2️ Guardar cada ruta
                foreach (var route in resultadoJson.Routes)
                {
                    // Insert en Ruta
                    var insertRuta = @"INSERT INTO Ruta (idCamion, idEstadoRuta) VALUES (@IdVehiculo, @Estado); SELECT CAST(SCOPE_IDENTITY() AS INT);";

                    int idRuta;
                    using (var cmd = new SqlCommand(insertRuta, connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@IdVehiculo", route.Vehicle);
                        cmd.Parameters.AddWithValue("@Estado", idEstadoRuta);
                        // convertir metros a km y segundos a horas

                        idRuta = (int)await cmd.ExecuteScalarAsync();
                    }

                    var queryUsuario = @"UPDATE Flota SET asignado = 1 WHERE idCamion = @idFlota";

                    using var cmdUsuario = new SqlCommand(queryUsuario, connection, transaction);
                    cmdUsuario.Parameters.AddWithValue("@idFlota", route.Vehicle);

                    await cmdUsuario.ExecuteNonQueryAsync();


                    // Insert de pedidos asignados a esta ruta
                    int orden = 1;
                    foreach (var step in route.Steps)
                    {
                        if (step.Type == "job")
                        {
                            var insertRutaPedido = @"INSERT INTO RutaPedidos (idRuta, idPedido, ordenEntrega) VALUES (@IdRuta, @IdPedido, @OrdenEntrega)";

                            using var cmd = new SqlCommand(insertRutaPedido, connection, transaction);
                            cmd.Parameters.AddWithValue("@IdRuta", idRuta);
                            cmd.Parameters.AddWithValue("@IdPedido", step.Id);
                            cmd.Parameters.AddWithValue("@OrdenEntrega", orden);
                            await cmd.ExecuteNonQueryAsync();

                            //  Marcar pedido como "Programado"
                            var updatePedido = @"UPDATE Pedido SET idEstadoPedido = (SELECT idEstado FROM EstadoPedido WHERE descripcion = 'Programado') WHERE idPedido = @IdPedido";

                            using var cmdUpdate = new SqlCommand(updatePedido, connection, transaction);
                            cmdUpdate.Parameters.AddWithValue("@IdPedido", step.Id);
                            await cmdUpdate.ExecuteNonQueryAsync();

                            orden++;
                        }
                    }
                }

                // 3️ Marcar pedidos no asignados como "Reprogramado"
                foreach (var unassigned in resultadoJson.Unassigned)
                {
                    var updatePedido = @"UPDATE Pedido SET idEstadoPedido = (SELECT idEstado FROM EstadoPedido WHERE descripcion = 'Reprogramado') WHERE idPedido = @IdPedido";

                    using var cmd = new SqlCommand(updatePedido, connection, transaction);
                    cmd.Parameters.AddWithValue("@IdPedido", unassigned.Id);
                    await cmd.ExecuteNonQueryAsync();
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public Ruta? retornarRouteModelConIdRuta(string idRuta)
        {
            try
            {
                var query = $@"select idRuta, idCamion, r.idEstadoRuta, fechaCreacion, er.descripcion as estado from Ruta r inner join EstadoRuta er on er.idEstadoRuta = r.idEstadoRuta where idRuta = @idRuta and idRuta not in (select idRuta from Ruta r inner join EstadoRuta er on er.idEstadoRuta = r.idEstadoRuta where descripcion in ('Cancelada', 'Finalizada'))";

                using var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
                connection.Open();

                using var cmd = new SqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@idRuta", idRuta);
                using var reader = cmd.ExecuteReader();

                if (reader.Read())
                {
                    return new Ruta
                    {
                        IdRuta = Convert.ToInt32(reader["idRuta"]),
                        IdCamion = Convert.ToInt32(reader["idCamion"]),
                        idEstadoRuta = Convert.ToInt32(reader["idEstadoRuta"]),
                        FechaCreacion = reader.GetDateTime(reader.GetOrdinal("fechaCreacion")),
                        Estado = reader.GetString(reader.GetOrdinal("estado"))
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        //iniciar rutas

        public async Task<ResultadoOperacion> IniciarRutaAsync(string idRuta)
        {
            using var spConnection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
            await spConnection.OpenAsync();

            using var spCmd = new SqlCommand("sp_iniciarRuta", spConnection)
            {
                CommandType = CommandType.StoredProcedure
            };

            spCmd.Parameters.AddWithValue("@idRuta", idRuta);

            var outputParam = new SqlParameter("@errorMessage", SqlDbType.VarChar, 200)
            {
                Direction = ParameterDirection.Output
            };
            spCmd.Parameters.Add(outputParam);

            try
            {
                await spCmd.ExecuteNonQueryAsync();

                var errorMessage = outputParam.Value as string;

                if (!string.IsNullOrEmpty(errorMessage))
                {
                    return new ResultadoOperacion
                    {
                        Exito = false,
                        Mensaje = errorMessage
                    };
                }

                var listaClientes = new List<(int IdCliente, string NombreCompleto, string Email)>();

                using (var clientConnection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString")))
                {
                    await clientConnection.OpenAsync();

                    var query = @"
SELECT c.idCliente, c.nombre + ' ' + c.apellido AS nombreCompleto, c.email
FROM RutaPedidos rp
INNER JOIN Pedido p ON p.idPedido = rp.idPedido
INNER JOIN Cliente c ON c.idCliente = p.idCliente
WHERE rp.idRuta = @idRuta";

                    using (var clientCmd = new SqlCommand(query, clientConnection))
                    {
                        clientCmd.Parameters.AddWithValue("@idRuta", idRuta);

                        using (var reader = await clientCmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                listaClientes.Add((
                                    IdCliente: (int)reader["idCliente"],
                                    NombreCompleto: reader["nombreCompleto"].ToString(),
                                    Email: reader["email"].ToString()
                                ));
                            }
                        }
                    }
                }

                int totalClientes = listaClientes.Count;
                int enviadosCorrectos = 0;

                foreach (var cliente in listaClientes)
                {
                    string cuerpo = $@"
<p>Hola {cliente.NombreCompleto},</p>
<p>Tu pedido está en camino.</p>";

                    bool enviado = await _emailService.EnviarCorreo(cliente.Email, "Alerta - Asignación de Ruta", cuerpo);

                    if (enviado)
                        enviadosCorrectos++;
                }

                if (enviadosCorrectos != totalClientes)
                {
                    return new ResultadoOperacion
                    {
                        Exito = true,
                        Mensaje = "Ruta iniciada correctamente, pero hubo problemas en el envío de notificaciones a algunos clientes."
                    };
                }

                return new ResultadoOperacion
                {
                    Exito = true,
                    Mensaje = "Ruta iniciada correctamente y notificaciones enviadas a todos los clientes."
                };
            }
            catch (Exception ex)
            {
                return new ResultadoOperacion
                {
                    Exito = false,
                    Mensaje = ex.Message
                };
            }
        }

        public ResultadoOperacion FinalizarRuta(string idRuta)
        {
            try
            {
                using var conn = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
                conn.Open();

                using var cmd = new SqlCommand("sp_finalizarRuta", conn);
                cmd.CommandType = CommandType.StoredProcedure;

                // Parámetros
                cmd.Parameters.AddWithValue("@idRuta", Convert.ToInt32(idRuta));

                var errorParam = new SqlParameter("@errorMessage", SqlDbType.VarChar, 200)
                {
                    Direction = ParameterDirection.Output
                };
                cmd.Parameters.Add(errorParam);

                // Ejecutar el SP
                cmd.ExecuteNonQuery();

                // Leer mensaje de salida
                string mensaje = errorParam.Value != DBNull.Value ? errorParam.Value.ToString() : null;






                return new ResultadoOperacion
                {
                    Exito = string.IsNullOrEmpty(mensaje), // si mensaje tiene texto, hubo error
                    Mensaje = string.IsNullOrEmpty(mensaje) ? "Ruta finalizada correctamente" : mensaje
                };
            }
            catch (SqlException ex)
            {
                return new ResultadoOperacion
                {
                    Exito = false,
                    Mensaje = $"Error en base de datos: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                return new ResultadoOperacion
                {
                    Exito = false,
                    Mensaje = $"Error inesperado: {ex.Message}"
                };
            }
        }



        public async Task<ResultadoOperacion> EliminarRoute(string idRuta)
        {
            try
            {
                using var conn = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
                conn.Open();

                using var cmd = new SqlCommand("sp_cancelarRuta", conn);
                cmd.CommandType = CommandType.StoredProcedure;

                // Parámetros
                cmd.Parameters.AddWithValue("@idRuta", Convert.ToInt32(idRuta));

                var errorParam = new SqlParameter("@errorMessage", SqlDbType.VarChar, 200)
                {
                    Direction = ParameterDirection.Output
                };
                cmd.Parameters.Add(errorParam);

                // Ejecutar el SP
                cmd.ExecuteNonQuery();

                // Leer mensaje de salida
                string mensaje = errorParam.Value != DBNull.Value ? errorParam.Value.ToString() : "Operación finalizada";

                return new ResultadoOperacion
                {
                    Exito = string.IsNullOrEmpty(mensaje), // si mensaje tiene texto, hubo error
                    Mensaje = string.IsNullOrEmpty(mensaje) ? "Ruta cancelada correctamente" : mensaje
                };
            }
            catch (SqlException ex)
            {
                return new ResultadoOperacion
                {
                    Exito = false,
                    Mensaje = $"Error en base de datos: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                return new ResultadoOperacion
                {
                    Exito = false,
                    Mensaje = $"Error inesperado: {ex.Message}"
                };
            }
        }


        public async Task<ResultadoOperacion> MarcarEntregaFallida(string idPedido, string motivo)
        {
            try
            {
                using var conn = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
                conn.Open();

                using var cmd = new SqlCommand("sp_entregaFallida", conn);
                cmd.CommandType = CommandType.StoredProcedure;

                // Parámetros
                cmd.Parameters.AddWithValue("@idPedido", Convert.ToInt32(idPedido));
                cmd.Parameters.AddWithValue("@motivoFallida", motivo); // motivo seleccionado por el conductor

                var errorParam = new SqlParameter("@errorMessage", SqlDbType.VarChar, 200)
                {
                    Direction = ParameterDirection.Output
                };
                cmd.Parameters.Add(errorParam);

                // Ejecutar el SP
                cmd.ExecuteNonQuery();

                // Leer mensaje de salida
                // Leer mensaje de salida
                var errorMessage = errorParam.Value as string;

                if (!string.IsNullOrEmpty(errorMessage))
                {
                    return new ResultadoOperacion
                    {
                        Exito = false,
                        Mensaje = errorMessage
                    };
                }

                var listaClientes = new List<(int IdCliente, string NombreCompleto, string Email)>();

                using (var clientConnection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString")))
                {
                    await clientConnection.OpenAsync();

                    var query = @"
SELECT distinct c.idCliente, c.nombre + ' ' + c.apellido AS nombreCompleto, c.email
FROM RutaPedidos rp
INNER JOIN Pedido p ON p.idPedido = rp.idPedido
INNER JOIN Cliente c ON c.idCliente = p.idCliente
WHERE rp.idPedido = @idPedido";

                    using (var clientCmd = new SqlCommand(query, clientConnection))
                    {
                        clientCmd.Parameters.AddWithValue("@idPedido", idPedido);

                        using (var reader = await clientCmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                listaClientes.Add((
                                    IdCliente: (int)reader["idCliente"],
                                    NombreCompleto: reader["nombreCompleto"].ToString(),
                                    Email: reader["email"].ToString()
                                ));
                            }
                        }
                    }
                }

                int totalClientes = listaClientes.Count;
                int enviadosCorrectos = 0;

                foreach (var cliente in listaClientes)
                {
                    string cuerpo = $@"
<p>Hola {cliente.NombreCompleto},</p>
<p>El pedido no ha podido entregar!</p>";

                    bool enviado = await _emailService.EnviarCorreo(cliente.Email, "Alerta - Pedido fallido", cuerpo);

                    if (enviado)
                        enviadosCorrectos++;
                }

                if (enviadosCorrectos != totalClientes)
                {
                    return new ResultadoOperacion
                    {
                        Exito = true,
                        Mensaje = "Pedido fallido correctamente, pero hubo problemas en el envío de notificacion al cliente."
                    };
                }

                return new ResultadoOperacion
                {
                    Exito = true,
                    Mensaje = "Pedido fallido correctamente y notificacion enviada al cliente."
                };
            }
            catch (SqlException ex)
            {
                return new ResultadoOperacion
                {
                    Exito = false,
                    Mensaje = $"Error en base de datos: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                return new ResultadoOperacion
                {
                    Exito = false,
                    Mensaje = $"Error inesperado: {ex.Message}"
                };
            }
        }

        public async Task<ResultadoOperacion> MarcarEntregaEntregada(string idPedido, string path)
        {
            try
            {
                using var conn = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
                await conn.OpenAsync();

                using var cmd = new SqlCommand("sp_entregarPedido", conn);
                cmd.CommandType = CommandType.StoredProcedure;

                // Parámetros
                cmd.Parameters.AddWithValue("@idPedido", Convert.ToInt32(idPedido));

                // Nuevo parámetro para ruta del comprobante
                cmd.Parameters.AddWithValue("@comprobantePath", path);


                var errorParam = new SqlParameter("@errorMessage", SqlDbType.VarChar, 200)
                {
                    Direction = ParameterDirection.Output
                };
                cmd.Parameters.Add(errorParam);

                // Ejecutar el SP
                await cmd.ExecuteNonQueryAsync();

                // Leer mensaje de salida
                var errorMessage = errorParam.Value as string;

                if (!string.IsNullOrEmpty(errorMessage))
                {
                    return new ResultadoOperacion
                    {
                        Exito = false,
                        Mensaje = errorMessage
                    };
                }

                var listaClientes = new List<(int IdCliente, string NombreCompleto, string Email)>();

                using (var clientConnection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString")))
                {
                    await clientConnection.OpenAsync();

                    var query = @"
SELECT c.idCliente, c.nombre + ' ' + c.apellido AS nombreCompleto, c.email
FROM RutaPedidos rp
INNER JOIN Pedido p ON p.idPedido = rp.idPedido
INNER JOIN Cliente c ON c.idCliente = p.idCliente
WHERE rp.idPedido = @idPedido";

                    using (var clientCmd = new SqlCommand(query, clientConnection))
                    {
                        clientCmd.Parameters.AddWithValue("@idPedido", idPedido);

                        using (var reader = await clientCmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                listaClientes.Add((
                                    IdCliente: (int)reader["idCliente"],
                                    NombreCompleto: reader["nombreCompleto"].ToString(),
                                    Email: reader["email"].ToString()
                                ));
                            }
                        }
                    }
                }

                int totalClientes = listaClientes.Count;
                int enviadosCorrectos = 0;

                foreach (var cliente in listaClientes)
                {
                    string cuerpo = $@"
<p>Hola {cliente.NombreCompleto},</p>
<p>El pedido se ha entregado con exito!</p>";

                    bool enviado = await _emailService.EnviarCorreo(cliente.Email, "Alerta - Pedido entregado", cuerpo);


                    if (enviado)
                        enviadosCorrectos++;
                }

                if (enviadosCorrectos != totalClientes)
                {
                    return new ResultadoOperacion
                    {
                        Exito = true,
                        Mensaje = "Pedido entregado correctamente, pero hubo problemas en el envío de notificacion al cliente."
                    };
                }

                return new ResultadoOperacion
                {
                    Exito = true,
                    Mensaje = "Pedido entregado correctamente y notificacion enviada al cliente."
                };

            }
            catch (SqlException ex)
            {
                return new ResultadoOperacion
                {
                    Exito = false,
                    Mensaje = $"Error en base de datos: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                return new ResultadoOperacion
                {
                    Exito = false,
                    Mensaje = $"Error inesperado: {ex.Message}"
                };
            }
        }






        public PuntoPartida obtenerPuntoPartidaActual()
        {
            try
            {
                using var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
                connection.Open();

                var query = @"select direccion, latitud, longitud
                      from DomicilioEntrega
                      where idDomicilioEntrega = (select top 1 idDomicilioInicio from Flota where eliminado = 0)";

                using var cmd = new SqlCommand(query, connection);
                using var reader = cmd.ExecuteReader();

                if (reader.Read())
                {
                    return new PuntoPartida
                    {
                        Direccion = reader.GetString(reader.GetOrdinal("direccion")),
                        Latitud = reader.GetDecimal(reader.GetOrdinal("latitud")),
                        Longitud = reader.GetDecimal(reader.GetOrdinal("longitud"))
                    };
                }

                return new PuntoPartida
                {
                    Direccion = string.Empty,
                    Latitud = 0,
                    Longitud = 0
                };
            }
            catch
            {
                return new PuntoPartida
                {
                    Direccion = string.Empty,
                    Latitud = 0,
                    Longitud = 0
                };
            }
        }

        private int RetornarIdEstadoRuta(string estado = "Sin Asignar")
        {
            string query = @"SELECT idEstadoRuta FROM EstadoRuta WHERE descripcion = @estado";

            using var conn = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
            using var command = new SqlCommand(query, conn);
            command.Parameters.AddWithValue("@estado", estado);

            conn.Open();
            object? result = command.ExecuteScalar();

            if (result != null && int.TryParse(result.ToString(), out int idEstado))
            {
                return idEstado;
            }

            return -1;
        }
        public async Task<string> GetRouteAsync()
        {
            // JSON de ejemplo que querés enviar a VROOM
            var json = @"
            {
              ""vehicles"": [
                {""id"":1,""profile"":""car"",""start"":[-58.4525,-34.5745],""capacity"":[10000],""dimensions"":[10,10,10]},
                {""id"":2,""profile"":""car"",""start"":[-58.4525,-34.5745],""capacity"":[10000],""dimensions"":[10,10,10]}
              ],
              ""jobs"": [
                {""id"":1,""location"":[-58.4491,-34.5772],""amount"":[100]},
                {""id"":2,""location"":[-58.5089,-34.6044],""amount"":[100]},
                {""id"":3,""location"":[-58.5373,-34.5759],""amount"":[100]},
                {""id"":4,""location"":[-58.5438,-34.5792],""amount"":[100]},
                {""id"":5,""location"":[-58.4269,-34.5780],""amount"":[100]},
                {""id"":6,""location"":[-58.4904,-34.6009],""amount"":[100]},
                {""id"":7,""location"":[-58.4457,-34.6075],""amount"":[100]},
                {""id"":8,""location"":[-58.3926,-34.6092],""amount"":[100]},
                {""id"":9,""location"":[-58.4368,-34.5628],""amount"":[100]}
              ],
              ""parameters"": {
                ""time_limit"":3600,
                ""max_routes"":2,
                ""vehicle_speed"":50,
                ""service_time"":300
              }
            }";

            // Crea el contenido para el POST
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Llamada POST a VROOM
            var response = await _httpClient.PostAsync(VroomUrl, content);

            // Lanza excepción si hay error HTTP
            response.EnsureSuccessStatusCode();

            // Devuelve el JSON de VROOM como string
            return await response.Content.ReadAsStringAsync();
        }



        public async Task<bool> AsignarConductorAsync(int idRuta, int idConductor, string patente, string enlace)
        {


            if (string.IsNullOrWhiteSpace(patente))
                return false;

            if (idRuta <= 0 || idConductor <= 0)
                return false;

            try
            {
                using var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
                await connection.OpenAsync();

                // Usamos transacción para asegurar consistencia
                using var transaction = connection.BeginTransaction(); // <-- SqlTransaction

                try
                {
                    // 1️⃣ Actualizar la ruta
                    var queryRuta = @"UPDATE Ruta SET idUsuario = @IdConductor, idEstadoRuta = (SELECT idEstadoRuta FROM EstadoRuta WHERE descripcion = 'Pendiente') WHERE idRuta = @IdRuta";

                    using var cmdRuta = new SqlCommand(queryRuta, connection, transaction);
                    cmdRuta.Parameters.AddWithValue("@IdRuta", idRuta);
                    cmdRuta.Parameters.AddWithValue("@IdConductor", idConductor);

                    var filasRuta = await cmdRuta.ExecuteNonQueryAsync();

                    if (filasRuta == 0)
                    {
                        await transaction.RollbackAsync();
                        return false;
                    }

                    // 3) extraer mail, y nombreCompeto de usuario rol = conductor y mandar mail diciendo que fue asignado a la ruta 

                    string queryMail = @"SELECT u.email, (u.nombre + ' ' + u.apellido) AS nombreCompleto FROM Usuario u INNER JOIN Rol r ON r.idRol = u.idRol INNER JOIN Ruta ru ON ru.idUsuario = u.idUsuario WHERE r.nombre = 'Conductor' AND ru.idRuta = @IdRuta";

                    string correo = "";
                    string nombreCompleto = "";

                    using (var cmdMail = new SqlCommand(queryMail, connection, transaction))
                    {
                        cmdMail.Parameters.AddWithValue("@IdRuta", idRuta);
                        using var reader = await cmdMail.ExecuteReaderAsync();
                        if (await reader.ReadAsync())
                        {
                            correo = reader["email"].ToString();
                            nombreCompleto = reader["nombreCompleto"].ToString();
                        }
                    }
                    if (string.IsNullOrWhiteSpace(correo))
                    {
                        await transaction.RollbackAsync();
                        return false; // no hay correo -> no seguimos
                    }


                    // 5️⃣ Armar cuerpo del correo
                    string cuerpo = $@"<p>Hola {nombreCompleto},</p><p>Se te ha asignado una nueva ruta de entrega.</p><p>Puedes verla en el siguiente enlace:</p><p><a href='{enlace}'>{enlace}</a></p>";

                    // 6️⃣ Enviar correo
                    bool enviado = await _emailService.EnviarCorreo(correo, "Alerta - Asignación de Ruta", cuerpo);

                    if (!enviado)
                    {
                        // Decisión: rollback o no
                        await transaction.RollbackAsync();
                        return false;
                    }


                    transaction.Commit();
                    return true;
                }
                catch
                {
                    transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en AsignarConductorAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<List<RouteViewModel>> ObtenerRutasSinAsignarAsync()
        {
            var rutas = new List<RouteViewModel>();

            try
            {
                using var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
                await connection.OpenAsync();

                var query = "SELECT r.idRuta, f.patente, r.fechaCreacion, (select count(*) from RutaPedidos where idRuta = r.idRuta) as cantPedidos, er.descripcion as estado, r.idCamion FROM Ruta r inner join Flota f on f.idCamion = r.idCamion inner join EstadoRuta er on er.idEstadoRuta = r.idEstadoRuta where er.descripcion = 'sin asignar' ORDER BY r.fechaCreacion\r\n";

                using var cmd = new SqlCommand(query, connection);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    rutas.Add(new RouteViewModel
                    {
                        IdRuta = reader.GetInt32(reader.GetOrdinal("idRuta")),
                        Patente = reader.GetString(reader.GetOrdinal("patente")),
                        FechaCreacion = reader.GetDateTime(reader.GetOrdinal("fechaCreacion")),
                        CantPedidos = reader.GetInt32(reader.GetOrdinal("cantPedidos")),
                        Estado = reader.GetString(reader.GetOrdinal("estado")),
                        IdCamion = reader.GetInt32(reader.GetOrdinal("idCamion"))
                    });
                }


            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en ObtenerRutasSinAsignarAsync: {ex.Message}");
                // Devuelve lista vacía en caso de error
                return new List<RouteViewModel>();
            }

            return rutas;
        }

        public async Task<RouteViewModel> RetornarRutaConIdRuta(string idRuta)
        {
            RouteViewModel? ruta = null;

            try
            {
                using var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
                await connection.OpenAsync();

                var query = "SELECT r.idRuta, f.patente, r.fechaCreacion, (select count(*) from RutaPedidos where idRuta = r.idRuta) as cantPedidos, er.descripcion as estado, r.idCamion, u.nombre + ' ' + u.apellido as nombreCompleto from Ruta r inner join Flota f on f.idCamion = r.idCamion inner join EstadoRuta er on er.idEstadoRuta = r.idEstadoRuta left join Usuario u on u.idUsuario = r.idUsuario where idRuta = @idRuta";

                using var cmd = new SqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@idRuta", idRuta);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    ruta = new RouteViewModel
                    {
                        IdRuta = reader.GetInt32(reader.GetOrdinal("idRuta")),
                        Patente = reader.GetString(reader.GetOrdinal("patente")),
                        FechaCreacion = reader.GetDateTime(reader.GetOrdinal("fechaCreacion")),
                        CantPedidos = reader.GetInt32(reader.GetOrdinal("cantPedidos")),
                        Estado = reader.GetString(reader.GetOrdinal("estado")),
                        IdCamion = reader.GetInt32(reader.GetOrdinal("idCamion")),
                        NombreCompletoConductor = reader["nombreCompleto"] != DBNull.Value ? reader["nombreCompleto"].ToString() : null
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en RetornarRutaConIdRuta: {ex.Message}");
                return null;
            }

            return ruta;
        }

        public async Task<RouteViewModel> RetornarRutaConNombreUsuario(string nombreUsuario)
        {
            RouteViewModel? ruta = null;

            try
            {
                using var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
                await connection.OpenAsync();

                var query = "SELECT r.idRuta, f.patente, r.fechaCreacion, (select count(*) from RutaPedidos where idRuta = r.idRuta) as cantPedidos, er.descripcion as estado, r.idCamion, u.nombre + ' ' + u.apellido as nombreCompleto from Ruta r inner join Flota f on f.idCamion = r.idCamion inner join EstadoRuta er on er.idEstadoRuta = r.idEstadoRuta inner join Usuario u on u.idUsuario = r.idUsuario where u.usuario = @nombreUsuario and er.descripcion not in ('Finalizada','Cancelada')";

                using var cmd = new SqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@nombreUsuario", nombreUsuario);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    ruta = new RouteViewModel
                    {
                        IdRuta = reader.GetInt32(reader.GetOrdinal("idRuta")),
                        Patente = reader.GetString(reader.GetOrdinal("patente")),
                        FechaCreacion = reader.GetDateTime(reader.GetOrdinal("fechaCreacion")),
                        CantPedidos = reader.GetInt32(reader.GetOrdinal("cantPedidos")),
                        Estado = reader.GetString(reader.GetOrdinal("estado")),
                        IdCamion = reader.GetInt32(reader.GetOrdinal("idCamion")),
                        NombreCompletoConductor = reader.GetString(reader.GetOrdinal("nombreCompleto"))
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en RetornarRutaConNombreUsuario: {ex.Message}");
                return null;
            }

            return ruta;
        }

        public byte[] ExportarRutasPdf(string columna = "r.fechaCreacion", string orden = "desc")
        {
            var rutas = ObtenerRutas(columna, orden);

            using (var ms = new MemoryStream())
            {
                PdfDocument document = new PdfDocument();
                var page = document.AddPage();
                page.Orientation = PdfSharpCore.PageOrientation.Landscape;
                XGraphics gfx = XGraphics.FromPdfPage(page);

                XFont font = new XFont("Verdana", 8, XFontStyle.Regular);
                XFont headerFont = new XFont("Verdana", 8, XFontStyle.Bold);

                double startX = 20;
                double startY = 50;
                double rowHeight = 18;

                double[] colWidths = { 40, 90, 60, 60, 80, 80, 80, 70 };
                string[] headers = { "ID Ruta", "Conductor", "Patente", "Pedidos", "Creación", "Inicio", "Fin", "Estado" };

                // Cabecera
                double currentX = startX;
                for (int i = 0; i < headers.Length; i++)
                {
                    gfx.DrawString(headers[i], headerFont, XBrushes.Black,
                        new XRect(currentX, startY, colWidths[i], rowHeight), XStringFormats.Center);
                    currentX += colWidths[i];
                }

                gfx.DrawLine(XPens.Black, startX, startY + rowHeight, startX + colWidths.Sum(), startY + rowHeight);

                double y = startY + rowHeight;

                foreach (var ruta in rutas)
                {
                    currentX = startX;

                    string[] valores =
                    {
                ruta.IdRuta.ToString(),
                ruta.Conductor ?? "-",
                ruta.Patente ?? "-",
                ruta.CantPedidos.ToString(),
                ruta.FechaCreacion.ToString("dd/MM/yyyy"),
                ruta.FechaInicio?.ToString("dd/MM/yyyy") ?? "-",
                ruta.FechaFin?.ToString("dd/MM/yyyy") ?? "-",
                ruta.Estado ?? "-"
            };

                    for (int i = 0; i < valores.Length; i++)
                    {
                        gfx.DrawString(valores[i], font, XBrushes.Black,
                            new XRect(currentX, y, colWidths[i], rowHeight), XStringFormats.Center);
                        currentX += colWidths[i];
                    }

                    gfx.DrawLine(XPens.Gray, startX, y + rowHeight, startX + colWidths.Sum(), y + rowHeight);
                    y += rowHeight;
                }

                document.Save(ms, false);
                return ms.ToArray();
            }
        }


    }
}