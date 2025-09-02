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

        public async Task<(double lat, double lon, string postalCode)> GeocodeAsync(string direccion)
        {
            string url = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(direccion)}&format=json&addressdetails=1";

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


        public byte[] ExportarPedidosPdf(string columna = "fechaCreacion", string orden = "desc")
        {
            // Traer los pedidos ordenados
            var pedidos = ObtenerPedidos(columna, orden);

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
                gfx.DrawString("ID Cliente", font, XBrushes.Black, new XRect(startX + 60, startY, 60, rowHeight), XStringFormats.Center);
                gfx.DrawString("Peso", font, XBrushes.Black, new XRect(startX + 120, startY, 60, rowHeight), XStringFormats.Center);
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
                    gfx.DrawString(pedido.IdCliente.ToString(), font, XBrushes.Black, new XRect(startX + 60, y, 60, rowHeight), XStringFormats.Center);
                    gfx.DrawString(pedido.Peso.ToString("N2"), font, XBrushes.Black, new XRect(startX + 120, y, 60, rowHeight), XStringFormats.Center);
                    gfx.DrawString(pedido.FechaCreacion.ToString("dd/MM/yyyy"), font, XBrushes.Black, new XRect(startX + 180, y, 80, rowHeight), XStringFormats.Center);
                    gfx.DrawString(pedido.FechaDespacho.ToString("dd/MM/yyyy"), font, XBrushes.Black, new XRect(startX + 260, y, 80, rowHeight), XStringFormats.Center);
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