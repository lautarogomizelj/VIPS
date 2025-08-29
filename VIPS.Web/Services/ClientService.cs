using System.Data;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.ObjectPool;
using VIPS.Web.Models;

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

                var query = $@"select idCliente, nombre, apellido, domicilioLegal, fechaCreacion from Cliente order by {columna} {ordenSeguro}";

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
            var query = "SELECT COUNT(1) FROM Cliente WHERE dni = @dni";
            using var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
            connection.Open();

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@dni", dni);

            var count = (int)command.ExecuteScalar();
            return count > 0;
        }

        public bool VerificarEmailExistente(string email)
        {
            var query = "SELECT COUNT(1) FROM Cliente WHERE email = @email";
            using var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
            connection.Open();

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@email", email);

            var count = (int)command.ExecuteScalar();
            return count > 0;
        }
    }
}