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
                        Ancho = Convert.ToDecimal(reader["ancho"]),
                        Largo = Convert.ToDecimal(reader["largo"]),     
                        Alto = Convert.ToDecimal(reader["alto"]),
                        CapacidadPeso = Convert.ToDecimal(reader["capacidadPeso"]),
                        CapacidadVolumen = Convert.ToDecimal(reader["capacidadVolumen"]),
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

                string query = @"UPDATE Flota SET eliminado = 1 WHERE patente = @patente";

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
            try
            {
                using var conn = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));

                conn.Open();

                string query = @"INSERT INTO Flota (patente, ancho, largo, alto, capacidadPeso, capacidadVolumen, estado) VALUES (@patente, @ancho, @largo, @alto, @capacidadPeso, @capacidadVolumen, @estado)";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@patente", fleetModel.Patente);
                cmd.Parameters.AddWithValue("@ancho", fleetModel.Ancho);
                cmd.Parameters.AddWithValue("@largo", fleetModel.Largo);
                cmd.Parameters.AddWithValue("@alto", fleetModel.Alto);
                cmd.Parameters.AddWithValue("@capacidadPeso", fleetModel.CapacidadPeso);
                cmd.Parameters.AddWithValue("@capacidadVolumen", fleetModel.CapacidadVolumen);
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

        public ConflictoFlota ExisteConflicto(FleetModel model)
        {
            var conflicto = new ConflictoFlota();

            string query = @"
select patente, ancho, largo, alto, capacidadPeso, capacidadVolumen, estado
FROM Flota
WHERE Patente <> @Patente
  AND eliminado = 0
  AND (Patente = @Patente 
       OR Ancho = @Ancho 
       OR Largo = @Largo 
       OR Alto = @Alto 
       OR CapacidadPeso = @CapacidadPeso 
       OR CapacidadVolumen = @CapacidadVolumen 
       OR Estado = @Estado);";

            using var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
            connection.Open();

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@Patente", model.Patente ?? "");
            command.Parameters.AddWithValue("@Ancho", model.Ancho);
            command.Parameters.AddWithValue("@Largo", model.Largo);
            command.Parameters.AddWithValue("@Alto", model.Alto);
            command.Parameters.AddWithValue("@CapacidadPeso", model.CapacidadPeso);
            command.Parameters.AddWithValue("@CapacidadVolumen", model.CapacidadVolumen);
            command.Parameters.AddWithValue("@Estado", model.Estado);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var patenteDb = reader["patente"] as string;
                var anchoDb = Convert.ToDecimal(reader["ancho"]);
                var largoDb = Convert.ToDecimal(reader["largo"]);
                var altoDb = Convert.ToDecimal(reader["alto"]);
                var capacidadPesoDb = Convert.ToDecimal(reader["capacidadPeso"]);
                var capacidadVolumenDb = Convert.ToDecimal(reader["capacidadVolumen"]);
                var estadoDb = Convert.ToInt32(reader["estado"]);

                if (!string.IsNullOrEmpty(patenteDb) && patenteDb == model.Patente)
                {
                    conflicto.Patente = true;
                }
                if (anchoDb == model.Ancho)
                {
                    conflicto.Ancho = true;
                }
                if (largoDb == model.Largo)
                {
                    conflicto.Largo = true;
                }
                if (altoDb == model.Alto)
                {
                    conflicto.Alto = true;
                }
                if (capacidadPesoDb == model.CapacidadPeso)
                {
                    conflicto.CapacidadPeso = true;
                }
                if (capacidadVolumenDb == model.CapacidadVolumen)
                {
                    conflicto.CapacidadVolumen = true;
                }
                if (estadoDb == model.Estado)
                {
                    conflicto.Estado = true;
                }
            }

            return conflicto;
        }


        public ResultadoOperacion UpdateFleet(FleetModel model)
        {
            var resultado = new ResultadoOperacion();

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
            estado = @estado
        WHERE patente = @patente AND eliminado = 0";

                using var command = new SqlCommand(query, connection);

                command.Parameters.AddWithValue("@patente", model.Patente);
                command.Parameters.AddWithValue("@ancho", model.Ancho);
                command.Parameters.AddWithValue("@largo", model.Largo);
                command.Parameters.AddWithValue("@alto", model.Alto);
                command.Parameters.AddWithValue("@capacidadPeso", model.CapacidadPeso);
                command.Parameters.AddWithValue("@capacidadVolumen", model.CapacidadVolumen);
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


    }
}
