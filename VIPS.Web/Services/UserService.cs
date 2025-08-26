using System.Data;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.ObjectPool;
using VIPS.Web.Models;

namespace VIPS.Web.Services
{
    public class UserService
    {
        private readonly IConfiguration _configuration;

        public UserService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public ResultadoOperacion CrearUsuario(UsuarioModel usuarioModel, String contraseniaHash)
        {
            try
            {
                using var conn = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));

                conn.Open();

                string query = @"INSERT INTO Usuario (dni, nombre, apellido, email, telefono, telefonoEmergencia, contraseniaHash, IdRol, usuario) VALUES (@dni, @nombre, @apellido, @email, @telefono, @telefonoEmergencia, @contraseniaHash, @idRol, @usuario)";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@dni", usuarioModel.Dni);
                cmd.Parameters.AddWithValue("@nombre", usuarioModel.Nombre);
                cmd.Parameters.AddWithValue("@apellido", usuarioModel.Apellido);
                cmd.Parameters.AddWithValue("@email", usuarioModel.Email);
                cmd.Parameters.AddWithValue("@telefono", usuarioModel.Telefono);

                // Revisar si es null
                cmd.Parameters.AddWithValue("@telefonoEmergencia",
                    (object)usuarioModel.TelefonoEmergencia ?? DBNull.Value);

                cmd.Parameters.AddWithValue("@contraseniaHash", contraseniaHash);
                cmd.Parameters.AddWithValue("@idRol", usuarioModel.IdRol);
                cmd.Parameters.AddWithValue("@usuario", usuarioModel.Usuario);

                //devulve 1 si creo una fila
                int filas = cmd.ExecuteNonQuery();

                return new ResultadoOperacion
                {
                    Exito = filas > 0,
                    Mensaje = filas > 0
                ? "Usuario creado correctamente"
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

        public List<RolViewModel> ObtenerRoles()
        {
            try
            {
                var lista = new List<RolViewModel>();
                using var conn = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
                conn.Open();

                var query = "SELECT idRol, nombre FROM Rol WHERE idRol NOT IN (1) ORDER BY nombre";

                using var cmd = new SqlCommand(query, conn);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    lista.Add(new RolViewModel
                    {
                        IdRol = (int)reader["IdRol"],
                        Nombre = reader["Nombre"].ToString()
                    });
                }

                return lista;
            }
            catch (Exception ex)
            {
                return new List<RolViewModel>();
            }
        }

        public List<UsuarioViewModel> ObtenerUsuarios(string columna, string orden)
        {
            try
            {
                var columnasPermitidas = new[] { "usuario", "nombre", "fechaCreacion", "fechaUltimoLogin", "fechaUltimoIntentoFallido" };
                if (!columnasPermitidas.Contains(columna))
                    throw new ArgumentException("Columna no válida");

                // Orden seguro
                var ordenSeguro = (orden?.ToUpper() == "DESC") ? "DESC" : "ASC";

                var query = $@"select u.usuario, r.nombre, u.fechaCreacion, u.fechaUltimoLogin, u.fechaUltimoIntentoFallido from Usuario u inner join Rol r on r.idRol = u.idRol order by {columna} {ordenSeguro}";

                using var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
                connection.Open();

                using var cmd = new SqlCommand(query, connection);
                using var adapter = new SqlDataAdapter(cmd);

                var dataTable = new DataTable();
                adapter.Fill(dataTable);

                var listaUsuarios = new List<UsuarioViewModel>();

                foreach (DataRow row in dataTable.Rows)
                {

                    listaUsuarios.Add(new UsuarioViewModel
                    {
                        Usuario = row["usuario"].ToString(),
                        Rol = row["nombre"].ToString(),
                        FechaAlta = Convert.ToDateTime(row["fechaCreacion"]),
                        FechaUltimoAcceso = row["fechaUltimoLogin"] == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(row["fechaUltimoLogin"]),
                        FechaUltimoAccesoFallido = row["fechaUltimoIntentoFallido"] == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(row["fechaUltimoIntentoFallido"])

                    });
                }


                return listaUsuarios;
            }
            catch (Exception ex)
            {
                return new List<UsuarioViewModel>();
            }
        }

        public ResultadoOperacion EliminarUsuario(String Usuario)
        {
            try
            {
                using var conn = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));

                conn.Open();

                string query = @"DELETE FROM Usuario WHERE usuario = @usuario)";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@usuario", Usuario);

                //devulve 1 si creo una fila
                int filas = cmd.ExecuteNonQuery();

                return new ResultadoOperacion
                {
                    Exito = filas > 0,
                    Mensaje = filas > 0
                ? "Usuario borrado correctamente"
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

        /*public void ModificarUsuario(UsuarioModel usuario)
        {
            try
            {
                using var conn = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));

                conn.Open();

                string query = @"UPDATE Usuario SET dni = @dni, nombre = @nombre, apellido = @apellido, email = @email, telefono = @telefono, telefonoEmergencia = @telefonoEmergencia, contraseniaHash = @contraseniaHash, IdRol = @idRol, usuario = @usuario";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@dni", usuario.Dni);
                cmd.Parameters.AddWithValue("@nombre", usuario.Nombre);
                cmd.Parameters.AddWithValue("@apellido", usuario.Apellido);
                cmd.Parameters.AddWithValue("@email", usuario.Email);
                cmd.Parameters.AddWithValue("@telefono", usuario.Telefono);

                // Revisar si es null
                cmd.Parameters.AddWithValue("@telefonoEmergencia",
                    (object)usuario.TelefonoEmergencia ?? DBNull.Value);

                cmd.Parameters.AddWithValue("@contraseniaHash", contraseniaHash);
                cmd.Parameters.AddWithValue("@idRol", usuario.IdRol);
                cmd.Parameters.AddWithValue("@usuario", usuario.Usuario);

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@usuario", Usuario);

                //devulve 1 si creo una fila
                int filas = cmd.ExecuteNonQuery();

                return new ResultadoOperacion
                {
                    Exito = filas > 0,
                    Mensaje = filas > 0
                ? "Usuario modificado correctamente"
                : "No se modifico ningún registro"
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
        }*/

        public async Task<bool> VerificarDniExistente(string dni)
        {
            var query = "SELECT COUNT(1) FROM Usuario WHERE dni = @dni";
            using var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
            await connection.OpenAsync();

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@dni", dni);

            var count = (int)await command.ExecuteScalarAsync();
            return count > 0;
        }

        public async Task<bool> VerificarEmailExistente(string email)
        {
            var query = "SELECT COUNT(1) FROM Usuario WHERE email = @email";
            using var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
            await connection.OpenAsync();

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@email", email);

            var count = (int)await command.ExecuteScalarAsync();
            return count > 0;
        }

        public async Task<bool> VerificarUsuarioExistente(string usuario)
        {
            var query = "SELECT COUNT(1) FROM Usuario WHERE usuario = @usuario";
            using var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
            await connection.OpenAsync();

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@usuario", usuario);

            var count = (int)await command.ExecuteScalarAsync();
            return count > 0;
        }


        public async Task<int?> RetornarIdUsuarioConEmail(string email)
        {
            string query = @"SELECT Id FROM Usuario WHERE email = @correo";

            using var conn = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
            using var command = new SqlCommand(query, conn);
            command.Parameters.AddWithValue("@correo", email);

            await conn.OpenAsync();
            object? result = await command.ExecuteScalarAsync();

            if (result != null && int.TryParse(result.ToString(), out int idUsuario))
            {
                return idUsuario;
            }

            return null; // si no encuentra el usuario
        }

        public async Task<int?> RetornarIdUsuarioConToken(string token)
        {
            string query = @"SELECT idUsuario FROM Usuario WHERE token = @token";

            using var conn = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
            using var command = new SqlCommand(query, conn);
            command.Parameters.AddWithValue("@token", token);

            await conn.OpenAsync();
            object? result = await command.ExecuteScalarAsync();

            if (result != null && int.TryParse(result.ToString(), out int idUsuario))
            {
                return idUsuario;
            }

            return null; // si no encuentra el usuario
        }

        public async Task<string?> RetornarUsuarioConToken(string token)
        {
            string query = @"SELECT usuario FROM Usuario u inner join RecuperacionContrasenia r on r.idUsuario = u.idUsuario WHERE token = @token";

            using var conn = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
            using var command = new SqlCommand(query, conn);
            command.Parameters.AddWithValue("@token", token);

            await conn.OpenAsync();
            object? result = await command.ExecuteScalarAsync();

            return result?.ToString(); // devuelve el string o null si no encuentra nada
        }


        public async Task GuardarTokenRestablecimiento(int idUsuario, string token, DateTime expiracion)
        {
            string query = @"INSERT INTO RecuperacionContrasenia (idUsuario, token, fechaExpiracion) VALUES (@idUsuario, @token, @fechaExpiracion)";

            using var conn = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
            using var command = new SqlCommand(query, conn);
            command.Parameters.AddWithValue("@idUsuario", idUsuario);
            command.Parameters.AddWithValue("@token", token);
            command.Parameters.AddWithValue("@fechaExpiracion", expiracion);

            await conn.OpenAsync();
            await command.ExecuteNonQueryAsync();
        }

        public async Task ActualizarPassword(int idUsuario, string contraseniaHash)
        {
            string query = @"UPDATE Usuario SET contraseniaHash = @contraseniaHash WHERE idUsuario = @idUsuario";

            using var conn = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
            using var command = new SqlCommand(query, conn);
            command.Parameters.AddWithValue("@contraseniaHash", contraseniaHash);
            command.Parameters.AddWithValue("@idUsuario", idUsuario);

            await conn.OpenAsync();
            await command.ExecuteNonQueryAsync();
        }

        public async Task EliminarToken(string token)
        {
            string query = @"DELETE FROM RecuperacionContrasenia WHERE token = @token";

            using var conn = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
            using var command = new SqlCommand(query, conn);
            command.Parameters.AddWithValue("@token", token);

            await conn.OpenAsync();
            await command.ExecuteNonQueryAsync();
        }

        public async Task<bool> ValidarToken(string token)
        {
            string query = @"SELECT COUNT(*) FROM RecuperacionContrasenia WHERE token = @token AND fechaExpiracion > GETUTCDATE()";

            using var conn = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
            using var command = new SqlCommand(query, conn);
            command.Parameters.AddWithValue("@token", token);

            await conn.OpenAsync();
            int count = (int)await command.ExecuteScalarAsync();

            return count > 0; // true si token válido
        }



    }
}