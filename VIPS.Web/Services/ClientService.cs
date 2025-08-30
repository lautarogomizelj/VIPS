using System.Data;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.ObjectPool;
using VIPS.Web.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace VIPS.Web.Services
{
    public class ClientService
    {
        private readonly IConfiguration _configuration;

        public ClientService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public ResultadoOperacion CrearCliente(ClienteModel clienteModel)
        {
            try
            {
                using var conn = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));

                conn.Open();

                string query = @"INSERT INTO Cliente (nombre, apellido, email, telefono, domicilioLegal, dni) VALUES (@nombre, @apellido, @email, @telefono, @domicilioLegal, @dni)";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@nombre", clienteModel.Nombre);
                cmd.Parameters.AddWithValue("@apellido", clienteModel.Apellido);
                cmd.Parameters.AddWithValue("@email", clienteModel.Email);
                cmd.Parameters.AddWithValue("@telefono", clienteModel.Telefono);
                cmd.Parameters.AddWithValue("@domicilioLegal", clienteModel.DomicilioLegal);
                cmd.Parameters.AddWithValue("@dni", clienteModel.Dni);

                //devulve 1 si creo una fila
                int filas = cmd.ExecuteNonQuery();

                return new ResultadoOperacion
                {
                    Exito = filas > 0,
                    Mensaje = filas > 0
                ? "Cliente creado correctamente"
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



        public List<ClienteViewModel> ObtenerClientes(string columna, string orden)
        {
            try
            {
                var columnasPermitidas = new[] { "idCliente", "nombre", "apellido", "domicilioLegal", "fechaCreacion" };
                if (!columnasPermitidas.Contains(columna))
                    throw new ArgumentException("Columna no válida");

                // Orden seguro
                var ordenSeguro = (orden?.ToUpper() == "DESC") ? "DESC" : "ASC";

                var query = $@"select idCliente, nombre, apellido, domicilioLegal, fechaCreacion from Cliente where eliminado = 0 order by {columna} {ordenSeguro}";

                using var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
                connection.Open();

                using var cmd = new SqlCommand(query, connection);
                using var adapter = new SqlDataAdapter(cmd);

                var dataTable = new DataTable();
                adapter.Fill(dataTable);

                var lista = new List<ClienteViewModel>();

                foreach (DataRow row in dataTable.Rows)
                {

                    lista.Add(new ClienteViewModel
                    {
                        IdCliente = Convert.ToInt32(row["idCliente"]),
                        Nombre = row["nombre"].ToString(),
                        Apellido = row["apellido"].ToString(),
                        DomicilioLegal = row["domicilioLegal"].ToString(),
                        FechaCreacion = Convert.ToDateTime(row["fechaCreacion"])

                    });
                }


                return lista;
            }
            catch (Exception ex)
            {
                return new List<ClienteViewModel>();
            }
        }

        public bool VerificarDniExistente(string dni)
        {
            var query = "SELECT COUNT(1) FROM Cliente WHERE dni = @dni and eliminado = 0";
            using var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
            connection.Open();

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@dni", dni);

            var count = (int)command.ExecuteScalar();
            return count > 0;
        }

        public bool VerificarEmailExistente(string email)
        {
            var query = "SELECT COUNT(1) FROM Cliente WHERE email = @email and eliminado = 0";
            using var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
            connection.Open();

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@email", email);

            var count = (int)command.ExecuteScalar();
            return count > 0;
        }

        public List<SelectListItem> ObtenerListaNombreyDni()
        {
            try
            {
                var query = "SELECT nombre, dni FROM Cliente where eliminado = 0 ORDER BY dni DESC";

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
                        Value = row["dni"].ToString(),   // value = dni
                        Text = row["nombre"].ToString()  // texto visible = nombre
                    });
                }

                return lista;
            }
            catch
            {
                return new List<SelectListItem>();
            }
        }


        public ClienteModel? retornarClienteModelConIdCliente(int idCliente)
        {
            try
            {
                var query = $@"select idCliente, dni, nombre, apellido, email, telefono, domicilioLegal from Cliente where idCliente = @idCliente and eliminado = 0";

                using var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
                connection.Open();

                using var cmd = new SqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@idCliente", idCliente);
                using var reader = cmd.ExecuteReader();

                if (reader.Read())
                {
                    return new ClienteModel
                    {
                        IdCliente = Convert.ToInt32(reader["idCliente"]),
                        Dni = reader["dni"].ToString(),
                        Nombre = reader["nombre"].ToString(),
                        Apellido = reader["apellido"].ToString(),
                        Email = reader["email"].ToString(),
                        Telefono = reader["telefono"].ToString(),
                        DomicilioLegal = reader["domicilioLegal"].ToString()
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public ResultadoOperacion EliminarCliente(int idCliente)
        {
            try
            {
                using var conn = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));

                conn.Open();

                string query = @"UPDATE Cliente SET eliminado = 1 WHERE idCliente= @idCliente";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@idCliente", idCliente);

                //devulve 1 si creo una fila
                int filas = cmd.ExecuteNonQuery();

                return new ResultadoOperacion
                {
                    Exito = filas > 0,
                    Mensaje = filas > 0
                ? "Cliente borrado correctamente"
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

        public ConflictoCliente ExisteConflicto(ClienteModel model)
        {
            var conflicto = new ConflictoCliente();

            string query = @"
        SELECT Dni, Email, Telefono, DomicilioLegal
        FROM Cliente
        WHERE IdCliente <> @IdCliente
          AND eliminado = 0
          AND (Dni = @Dni OR Email = @Email OR Telefono = @Telefono OR DomicilioLegal = @DomicilioLegal);";

            using var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
            connection.Open();

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@IdCliente", model.IdCliente);
            command.Parameters.AddWithValue("@Dni", model.Dni ?? "");
            command.Parameters.AddWithValue("@Email", model.Email ?? "");
            command.Parameters.AddWithValue("@Telefono", model.Telefono ?? "");
            command.Parameters.AddWithValue("@DomicilioLegal", model.DomicilioLegal ?? "");

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var dniDb = reader["Dni"] as string;
                var emailDb = reader["Email"] as string;
                var telefonoDb = reader["Telefono"] as string;
                var domicilioDb = reader["DomicilioLegal"] as string;

                if (!string.IsNullOrEmpty(dniDb) && dniDb == model.Dni)
                {
                    conflicto.Dni = true;
                }
                if (!string.IsNullOrEmpty(emailDb) && emailDb == model.Email)
                {
                    conflicto.Email = true;
                }
                if (!string.IsNullOrEmpty(telefonoDb) && telefonoDb == model.Telefono)
                {
                    conflicto.Telefono = true;
                }
                if (!string.IsNullOrEmpty(domicilioDb) && domicilioDb == model.DomicilioLegal)
                {
                    conflicto.DomicilioLegal = true;
                }
            }

            return conflicto;
        }


        public ResultadoOperacion UpdateClient(ClienteModel model)
        {
            var resultado = new ResultadoOperacion();

            try
            {
                using var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
                connection.Open();

                string query = @"
            UPDATE Cliente
            SET 
                dni = @dni,
                nombre = @nombre,
                apellido = @apellido,
                email = @correo,
                telefono = @telefono,
                domicilioLegal = @domicilioLegal
            WHERE idCliente = @idCliente and eliminado = 0";

                using var command = new SqlCommand(query, connection);

                command.Parameters.AddWithValue("@idCliente", model.IdCliente);
                command.Parameters.AddWithValue("@dni", model.Dni ?? "");
                command.Parameters.AddWithValue("@nombre", model.Nombre ?? "");
                command.Parameters.AddWithValue("@apellido", model.Apellido ?? "");
                command.Parameters.AddWithValue("@correo", model.Email ?? "");
                command.Parameters.AddWithValue("@telefono", model.Telefono ?? "");
                command.Parameters.AddWithValue("@domicilioLegal", model.DomicilioLegal);

                int filasAfectadas = command.ExecuteNonQuery();

                if (filasAfectadas > 0)
                {
                    resultado.Exito = true;
                    resultado.Mensaje = "Cliente actualizado correctamente.";
                }
                else
                {
                    resultado.Exito = false;
                    resultado.Mensaje = "No se encontró el cliente o no se realizaron cambios.";
                }
            }
            catch (Exception ex)
            {
                resultado.Exito = false;
                resultado.Mensaje = $"Error al actualizar el cliente: {ex.Message}";
            }

            return resultado;
        }

    }
}