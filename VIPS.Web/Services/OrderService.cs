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



namespace VIPS.Web.Services
{
    public class OrderService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public OrderService(IConfiguration configuration)
        {
            _configuration = configuration;
            _httpClient = new HttpClient();
            // Identificate para no ser bloqueado (User-Agent obligatorio en Nominatim)
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "VIPS-Logistics-System/1.0");
        }

        public async Task<(double lat, double lon, string postalCode)> GeocodeAsync(string domicilio, string ciudad, string provincia)
        {
            string direccionCompleta = $"{domicilio}, {ciudad}, {provincia}, Argentina";


            string url = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(direccionCompleta)}&format=json&addressdetails=1";

            var response = await _httpClient.GetStringAsync(url);
            var results = JArray.Parse(response);

            if (results.Count == 0)
                throw new Exception("No se encontraron resultados para la dirección.");

            var first = results[0];
            double lat = double.Parse(first["lat"].ToString(), System.Globalization.CultureInfo.InvariantCulture);
            double lon = double.Parse(first["lon"].ToString(), System.Globalization.CultureInfo.InvariantCulture);

            string postalCode = "";
            if (first["address"]?["postcode"] != null)
                postalCode = first["address"]["postcode"].ToString();

            return (lat, lon, postalCode);
        }

        public List<SelectListItem> ObtenerListaNombreyIdEstado()
        {
            try
            {
                var query = "SELECT descripcion, idEstado FROM EstadoPedido ORDER BY idEstado DESC";

                using var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
                connection.Open();

                using var cmd = new SqlCommand(query, connection);
                using var adapter = new SqlDataAdapter(cmd);

                var dataTable = new DataTable();
                adapter.Fill(dataTable);

                var lista = new List<SelectListItem>();

                foreach (DataRow row in dataTable.Rows)
                {
                    lista.Add(new SelectListItem
                    {
                        Value = row["idEstado"].ToString(),
                        Text = row["descripcion"].ToString()  // texto visible = nombre
                    });
                }

                return lista;
            }
            catch
            {
                return new List<SelectListItem>();
            }
        }

        public OrderModel? RetornarOrderModelConIdPedido(int idPedido)
        {
            try
            {
                var query = @"
            SELECT 
                p.idPedido,
                p.idCliente,
                p.ancho,
                p.largo,
                p.alto,
                p.peso,
                d.direccion AS DomicilioEntrega,
                d.ciudad,
                d.provincia,
                p.idEstadoPedido
            FROM Pedido p
            INNER JOIN DomicilioEntrega d ON d.idDomicilioEntrega = p.idDomicilioEntrega
            WHERE p.idPedido = @idPedido";

                using var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
                connection.Open();

                using var cmd = new SqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@idPedido", idPedido);

                using var reader = cmd.ExecuteReader();

                if (reader.Read())
                {
                    return new OrderModel
                    {
                        IdPedido = Convert.ToInt32(reader["idPedido"]),
                        IdCliente = Convert.ToInt32(reader["idCliente"]),
                        Ancho = reader["ancho"]?.ToString() ?? "",
                        Largo = reader["largo"]?.ToString() ?? "",
                        Alto = reader["alto"]?.ToString() ?? "",
                        Peso = reader["peso"]?.ToString() ?? "",
                        DomicilioEntrega = reader["DomicilioEntrega"]?.ToString() ?? "",
                        Ciudad = reader["ciudad"]?.ToString() ?? "",
                        Provincia = reader["provincia"]?.ToString() ?? "",
                        IdEstadoPedido = Convert.ToInt32(reader["idEstadoPedido"])
                    };
                }

                return null; // No encontró pedido
            }
            catch (Exception)
            {
                return null; // Manejar excepción según convenga
            }
        }


        public PedidoRutaViewModel? RetornarPedidoRutaViewModelConIdPedido(string idPedido)
        {
            try
            {
                var query = @"
            SELECT 
                p.idPedido,
                c.nombre + ' ' + c.apellido as cliente,
                d.direccion,
                ep.descripcion as estado
            FROM Pedido p
            INNER JOIN DomicilioEntrega d ON d.idDomicilioEntrega = p.idDomicilioEntrega
            inner join Cliente c on c.idCliente = p.idCliente
            inner join EstadoPedido ep on ep.idEstado = p.idEstadoPedido
            WHERE p.idPedido = @idPedido";

                using var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
                connection.Open();

                using var cmd = new SqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@idPedido", Convert.ToInt32(idPedido));

                using var reader = cmd.ExecuteReader();

                if (reader.Read())
                {
                    return new PedidoRutaViewModel
                    {
                        IdPedido = reader.GetInt32(reader.GetOrdinal("idPedido")),
                        Cliente = reader.GetString(reader.GetOrdinal("cliente")),
                        Direccion = reader.GetString(reader.GetOrdinal("direccion")),
                        Estado = reader.GetString(reader.GetOrdinal("estado")),
                    };
                }

                return null; // No encontró pedido
            }
            catch (Exception)
            {
                return null; // Manejar excepción según convenga
            }
        }


        public async Task<Dictionary<int, List<PedidoRuta>>> ObtenerPedidosPorRutaAsync()
        {
            try
            {
                var result = new Dictionary<int, List<PedidoRuta>>();

                using var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
                await connection.OpenAsync();

                var query = "SELECT  rp.idRuta, p.idPedido, c.nombre + ' ' + c.apellido as cliente, de.direccion, rp.ordenEntrega FROM RutaPedidos rp INNER JOIN Pedido p ON rp.idPedido = p.idPedido INNER JOIN Cliente c ON p.idCliente = c.idCliente INNER JOIN DomicilioEntrega de on de.idDomicilioEntrega = p.idDomicilioEntrega WHERE rp.eliminado = 0 AND rp.idRuta in (select idRuta from Ruta r inner join EstadoRuta er on er.idEstadoRuta = r.idEstadoRuta where er.descripcion = 'Sin Asignar') ORDER BY rp.idRuta, rp.ordenEntrega\r\n";

                using var cmd = new SqlCommand(query, connection);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var idRutaDb = reader.GetInt32(reader.GetOrdinal("idRuta"));

                    var pedido = new PedidoRuta
                    {
                        IdPedido = reader.GetInt32(reader.GetOrdinal("idPedido")),
                        Cliente = reader.GetString(reader.GetOrdinal("cliente")),
                        Direccion = reader.GetString(reader.GetOrdinal("direccion")),
                        OrdenEntrega = reader.GetInt32(reader.GetOrdinal("ordenEntrega"))
                    };

                    if (!result.ContainsKey(idRutaDb))
                    {
                        result[idRutaDb] = new List<PedidoRuta>();
                    }

                    result[idRutaDb].Add(pedido);
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en ObtenerPedidosPorRutaAsync: {ex.Message}");
                return new Dictionary<int, List<PedidoRuta>>(); // devuelve vacío
            }
        }

        public async Task<List<PedidoRuta>> ObtenerPedidosPorIdRutaAsync(string idRuta)
        {
            try
            {
                var result = new List<PedidoRuta>();

                using var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
                await connection.OpenAsync();

                var query = "SELECT rp.idRuta, p.idPedido, c.nombre + ' ' + c.apellido as cliente, de.direccion, rp.ordenEntrega, de.latitud, de.longitud, ep.descripcion as estado FROM RutaPedidos rp INNER JOIN Pedido p ON rp.idPedido = p.idPedido INNER JOIN Cliente c ON p.idCliente = c.idCliente INNER JOIN DomicilioEntrega de on de.idDomicilioEntrega = p.idDomicilioEntrega INNER JOIN EstadoPedido ep on ep.idEstado = p.idEstadoPedido WHERE rp.eliminado = 0 AND rp.idRuta = @idRuta ORDER BY rp.idRuta, rp.ordenEntrega";

                using var cmd = new SqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@idRuta", idRuta);


                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var idRutaDb = reader.GetInt32(reader.GetOrdinal("idRuta"));

                    var pedido = new PedidoRuta
                    {
                        IdPedido = reader.GetInt32(reader.GetOrdinal("idPedido")),
                        Cliente = reader.GetString(reader.GetOrdinal("cliente")),
                        Direccion = reader.GetString(reader.GetOrdinal("direccion")),
                        OrdenEntrega = reader.GetInt32(reader.GetOrdinal("ordenEntrega")),
                        Latitud = reader.GetDecimal(reader.GetOrdinal("latitud")),
                        Longitud = reader.GetDecimal(reader.GetOrdinal("longitud")),
                        Estado = reader.GetString(reader.GetOrdinal("estado"))

                    };


                    result.Add(pedido);
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en ObtenerPedidosPorIdRutaAsync: {ex.Message}");
                return new List<PedidoRuta>(); // devuelve vacío
            }
        }

        public async Task<List<PedidoRuta>> ObtenerPedidosPorNombreUsuarioAsync(string nombreUsuario)
        {
            try
            {
                var result = new List<PedidoRuta>();

                using var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
                await connection.OpenAsync();

                var query = "SELECT rp.idRuta, p.idPedido, c.nombre + ' ' + c.apellido AS cliente, de.direccion, rp.ordenEntrega, de.latitud, de.longitud, ep.descripcion AS estado, c.telefono, agg.cantTotal, agg.cantCompletados FROM RutaPedidos rp INNER JOIN Pedido p ON rp.idPedido = p.idPedido INNER JOIN Cliente c ON p.idCliente = c.idCliente INNER JOIN DomicilioEntrega de ON de.idDomicilioEntrega = p.idDomicilioEntrega INNER JOIN Ruta r ON r.idRuta = rp.idRuta INNER JOIN EstadoRuta er ON er.idEstadoRuta = r.idEstadoRuta INNER JOIN Usuario u ON r.idUsuario = u.idUsuario INNER JOIN EstadoPedido ep ON ep.idEstado = p.idEstadoPedido INNER JOIN (SELECT rp2.idRuta, COUNT(*) AS cantTotal, SUM(CASE WHEN ep2.descripcion = 'Entregado' THEN 1 WHEN ep2.descripcion = 'Fallido' THEN 1 ELSE 0 END) AS cantCompletados FROM RutaPedidos rp2 INNER JOIN Pedido p2 ON rp2.idPedido = p2.idPedido INNER JOIN EstadoPedido ep2 ON ep2.idEstado = p2.idEstadoPedido GROUP BY rp2.idRuta) agg ON agg.idRuta = rp.idRuta WHERE er.descripcion NOT IN ('Finalizada','Cancelada') AND u.usuario = @usuario ORDER BY rp.idRuta, rp.ordenEntrega;";

                using var cmd = new SqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@usuario", nombreUsuario);


                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var pedido = new PedidoRuta
                    {
                        IdPedido = reader.GetInt32(reader.GetOrdinal("idPedido")),
                        Cliente = reader.GetString(reader.GetOrdinal("cliente")),
                        Direccion = reader.GetString(reader.GetOrdinal("direccion")),
                        OrdenEntrega = reader.GetInt32(reader.GetOrdinal("ordenEntrega")),
                        Latitud = reader.GetDecimal(reader.GetOrdinal("latitud")),
                        Longitud = reader.GetDecimal(reader.GetOrdinal("longitud")),
                        Estado = reader.GetString(reader.GetOrdinal("estado")),
                        TelefonoCliente = reader.GetString(reader.GetOrdinal("telefono")),
                        CantCompletados = reader.GetInt32(reader.GetOrdinal("cantCompletados")),
                        CantTotal= reader.GetInt32(reader.GetOrdinal("cantTotal"))


                    };


                    result.Add(pedido);
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en ObtenerPedidosPorIdRutaAsync: {ex.Message}");
                return new List<PedidoRuta>(); // devuelve vacío
            }
        }

        public ResultadoOperacion EliminarPedido(int idPedido)
        {
            try
            {
                using var conn = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));

                conn.Open();

                string query = @"UPDATE Pedido SET eliminado = 1, fechaModificacion = GETDATE() WHERE idPedido= @idPedido";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@idPedido", idPedido);

                //devulve 1 si creo una fila
                int filas = cmd.ExecuteNonQuery();

                return new ResultadoOperacion
                {
                    Exito = filas > 0,
                    Mensaje = filas > 0
                ? "Pedido borrado correctamente"
                : "No se borro ningún registro"
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

        public async Task<ResultadoOperacion> CrearPedido(OrderModel model)
        {
            try
            {
                // 1. Geocodificación
                var (lat, lon, postalCode) = await GeocodeAsync(model.DomicilioEntrega, model.Ciudad, model.Provincia);

                // 2. Crear domicilio entrega
                var idDomicilioEntrega = CrearDomicilioEntrega(model.DomicilioEntrega, model.Ciudad, model.Provincia, lat, lon, postalCode);


                // 4. Retornar idEstadoPedido = pendiente

                var idEstado = RetornarIdEstadoPedido();

                decimal Ancho, Largo, Alto, Peso;

                try
                {
                    Ancho = ConvertirStringADecimal(model.Ancho, 2);
                    Largo = ConvertirStringADecimal(model.Largo, 2);
                    Alto = ConvertirStringADecimal(model.Alto, 2);
                    Peso = ConvertirStringADecimal(model.Peso, 2);
                }
                catch (FormatException ex)
                {
                    return new ResultadoOperacion { Exito = false, Mensaje = ex.Message };
                }
                catch (ArgumentException ex)
                {
                    return new ResultadoOperacion { Exito = false, Mensaje = ex.Message };
                }



                using var conn = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
                conn.Open();


                string query = @"INSERT INTO Pedido (ancho, largo, alto, peso, idEstadoPedido, idCliente, idDomicilioEntrega) VALUES (@ancho, @largo, @alto, @peso, @idEstadoPedido, @idCliente, @idDomicilioEntrega)";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@ancho", Ancho);
                cmd.Parameters.AddWithValue("@largo", Largo);
                cmd.Parameters.AddWithValue("@alto", Alto);
                cmd.Parameters.AddWithValue("@peso", Peso);
                cmd.Parameters.AddWithValue("@idEstadoPedido", idEstado);
                cmd.Parameters.AddWithValue("@idCliente", model.IdCliente);
                cmd.Parameters.AddWithValue("@idDomicilioEntrega", idDomicilioEntrega);


                //devulve 1 si creo una fila
                int filas = cmd.ExecuteNonQuery();

                return new ResultadoOperacion
                {
                    Exito = filas > 0,
                    Mensaje = filas > 0
                ? "Pedido creado correctamente"
                : "No se insertó ningún registro"
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

        private decimal ConvertirStringADecimal(string numero, int cantDecimales)
        {
            if (string.IsNullOrWhiteSpace(numero))
                throw new ArgumentException("El número no puede estar vacío.");

            // Normalizo: si viene con punto lo convierto a coma
            var normalizado = numero.Replace(".", ",");

            if (!decimal.TryParse(normalizado, NumberStyles.Any, new CultureInfo("es-AR"), out var valor))
                throw new FormatException($"El valor '{numero}' no es un número válido.");

            // Redondeo a la cantidad de decimales deseada
            return Math.Round(valor, cantDecimales);
        }

        // Inserta domicilioEntrega y devuelve el ID
        private int CrearDomicilioEntrega(string domicilio, string ciudad, string provincia, double lat, double lon, string postalCode)
        {
            using var conn = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
            conn.Open();




            // 1. Verificar si ya existe
            string checkQuery = @"
SELECT idDomicilioEntrega 
FROM DomicilioEntrega
WHERE UPPER(direccion) = UPPER(@domicilio)
  AND UPPER(ciudad) = UPPER(@ciudad)
  AND UPPER(provincia) = UPPER(@provincia)
  AND ABS(latitud - @lat) < 0.000001
  AND ABS(longitud - @lon) < 0.000001
  AND UPPER(codigoPostal) = UPPER(@cp)";

            using (var checkCmd = new SqlCommand(checkQuery, conn))
            {
                checkCmd.Parameters.AddWithValue("@domicilio", domicilio);
                checkCmd.Parameters.AddWithValue("@ciudad", ciudad);
                checkCmd.Parameters.AddWithValue("@provincia", provincia);
                checkCmd.Parameters.AddWithValue("@cp", postalCode ?? "");
                checkCmd.Parameters.AddWithValue("@lat", lat);
                checkCmd.Parameters.AddWithValue("@lon", lon);

                var result = checkCmd.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                {
                    // Ya existe, devolvemos el ID encontrado
                    return (int)result;
                }
            }

            decimal latDecimal = Math.Round((decimal)lat, 6);
            decimal lonDecimal = Math.Round((decimal)lon, 6);

            // 2. Insertar si no existe
            string insertQuery = @"INSERT INTO DomicilioEntrega 
                           (direccion, codigoPostal, ciudad, provincia, latitud, longitud)
                           OUTPUT INSERTED.idDomicilioEntrega
                           VALUES (@domicilio, @cp, @ciudad, @provincia, @lat, @lon)";

            using (var insertCmd = new SqlCommand(insertQuery, conn))
            {
                insertCmd.Parameters.AddWithValue("@domicilio", domicilio);
                insertCmd.Parameters.AddWithValue("@cp", postalCode ?? "");
                insertCmd.Parameters.AddWithValue("@ciudad", ciudad);
                insertCmd.Parameters.AddWithValue("@provincia", provincia);
                insertCmd.Parameters.AddWithValue("@lat", latDecimal);
                insertCmd.Parameters.AddWithValue("@lon", lonDecimal);

                int id = (int)insertCmd.ExecuteScalar();
                return id;
            }
        }


        public async Task<ResultadoOperacion> EditarPedido(OrderModel model)
        {
            var resultado = new ResultadoOperacion();

            try
            {
                using var conn = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
                conn.Open();

                var (lat, lon, postalCode) = await GeocodeAsync(model.DomicilioEntrega, model.Ciudad, model.Provincia);

                // 2. Crear domicilio entrega
                var idDomicilioEntrega = CrearDomicilioEntrega(model.DomicilioEntrega, model.Ciudad, model.Provincia, lat, lon, postalCode);

                decimal Ancho, Largo, Alto, Peso;

                try
                {
                    Ancho = ConvertirStringADecimal(model.Ancho, 2);
                    Largo = ConvertirStringADecimal(model.Largo, 2);
                    Alto = ConvertirStringADecimal(model.Alto, 2);
                    Peso = ConvertirStringADecimal(model.Peso, 2);
                }
                catch (FormatException ex)
                {
                    return new ResultadoOperacion { Exito = false, Mensaje = ex.Message };
                }
                catch (ArgumentException ex)
                {
                    return new ResultadoOperacion { Exito = false, Mensaje = ex.Message };
                }

                // 2. Actualizar pedido
                string updateQuery = @"
            UPDATE Pedido
            SET 
                idCliente = @idCliente,
                ancho = @ancho,
                largo = @largo,
                alto = @alto,
                peso = @peso,
                idDomicilioEntrega = @idDomicilioEntrega,
                idEstadoPedido = @idEstadoPedido,
                fechaModificacion = GETDATE()
            WHERE idPedido = @idPedido";

                using var cmd = new SqlCommand(updateQuery, conn);


                cmd.Parameters.AddWithValue("@idCliente", model.IdCliente);
                cmd.Parameters.AddWithValue("@ancho", Ancho);
                cmd.Parameters.AddWithValue("@largo", Largo);
                cmd.Parameters.AddWithValue("@alto", Alto);
                cmd.Parameters.AddWithValue("@peso", Peso);
                cmd.Parameters.AddWithValue("@idDomicilioEntrega", idDomicilioEntrega);
                cmd.Parameters.AddWithValue("@idEstadoPedido", model.IdEstadoPedido);
                cmd.Parameters.AddWithValue("@idPedido", model.IdPedido);

                int filasAfectadas = cmd.ExecuteNonQuery();

                if (filasAfectadas > 0)
                {
                    resultado.Exito = true;
                    resultado.Mensaje = "Pedido actualizado correctamente";
                }
                else
                {
                    resultado.Exito = false;
                    resultado.Mensaje = "No se encontró el pedido para actualizar";
                }
            }
            catch (Exception ex)
            {
                resultado.Exito = false;
                resultado.Mensaje = $"Error al actualizar pedido: {ex.Message}";
            }

            return resultado;
        }


        private string DecimalAStr(decimal numero, int cantDecimales = 2)
        {
            var culture = new CultureInfo("es-AR");
            culture.NumberFormat.NumberDecimalSeparator = ",";
            culture.NumberFormat.NumberGroupSeparator = "."; // opcional, para separar miles

            return numero.ToString($"N{cantDecimales}", culture);
        }

        private int RetornarIdClienteConDni(string dni)
        {
            string query = @"SELECT idCliente FROM CLiente WHERE dni = @dni and eliminado = 0";

            using var conn = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
            using var command = new SqlCommand(query, conn);
            command.Parameters.AddWithValue("@dni", dni);

            conn.Open();
            object? result = command.ExecuteScalar();

            if (result != null && int.TryParse(result.ToString(), out int idCliente))
            {
                return idCliente;
            }

            return -1; // si no encuentra el cliente
        }

        private int RetornarIdEstadoPedido(string estado = "pendiente")
        {
            string query = @"SELECT idEstado FROM EstadoPedido WHERE descripcion = @estado";

            using var conn = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
            using var command = new SqlCommand(query, conn);
            command.Parameters.AddWithValue("@estado", estado);

            conn.Open();
            object? result = command.ExecuteScalar();

            if (result != null && int.TryParse(result.ToString(), out int idEstado))
            {
                return idEstado;
            }

            return -1; // si no encuentra el cliente
        }


        public int RetornarIdDomicilioConDomicilio(string domicilioEntrega)
        {
            string query = @"SELECT idDomicilioEntrega FROM DomicilioEntrega WHERE domicilioEntrega = @domicilioEntrega and eliminado = 0";

            using var conn = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
            using var command = new SqlCommand(query, conn);
            command.Parameters.AddWithValue("@domicilioEntrega", domicilioEntrega);

            conn.Open();
            object? result = command.ExecuteScalar();

            if (result != null && int.TryParse(result.ToString(), out int idDomicilioEntrega))
            {
                return idDomicilioEntrega;
            }

            return -1; // si no encuentra el usuario
        }


        public List<OrderViewModel> ObtenerPedidos(string columna, string orden, string? parametro = null)
        {
            try
            {
                var columnasPermitidas = new[] { "idPedido", "nombreCliente", "peso", "fechaCreacion", "fechaDespacho", "estadoPedido", "direccion"};
                if (!columnasPermitidas.Contains(columna))
                    throw new ArgumentException("Columna no válida");


                string columnaDB = columna switch
                {
                    "idPedido" => "p.idPedido",
                    "nombreCliente" => "c.nombre",
                    "peso" => "p.peso",
                    "fechaCreacion" => "p.fechaCreacion",
                    "fechaDespacho" => "p.fechaDespacho",
                    "estadoPedido" => "e.descripcion",
                    "direccion" => "d.direccion",
                    _ => "p.fechaCreacion"
                };

                // Orden seguro
                var ordenSeguro = (orden?.ToUpper() == "DESC") ? "DESC" : "ASC";

                var query = $@"select p.idPedido, c.nombre, p.peso, p.fechaCreacion, p.fechaDespacho, e.descripcion as estadoPedido, d.direccion from Pedido p inner join Cliente c on c.idCliente = p.idCliente inner join DomicilioEntrega d on d.idDomicilioEntrega = p.idDomicilioEntrega inner join EstadoPedido e on e.idEstado = p.idEstadoPedido where p.eliminado = 0";


                if (!string.IsNullOrEmpty(parametro))
                {
                    query += $@" AND c.nombre LIKE @parametro order by {columna} {ordenSeguro}";
                }
                else
                {
                    query += $" order by {columna} {ordenSeguro}";

                }


                using var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
                connection.Open();

                using var cmd = new SqlCommand(query, connection);

                if (!string.IsNullOrEmpty(parametro))
                {
                    // Búsqueda parcial
                    cmd.Parameters.AddWithValue("@parametro", "%" + parametro + "%");
                }
                using var adapter = new SqlDataAdapter(cmd);

                var dataTable = new DataTable();
                adapter.Fill(dataTable);



                var lista = new List<OrderViewModel>();

                foreach (DataRow row in dataTable.Rows)
                {

                    lista.Add(new OrderViewModel
                    {
                        IdPedido = Convert.ToInt32(row["idPedido"]),
                        NombreCliente = row["nombre"].ToString(),

                        Peso = DecimalAStr(Convert.ToDecimal(row["peso"])),

                        FechaCreacion = Convert.ToDateTime(row["fechaCreacion"]),
                        FechaDespacho = row["fechaDespacho"] != DBNull.Value ? Convert.ToDateTime(row["fechaDespacho"]) : (DateTime?)null,

                        EstadoPedido = row["estadoPedido"].ToString(),

                        Direccion = row["direccion"].ToString(),
                    });
                }


                return lista;
            }
            catch (Exception ex)
            {
                return new List<OrderViewModel>();
            }
        }


        public async Task<ResultadoOperacion> CambiarPuntoPartidaFlota(string DomicilioPartida, string CiudadPartida, string ProvinciaPartida)
        {

                var resultado = new ResultadoOperacion();
            try
            {

                // 1. Geocodificación
                var (lat, lon, postalCode) = await GeocodeAsync(DomicilioPartida, CiudadPartida, ProvinciaPartida);

                // 2. Crear domicilio entrega
                var idDomicilioInicio = CrearDomicilioEntrega(DomicilioPartida, CiudadPartida, ProvinciaPartida, lat, lon, postalCode);

                using var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
                await connection.OpenAsync();


              // 2️ Actualizar todos los vehículos para que tengan este domicilio como punto de partida
                var updateQuery = "UPDATE Flota SET idDomicilioInicio = @IdDomicilio";
                using (var cmd = new SqlCommand(updateQuery, connection))
                {
                    cmd.Parameters.AddWithValue("@IdDomicilio", idDomicilioInicio);
                    await cmd.ExecuteNonQueryAsync();
                }

                resultado.Exito = true;
                resultado.Mensaje = "Se actualizó correctamente el punto de partida de la flota.";
            }
            catch (Exception ex)
            {
                resultado.Exito = false;
                resultado.Mensaje = $"Error al actualizar el punto de partida: {ex.Message}";
            }

            return resultado;
        }


        public async Task<List<OrderRouteModel>> GetPedidosPendientesAsync()
        {
            try
            {
                var query = "select idPedido, ancho, largo, alto, peso, latitud, longitud from Pedido p inner join DomicilioEntrega d on d.idDomicilioEntrega = p.idDomicilioEntrega where p.eliminado = 0 and idEstadoPedido in (select idEstado from EstadoPedido where descripcion = 'Pendiente' or descripcion = 'Reprogramado')";

                using var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
                await connection.OpenAsync();

                using var cmd = new SqlCommand(query, connection);
                using var reader = await cmd.ExecuteReaderAsync();

                var lista = new List<OrderRouteModel>();

                while (await reader.ReadAsync())
                {
                    lista.Add(new OrderRouteModel
                    {
                        IdPedido = reader.GetInt32(reader.GetOrdinal("idPedido")),
                        Ancho = reader.GetDecimal(reader.GetOrdinal("ancho")),
                        Largo = reader.GetDecimal(reader.GetOrdinal("largo")),
                        Alto = reader.GetDecimal(reader.GetOrdinal("alto")),
                        Peso = reader.GetDecimal(reader.GetOrdinal("peso")),
                        Latitud = reader.GetDecimal(reader.GetOrdinal("latitud")),
                        Longitud = reader.GetDecimal(reader.GetOrdinal("longitud"))
                    });
                }

                return lista;
            }
            catch (Exception ex)
            {
                return new List<OrderRouteModel>();
            }
        }


        public byte[] ExportarPedidosPdf(string columna = "fechaCreacion", string orden = "desc", string? parametro = null)
        {
            // Traer los pedidos ordenados
            var pedidos = ObtenerPedidos(columna, orden, parametro);

            using (var ms = new MemoryStream())
            {
                PdfDocument document = new PdfDocument();
                var page = document.AddPage();
                XGraphics gfx = XGraphics.FromPdfPage(page);
                XFont font = new XFont("Verdana", 10, XFontStyle.Regular);

                // Posiciones iniciales
                double startX = 20;
                double startY = 50;
                double rowHeight = 20;

                // Dibujar encabezado
                gfx.DrawString("ID Pedido", font, XBrushes.Black, new XRect(startX, startY, 60, rowHeight), XStringFormats.Center);
                gfx.DrawString("NombreCliente", font, XBrushes.Black, new XRect(startX + 60, startY, 60, rowHeight), XStringFormats.Center);
                gfx.DrawString("Peso (gr)", font, XBrushes.Black, new XRect(startX + 120, startY, 60, rowHeight), XStringFormats.Center);
                gfx.DrawString("Fecha Creación", font, XBrushes.Black, new XRect(startX + 180, startY, 80, rowHeight), XStringFormats.Center);
                gfx.DrawString("Fecha Despacho", font, XBrushes.Black, new XRect(startX + 260, startY, 80, rowHeight), XStringFormats.Center);
                gfx.DrawString("Estado", font, XBrushes.Black, new XRect(startX + 340, startY, 80, rowHeight), XStringFormats.Center);
                gfx.DrawString("Dirección", font, XBrushes.Black, new XRect(startX + 420, startY, 120, rowHeight), XStringFormats.Center);

                // Línea debajo del encabezado
                gfx.DrawLine(XPens.Black, startX, startY + rowHeight, startX + 540, startY + rowHeight);

                // Dibujar filas
                double y = startY + rowHeight;
                foreach (var pedido in pedidos)
                {
                    y += rowHeight;
                    gfx.DrawString(pedido.IdPedido.ToString(), font, XBrushes.Black, new XRect(startX, y, 60, rowHeight), XStringFormats.Center);
                    gfx.DrawString(pedido.NombreCliente.ToString(), font, XBrushes.Black, new XRect(startX + 60, y, 60, rowHeight), XStringFormats.Center);
                    gfx.DrawString(pedido.Peso, font, XBrushes.Black, new XRect(startX + 120, y, 60, rowHeight), XStringFormats.Center);
                    gfx.DrawString(pedido.FechaCreacion.ToString("dd/MM/yyyy"), font, XBrushes.Black, new XRect(startX + 180, y, 80, rowHeight), XStringFormats.Center);
                    gfx.DrawString(pedido.FechaDespacho.HasValue? pedido.FechaDespacho.Value.ToString("dd/MM/yyyy"): "-",font,XBrushes.Black,new XRect(startX + 260, y, 80, rowHeight),XStringFormats.Center);
                    gfx.DrawString(pedido.EstadoPedido, font, XBrushes.Black, new XRect(startX + 340, y, 80, rowHeight), XStringFormats.Center);
                    gfx.DrawString(pedido.Direccion, font, XBrushes.Black, new XRect(startX + 420, y, 120, rowHeight), XStringFormats.Center);

                    // Línea debajo de la fila
                    gfx.DrawLine(XPens.Gray, startX, y + rowHeight, startX + 540, y + rowHeight);
                }

                // Guardar PDF en memoria
                document.Save(ms, false);

                return ms.ToArray();
            }
        }




    }
}