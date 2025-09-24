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
    public class RouteService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        // URL de VROOM en la VM o host remoto
        private const string VroomUrl = "http://192.168.68.117:3000/solve";

        public RouteService(HttpClient httpClient, IConfiguration configuration)
        {
            _configuration = configuration;
            _httpClient = httpClient;
        }

        public List<RouteViewModel> ObtenerRutas(string columna, string orden, string? parametro = null)
        {
            try
            {
                var columnasPermitidas = new[] { "idRuta", "patente", "r.fechaCreacion", "cantidadPedidos", "estado" };
                if (!columnasPermitidas.Contains(columna))
                    throw new ArgumentException("Columna no válida");



                // Orden seguro
                var ordenSeguro = (orden?.ToUpper() == "DESC") ? "DESC" : "ASC";

                var query = $@"select idRuta, patente, r.fechaCreacion, count(*) as cantidadPedidos,estado, f.idCamion from Ruta r inner join Flota f on r.idCamion = f.idCamion where r.eliminado = 0 group by idRuta, patente, r.fechaCreacion, estado, f.idCamion order by {columna} {ordenSeguro}";


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
                        IdCamion = Convert.ToInt32(row["idCamion"])

                    });
                }


                return lista;
            }
            catch (Exception ex)
            {
                return new List<RouteViewModel>();
            }
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
    }
}
