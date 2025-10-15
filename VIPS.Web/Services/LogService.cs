using System.Data;
using Microsoft.Data.SqlClient;
using VIPS.Web.Models;

namespace VIPS.Web.Services
{
    public class LogService
    {
        private readonly IConfiguration _configuration;

        public LogService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public List<LogViewModel> ObtenerLogs(int cantidadLogs, string columna, string orden, string? parametro = null)
        {
            try
            {
                var columnasPermitidas = new[] { "idLogActividad", "usuario", "FechaHora", "Accion", "Detalle" };
                if (!columnasPermitidas.Contains(columna))
                    throw new ArgumentException("Columna no válida");

                var ordenSeguro = (orden?.ToUpper() == "DESC") ? "DESC" : "ASC";

                var query = @"SELECT idLogActividad, usuario, FechaHora, Accion, Detalle
                      FROM LogActividad l
                      INNER JOIN Usuario u ON l.idUsuario = u.idUsuario";

                if (!string.IsNullOrWhiteSpace(parametro))
                {
                    query += " WHERE detalle LIKE @parametro";
                }

                query += $" ORDER BY {columna} {ordenSeguro} OFFSET 0 ROWS FETCH NEXT @cantidadLogs ROWS ONLY";


                using var conn = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
                conn.Open();

                using var cmd = new SqlCommand(query, conn);

                cmd.Parameters.AddWithValue("@cantidadLogs", cantidadLogs);

                if (!string.IsNullOrWhiteSpace(parametro))
                {
                    cmd.Parameters.AddWithValue("@parametro", "%" + parametro + "%");
                }


                using var adapter = new SqlDataAdapter(cmd);
                var dataTable = new DataTable();
                adapter.Fill(dataTable);


                var lista = new List<LogViewModel>();
                foreach (DataRow row in dataTable.Rows)
                {
                    lista.Add(new LogViewModel
                    {
                        idLogActividad = row["idLogActividad"].ToString(),
                        Usuario = row["usuario"].ToString(),
                        FechaHora = Convert.ToDateTime(row["FechaHora"]),
                        Accion = row["Accion"].ToString(),
                        Detalle = row["Detalle"] == DBNull.Value ? null : row["Detalle"].ToString()
                    });
                }

                return lista;
            }
            catch (Exception ex)
            {
                // Podés loguear ex.Message si querés depurar
                return new List<LogViewModel>();
            }
        }


        public void AgregarLog(string username, DateTime fechaHora, string accion, string detalle, string ip)
        {
            try
            {
                ip = (ip == "::1") ? "127.0.0.1" : ip;

                using (SqlConnection conn = new SqlConnection(_configuration.GetConnectionString("MainConnectionString")))
                {
                    conn.Open();

                    using (SqlCommand cmd = new SqlCommand("sp_agregarLog", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@username", username ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@fecha_hora", fechaHora);
                        cmd.Parameters.AddWithValue("@accion", accion);
                        cmd.Parameters.AddWithValue("@detalle", detalle);
                        cmd.Parameters.AddWithValue("@ip", ip);

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (SqlException sqlEx) when (sqlEx.Number == 50000)
            {
                // Error personalizado del RAISERROR (usuario no encontrado)
                Console.WriteLine($"Usuario no encontrado para log: {username}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error agregando log: {ex.Message}");
            }
        }



    }
}