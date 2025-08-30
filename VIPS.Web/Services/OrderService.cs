using System.Data;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.ObjectPool;
using VIPS.Web.Models;

namespace VIPS.Web.Services
{
    public class OrderService
    {
        private readonly IConfiguration _configuration;

        public OrderService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /*public ResultadoOperacion CrearPedido(ClienteModel clienteModel)
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
        */


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


        public List<OrderViewModel> ObtenerPedidos(string columna, string orden)
        {
            try
            {
                var columnasPermitidas = new[] { "idPedido", "idCliente", "peso", "fechaCreacion", "fechaDespacho", "estadoPedido", "direccion"};
                if (!columnasPermitidas.Contains(columna))
                    throw new ArgumentException("Columna no válida");

                // Orden seguro
                var ordenSeguro = (orden?.ToUpper() == "DESC") ? "DESC" : "ASC";

                var query = $@"select p.idPedido, c.idCliente, p.peso, p.fechaCreacion, p.fechaDespacho, e.descripcion as estadoPedido, d.direccion from Pedido p inner join Cliente c on c.idCliente = p.idCliente inner join DomicilioEntrega d on d.idDomicilioEntrega = p.idDomicilioEntrega inner join EstadoPedido e on e.idEstado = p.idEstadoPedido order by {columna} {ordenSeguro}";

                using var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
                connection.Open();

                using var cmd = new SqlCommand(query, connection);
                using var adapter = new SqlDataAdapter(cmd);

                var dataTable = new DataTable();
                adapter.Fill(dataTable);

                var lista = new List<OrderViewModel>();

                foreach (DataRow row in dataTable.Rows)
                {

                    lista.Add(new OrderViewModel
                    {
                        IdPedido = Convert.ToInt32(row["idPedido"]),
                        IdCliente = Convert.ToInt32(row["idCliente"]),

                        Peso = Convert.ToDecimal(row["peso"]),

                        FechaCreacion = Convert.ToDateTime(row["fechaCreacion"]),
                        FechaDespacho = Convert.ToDateTime(row["fechaDespacho"]),

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
    }
}