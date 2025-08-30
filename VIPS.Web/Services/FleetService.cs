using System.Data;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.ObjectPool;
using VIPS.Web.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace VIPS.Web.Services
{
    public class FleetService
    {
        private readonly IConfiguration _configuration;

        public FleetService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public List<FleetViewModel> ObtenerFlota(string columna, string orden)
        {
            try
            {
                var columnasPermitidas = new[] { "idCamion", "patente", "capacidadPeso", "capacidadVolumen", "fechaCreacion", "estado" };
                if (!columnasPermitidas.Contains(columna))
                    throw new ArgumentException("Columna no válida");

                // Orden seguro
                var ordenSeguro = (orden?.ToUpper() == "DESC") ? "DESC" : "ASC";

                var query = $@"select idCamion, patente, capacidadPeso, capacidadVolumen, fechaCreacion, estado from Flota where eliminado = 0 order by {columna} {ordenSeguro}";

                using var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
                connection.Open();

                using var cmd = new SqlCommand(query, connection);
                using var adapter = new SqlDataAdapter(cmd);

                var dataTable = new DataTable();
                adapter.Fill(dataTable);

                var lista = new List<FleetViewModel>();

                foreach (DataRow row in dataTable.Rows)
                {

                    lista.Add(new FleetViewModel
                    {
                        IdCamion = Convert.ToInt32(row["idCamion"]),
                        Patente = row["patente"].ToString(),
                        CapacidadPeso = Convert.ToDecimal(row["capacidadPeso"]),
                        CapacidadVolumen = Convert.ToDecimal(row["capacidadVolumen"]),
                        FechaCreacion = Convert.ToDateTime(row["fechaCreacion"]),
                        Estado = Convert.ToInt32(row["estado"])

                    });
                }


                return lista;
            }
            catch (Exception ex)
            {
                return new List<FleetViewModel>();
            }
        }

      
    }
}