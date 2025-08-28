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

    }
}