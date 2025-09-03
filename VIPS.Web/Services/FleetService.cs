using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;                  // Para CultureInfo
using System.IO;                             // Para MemoryStream
using System.Linq;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.ObjectPool;
using Microsoft.AspNetCore.Mvc.Rendering;    // Para SelectList
using VIPS.Web.Models;
using PdfSharpCore.Pdf;                       // Para PdfDocument
using PdfSharpCore.Drawing;                   // Para XGraphics, XFont, XPens, XBrushes
using System.Security.Claims;                 // Para ClaimsPrincipal si lo usás
using System.Threading.Tasks;                 // Para async Task

namespace VIPS.Web.Services
{
    public class FleetService
    {
        private readonly IConfiguration _configuration;

        public FleetService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public List<FleetViewModel> ObtenerFlota(string columna, string orden, string? patente = null)
        {
            try
            {
                var columnasPermitidas = new[] { "idCamion", "patente", "capacidadPeso", "capacidadVolumen", "fechaCreacion", "estado" };
                if (!columnasPermitidas.Contains(columna))
                    throw new ArgumentException("Columna no válida");

                // Orden seguro
                var ordenSeguro = (orden?.ToUpper() == "DESC") ? "DESC" : "ASC";

                var query = $@"select idCamion, patente, capacidadPeso, capacidadVolumen, fechaCreacion, estado from Flota where eliminado = 0";

                if (!string.IsNullOrEmpty(patente))
                {
                    query += $@" AND patente LIKE @patente order by {columna} {ordenSeguro}";
                }
                else
                {
                    query += $" order by {columna} {ordenSeguro}";

                }

                using var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
                connection.Open();

                using var cmd = new SqlCommand(query, connection);

                if (!string.IsNullOrEmpty(patente))
                {
                    // Búsqueda parcial
                    cmd.Parameters.AddWithValue("@patente", "%" + patente + "%");
                }

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
                        CapacidadPeso = DecimalAStr(Convert.ToDecimal(row["capacidadPeso"])),
                        CapacidadVolumen = DecimalAStr(Convert.ToDecimal(row["capacidadVolumen"])),
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


        private string DecimalAStr(decimal numero, int cantDecimales = 2)
        {
            var culture = new CultureInfo("es-AR");
            culture.NumberFormat.NumberDecimalSeparator = ",";
            culture.NumberFormat.NumberGroupSeparator = "."; // opcional, para separar miles

            return numero.ToString($"N{cantDecimales}", culture);
        }



        public FleetModel? retornarFleetModelConPatente(string patente)
        {
            try
            {
                var query = $@"select patente, ancho, largo, alto, capacidadPeso, capacidadVolumen, estado from Flota where patente = @patente and eliminado = 0";

                using var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
                connection.Open();

                using var cmd = new SqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@patente", patente);
                using var reader = cmd.ExecuteReader();

                if (reader.Read())
                {
                    return new FleetModel
                    {
                        Patente = reader["patente"].ToString(),
                        Ancho = DecimalAStr(Convert.ToDecimal(reader["ancho"])),
                        Largo = DecimalAStr(Convert.ToDecimal(reader["largo"])),
                        Alto = DecimalAStr(Convert.ToDecimal(reader["alto"])),
                        CapacidadPeso = DecimalAStr(Convert.ToDecimal(reader["capacidadPeso"])),
                        CapacidadVolumen = DecimalAStr(Convert.ToDecimal(reader["capacidadVolumen"])),
                        Estado = Convert.ToInt32(reader["estado"])

                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public ResultadoOperacion EliminarFleet(string patente)
        {
            try
            {
                using var conn = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));

                conn.Open();

                string query = @"UPDATE Flota SET eliminado = 1, fechaModificacion = GETDATE() WHERE patente = @patente";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@patente", patente);

                //devulve 1 si creo una fila
                int filas = cmd.ExecuteNonQuery();

                return new ResultadoOperacion
                {
                    Exito = filas > 0,
                    Mensaje = filas > 0
                ? "Vehiculo borrado correctamente"
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

        public ResultadoOperacion CrearFleet(FleetModel fleetModel)
        {
            decimal Ancho, Largo, Alto, CapacidadPeso, CapacidadVolumen;

            try
            {
                Ancho = ConvertirStringADecimal(fleetModel.Ancho, 2);
                Largo = ConvertirStringADecimal(fleetModel.Largo, 2);
                Alto = ConvertirStringADecimal(fleetModel.Alto, 2);
                CapacidadPeso = ConvertirStringADecimal(fleetModel.CapacidadPeso, 2);
                CapacidadVolumen = ConvertirStringADecimal(fleetModel.CapacidadVolumen, 2);
            }
            catch (FormatException ex)
            {
                return new ResultadoOperacion { Exito = false, Mensaje = ex.Message };
            }
            catch (ArgumentException ex)
            {
                return new ResultadoOperacion { Exito = false, Mensaje = ex.Message };
            }


            try
            {
                using var conn = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));

                conn.Open();

                string query = @"INSERT INTO Flota (patente, ancho, largo, alto, capacidadPeso, capacidadVolumen, estado) VALUES (@patente, @ancho, @largo, @alto, @capacidadPeso, @capacidadVolumen, @estado)";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@patente", fleetModel.Patente);
                cmd.Parameters.AddWithValue("@ancho", Ancho);
                cmd.Parameters.AddWithValue("@largo", Largo);
                cmd.Parameters.AddWithValue("@alto", Alto);
                cmd.Parameters.AddWithValue("@capacidadPeso", CapacidadPeso);
                cmd.Parameters.AddWithValue("@capacidadVolumen", CapacidadVolumen);
                cmd.Parameters.AddWithValue("@estado", fleetModel.Estado);


                //devulve 1 si creo una fila
                int filas = cmd.ExecuteNonQuery();

                return new ResultadoOperacion
                {
                    Exito = filas > 0,
                    Mensaje = filas > 0
                ? "Vehiculo creado correctamente"
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

        public ResultadoOperacion UpdateFleet(FleetModel model)
        {
            var resultado = new ResultadoOperacion();

            // Convertir los campos decimales que vienen como string

            decimal Ancho, Largo, Alto, CapacidadPeso, CapacidadVolumen;

            try
            {
                Ancho = ConvertirStringADecimal(model.Ancho, 2);
                Largo = ConvertirStringADecimal(model.Largo, 2);
                Alto = ConvertirStringADecimal(model.Alto, 2);
                CapacidadPeso = ConvertirStringADecimal(model.CapacidadPeso, 2);
                CapacidadVolumen = ConvertirStringADecimal(model.CapacidadVolumen, 2);
            }
            catch (FormatException ex)
            {
                return new ResultadoOperacion { Exito = false, Mensaje = ex.Message };
            }
            catch (ArgumentException ex)
            {
                return new ResultadoOperacion { Exito = false, Mensaje = ex.Message };
            }



            try
            {
                using var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
                connection.Open();

                string query = @"
        UPDATE Flota
        SET 
            ancho = @ancho,
            largo = @largo,
            alto = @alto,
            capacidadPeso = @capacidadPeso,
            capacidadVolumen = @capacidadVolumen,
            estado = @estado,
            fechaModificacion = GETDATE()
        WHERE patente = @patente AND eliminado = 0";

                using var command = new SqlCommand(query, connection);

                command.Parameters.AddWithValue("@patente", model.Patente);
                command.Parameters.AddWithValue("@ancho", Ancho);
                command.Parameters.AddWithValue("@largo", Largo);
                command.Parameters.AddWithValue("@alto", Alto);
                command.Parameters.AddWithValue("@capacidadPeso", CapacidadPeso);
                command.Parameters.AddWithValue("@capacidadVolumen", CapacidadVolumen);
                command.Parameters.AddWithValue("@estado", model.Estado);

                int filasAfectadas = command.ExecuteNonQuery();

                if (filasAfectadas > 0)
                {
                    resultado.Exito = true;
                    resultado.Mensaje = "Vehículo actualizado correctamente.";
                }
                else
                {
                    resultado.Exito = false;
                    resultado.Mensaje = "No se encontró el vehículo o no se realizaron cambios.";
                }
            }
            catch (Exception ex)
            {
                resultado.Exito = false;
                resultado.Mensaje = $"Error al actualizar el vehículo: {ex.Message}";
            }

            return resultado;
        }


        public byte[] ExportarFlotaPdf(string columna = "fechaCreacion", string orden = "desc", string? parametro = null)
        {
            // Traer la flota ordenada
            var flota = ObtenerFlota(columna, orden, parametro);

            using (var ms = new MemoryStream())
            {
                PdfDocument document = new PdfDocument();
                var page = document.AddPage();
                XGraphics gfx = XGraphics.FromPdfPage(page);
                XFont font = new XFont("Verdana", 10, XFontStyle.Regular);

                // Posiciones iniciales
                double startX = 40;
                double startY = 50;
                double rowHeight = 20;

                // Dibujar encabezado
                gfx.DrawString("ID Camión", font, XBrushes.Black, new XRect(startX, startY, 60, rowHeight), XStringFormats.Center);
                gfx.DrawString("Patente", font, XBrushes.Black, new XRect(startX + 60, startY, 80, rowHeight), XStringFormats.Center);
                gfx.DrawString("Cap. Peso", font, XBrushes.Black, new XRect(startX + 140, startY, 80, rowHeight), XStringFormats.Center);
                gfx.DrawString("Cap. Volumen", font, XBrushes.Black, new XRect(startX + 220, startY, 80, rowHeight), XStringFormats.Center);
                gfx.DrawString("Fecha Creación", font, XBrushes.Black, new XRect(startX + 300, startY, 100, rowHeight), XStringFormats.Center);
                gfx.DrawString("Estado", font, XBrushes.Black, new XRect(startX + 400, startY, 60, rowHeight), XStringFormats.Center);

                // Línea debajo del encabezado
                gfx.DrawLine(XPens.Black, startX, startY + rowHeight, startX + 460, startY + rowHeight);

                // Dibujar filas
                double y = startY + rowHeight;
                foreach (var camion in flota)
                {
                    y += rowHeight;
                    gfx.DrawString(camion.IdCamion.ToString(), font, XBrushes.Black, new XRect(startX, y, 60, rowHeight), XStringFormats.Center);
                    gfx.DrawString(camion.Patente, font, XBrushes.Black, new XRect(startX + 60, y, 80, rowHeight), XStringFormats.Center);
                    gfx.DrawString(camion.CapacidadPeso, font, XBrushes.Black, new XRect(startX + 140, y, 80, rowHeight), XStringFormats.Center);
                    gfx.DrawString(camion.CapacidadVolumen, font, XBrushes.Black, new XRect(startX + 220, y, 80, rowHeight), XStringFormats.Center);
                    gfx.DrawString(camion.FechaCreacion.ToString("dd/MM/yyyy"), font, XBrushes.Black, new XRect(startX + 300, y, 100, rowHeight), XStringFormats.Center);
                    gfx.DrawString(camion.Estado == 1 ? "Activo" : "Inactivo", font, XBrushes.Black, new XRect(startX + 400, y, 60, rowHeight), XStringFormats.Center);

                    // Línea debajo de la fila
                    gfx.DrawLine(XPens.Gray, startX, y + rowHeight, startX + 460, y + rowHeight);
                }

                // Guardar PDF en memoria
                document.Save(ms, false);
                return ms.ToArray();


            }
        }
    }
}
