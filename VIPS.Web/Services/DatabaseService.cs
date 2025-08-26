using Microsoft.Data.SqlClient;
using System.Data;

namespace VIPS.Web.Services
{
    public class DatabaseService
    {
        private readonly IConfiguration _configuration;

        public DatabaseService(IConfiguration configuration)
        {
            _configuration = configuration;
        }


        // Nuevo método que devuelve la conexión abierta
        private async Task<SqlConnection> GetOpenConnectionAsync()
        {
            var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
            await connection.OpenAsync();
            return connection;
        }

        public async Task<DataSet> DevolverUsuariosAsync()
        {
            using var connection = await GetOpenConnectionAsync();
            using var command = new SqlCommand("SELECT * FROM Usuario", connection);
            using var adapter = new SqlDataAdapter(command);

            var dataSet = new DataSet();
            adapter.Fill(dataSet, "Usuario");

            return dataSet;
        }

        /*

        public async Task<DataTable> ExecuteQueryAsync(string query, params SqlParameter[] parameters)
        {
            using var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
            await connection.OpenAsync();

            using var command = new SqlCommand(query, connection);
            if (parameters != null && parameters.Length > 0)
                command.Parameters.AddRange(parameters);

            using var adapter = new SqlDataAdapter(command);
            var dataTable = new DataTable();
            adapter.Fill(dataTable);

            return dataTable;
        }

        public async Task<int> ExecuteNonQueryAsync(string query, params SqlParameter[] parameters)
        {
            using var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
            await connection.OpenAsync();

            using var command = new SqlCommand(query, connection);
            if (parameters != null && parameters.Length > 0)
                command.Parameters.AddRange(parameters);

            return await command.ExecuteNonQueryAsync();
        }

        */
    }
}