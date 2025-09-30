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

        public List<UsuarioViewModel> ObtenerUsuarios(string columna, string orden, string? parametro = null)
        {
            try
            {
                var columnasPermitidas = new[] { "usuario", "nombre", "fechaCreacion", "fechaUltimoLogin", "fechaUltimoIntentoFallido" };
                if (!columnasPermitidas.Contains(columna))
                    throw new ArgumentException("Columna no válida");

                // Orden seguro
                var ordenSeguro = (orden?.ToUpper() == "DESC") ? "DESC" : "ASC";

                var query = $@"select u.usuario, r.nombre, u.fechaCreacion, u.fechaUltimoLogin, u.fechaUltimoIntentoFallido from Usuario u inner join Rol r on r.idRol = u.idRol WHERE eliminado = 0";

                if (!string.IsNullOrEmpty(parametro))
                {
                    query += $@" AND u.usuario LIKE @parametro order by {columna} {ordenSeguro}";
                }
                else
                {
                    query += $" order by {columna} {ordenSeguro}";

                }

                using var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
                connection.Open();

                using var cmd = new SqlCommand(query, connection);

                if (!string.IsNullOrEmpty(parametro))
                {
                    // Búsqueda parcial
                    cmd.Parameters.AddWithValue("@parametro", "%" + parametro + "%");
                }

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


        public UsuarioModelEdit? retornarUsuarioModelEditConIdUsuario(int idUsuario)
        {
            try
            {
                var query = $@"select idUsuario, usuario, dni, nombre, apellido, email, telefono, idRol from Usuario where idUsuario = @idUsuario";

                using var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
                connection.Open();

                using var cmd = new SqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@idUsuario", idUsuario);
                using var reader = cmd.ExecuteReader();

                if (reader.Read())
                {
                    return new UsuarioModelEdit
                    {
                        IdUsuario = Convert.ToInt32(reader["idUsuario"]),
                        Usuario = reader["usuario"].ToString(),
                        Dni = reader["dni"].ToString(),
                        Nombre = reader["nombre"].ToString(),
                        Apellido = reader["apellido"].ToString(),
                        Email = reader["email"].ToString(),
                        Telefono = reader["telefono"].ToString(),
                        IdRol = Convert.ToInt32(reader["idRol"])
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                return null;
            }
        }


        public ResultadoOperacion EliminarUsuario(String Usuario)
        {
            try
            {
                using var conn = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));

                conn.Open();

                string query = @"UPDATE Usuario SET eliminado = 1 WHERE usuario = @usuario";

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

     

        public bool VerificarDniExistente(string dni)
        {
            var query = "SELECT COUNT(1) FROM Usuario WHERE dni = @dni and eliminado = 0";
            using var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
            connection.OpenAsync();

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@dni", dni);

            var count = (int) command.ExecuteScalar();
            return count > 0;
        }

        public bool EsAdminGeneral(int idRol)
        {
            var query = "SELECT COUNT(1) FROM Rol WHERE IdRol = @idRol AND Nombre = 'Admin General'";

            using var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
            connection.Open();

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@idRol", idRol);

            var count = (int)command.ExecuteScalar();
            return count > 0;
        }

        public bool VerificarEmailExistente(string email)
        {
            var query = "SELECT COUNT(1) FROM Usuario WHERE email = @email and eliminado = 0";
            using var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
            connection.Open();

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@email", email);

            var count = (int) command.ExecuteScalar();
            return count > 0;
        }

        public bool VerificarUsuarioExistente(string usuario)
        {
            var query = "SELECT COUNT(1) FROM Usuario WHERE usuario = @usuario and eliminado = 0";
            using var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
            connection.Open();

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@usuario", usuario);

            var count = (int) command.ExecuteScalar();
            return count > 0;
        }


        public int RetornarIdUsuarioConEmail(string email)
        {
            string query = @"SELECT idUsuario FROM Usuario WHERE email = @correo and eliminado = 0";

            using var conn = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
            using var command = new SqlCommand(query, conn);
            command.Parameters.AddWithValue("@correo", email);

            conn.Open();
            object? result = command.ExecuteScalar();

            if (result != null && int.TryParse(result.ToString(), out int idUsuario))
            {
                return idUsuario;
            }

            return -1; // si no encuentra el usuario
        }

        public int RetornarIdUsuarioConUsuario(string usuario)
        {
            string query = @"SELECT idUsuario FROM Usuario WHERE usuario = @usuario and eliminado = 0";

            using var conn = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
            using var command = new SqlCommand(query, conn);
            command.Parameters.AddWithValue("@usuario", usuario);

            conn.Open();
            object? result = command.ExecuteScalar();

            if (result != null && int.TryParse(result.ToString(), out int idUsuario))
            {
                return idUsuario;
            }

            return -1; // si no encuentra el usuario
        }

        public int RetornarIdUsuarioConToken(string token)
        {
            string query = @"SELECT u.idUsuario FROM Usuario u inner join RecuperacionContrasenia r on r.idUsuario = u.idUsuario WHERE token = @token and eliminado = 0";

            using var conn = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
            using var command = new SqlCommand(query, conn);
            command.Parameters.AddWithValue("@token", token);

            conn.Open();
            object? result = command.ExecuteScalar();

            if (result != null && int.TryParse(result.ToString(), out int idUsuario))
            {
                return idUsuario;
            }

            return -1; // si no encuentra el usuario
        }

        public string? RetornarUsuarioConToken(string token)
        {
            string query = @"SELECT usuario FROM Usuario u inner join RecuperacionContrasenia r on r.idUsuario = u.idUsuario WHERE token = @token and eliminado = 0";

            using var conn = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
            using var command = new SqlCommand(query, conn);
            command.Parameters.AddWithValue("@token", token);

            conn.Open();
            object? result = command.ExecuteScalarAsync();

            return result?.ToString(); // devuelve el string o null si no encuentra nada
        }

        public string? RetornarUsuarioConEmail(string email)
        {
            string query = @"SELECT usuario FROM Usuario WHERE email = @email and eliminado = 0";

            using var conn = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
            using var command = new SqlCommand(query, conn);
            command.Parameters.AddWithValue("@email", email);

            conn.Open();
            object? result = command.ExecuteScalar();

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
            string query = @"UPDATE Usuario SET contraseniaHash = @contraseniaHash WHERE idUsuario = @idUsuario and eliminado = 0";

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

        public bool ValidarToken(string token)
        {
            string query = @"SELECT COUNT(*) FROM RecuperacionContrasenia WHERE token = @token AND fechaExpiracion > GETUTCDATE() AND usado = 0";

            using var conn = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
            using var command = new SqlCommand(query, conn);
            command.Parameters.AddWithValue("@token", token);

            conn.Open();
            int count = (int) command.ExecuteScalar();

            return count > 0; // true si token válido
        }

        public string? retornarContraseniaHashConUsuario(string username)
        {
             string query = @"SELECT contraseniaHash FROM Usuario WHERE usuario = @username and eliminado = 0";

            using var conn = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
            using var command = new SqlCommand(query, conn);
            command.Parameters.AddWithValue("@username", username);

            conn.Open();
            object? result = command.ExecuteScalar();

            return result?.ToString(); // devuelve el string o null si no encuentra nada
        }

        public ConflictoUsuario ExisteConflicto(UsuarioModelEdit model)
        {
            var conflicto = new ConflictoUsuario();

            // SQL: devolvemos flags 1/0 si hay conflicto en cada campo
            string query = @"
        SELECT 
             ISNULL(MAX(CASE WHEN dni = @dni THEN 1 ELSE 0 END), 0) AS ConflictoDni,
            ISNULL(MAX(CASE WHEN usuario = @usuario THEN 1 ELSE 0 END), 0) AS ConflictoUsuario,
            ISNULL(MAX(CASE WHEN email = @correo THEN 1 ELSE 0 END), 0) AS ConflictoEmail,
            ISNULL(MAX(CASE WHEN telefono = @telefono THEN 1 ELSE 0 END), 0) AS ConflictoTelefono
        FROM Usuario
        WHERE idUsuario <> @idUsuario
          AND (dni = @dni OR usuario = @usuario OR email = @correo OR telefono = @telefono and eliminado = 0);";

            using var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
            connection.Open();

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@idUsuario", model.IdUsuario);
            command.Parameters.AddWithValue("@dni", model.Dni ?? "");
            command.Parameters.AddWithValue("@usuario", model.Usuario ?? "");
            command.Parameters.AddWithValue("@correo", model.Email ?? "");
            command.Parameters.AddWithValue("@telefono", model.Telefono ?? "");

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                conflicto.Dni = reader.GetInt32(0) == 1;
                conflicto.Usuario = reader.GetInt32(1) == 1;
                conflicto.Email = reader.GetInt32(2) == 1;
                conflicto.Telefono = reader.GetInt32(3) == 1;
            }

            return conflicto;
        }

        public ResultadoOperacion UpdateUser(UsuarioModelEdit model)
        {
            var resultado = new ResultadoOperacion();

            try
            {
                using var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
                connection.Open();

                string query = @"
            UPDATE Usuario
            SET 
                usuario = @usuario,
                email = @correo,
                dni = @dni,
                telefono = @telefono,
                IdRol = @idRol,
                nombre = @nombre,
                apellido = @apellido,
                fechaModificacion = GETDATE()
            WHERE idUsuario = @idUsuario and eliminado = 0";

                using var command = new SqlCommand(query, connection);

                command.Parameters.AddWithValue("@usuario", model.Usuario ?? "");
                command.Parameters.AddWithValue("@correo", model.Email ?? "");
                command.Parameters.AddWithValue("@dni", model.Dni ?? "");
                command.Parameters.AddWithValue("@telefono", model.Telefono ?? "");
                command.Parameters.AddWithValue("@idRol", model.IdRol);
                command.Parameters.AddWithValue("@idUsuario", model.IdUsuario);
                command.Parameters.AddWithValue("@nombre", model.Nombre ?? "");
                command.Parameters.AddWithValue("@apellido", model.Apellido ?? "");

                int filasAfectadas = command.ExecuteNonQuery();

                if (filasAfectadas > 0)
                {
                    resultado.Exito = true;
                    resultado.Mensaje = "Usuario actualizado correctamente.";
                }
                else
                {
                    resultado.Exito = false;
                    resultado.Mensaje = "No se encontró el usuario o no se realizaron cambios.";
                }
            }
            catch (Exception ex)
            {
                resultado.Exito = false;
                resultado.Mensaje = $"Error al actualizar el usuario: {ex.Message}";
            }

            return resultado;
        }

        public ResultadoOperacion ActualizarIdioma(string username, string idioma)
        {
            try
            {
                using var conn = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
                conn.Open();

                string query = @"UPDATE Usuario 
                         SET idiomaInterfaz = @idioma 
                         WHERE usuario = @username and eliminado = 0";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@idioma", idioma);
                cmd.Parameters.AddWithValue("@username", username);

                int filasAfectadas = cmd.ExecuteNonQuery();

                if (filasAfectadas > 0)
                {
                    return new ResultadoOperacion
                    {
                        Exito = true,
                        Mensaje = "Idioma actualizado correctamente"
                    };
                }
                else
                {
                    return new ResultadoOperacion
                    {
                        Exito = false,
                        Mensaje = "Usuario no encontrado"
                    };
                }
            }
            catch (Exception ex)
            {
                return new ResultadoOperacion
                {
                    Exito = false,
                    Mensaje = ex.Message
                };
            }
        }



        public async Task<List<ConductorDisponible>> ObtenerConductoresDisponiblesAsync()
        {
            try
            {
                var conductores = new List<ConductorDisponible>();

                using var connection = new SqlConnection(_configuration.GetConnectionString("MainConnectionString"));
                await connection.OpenAsync();

                var query = "select idUsuario, nombre + ' ' + apellido as nombreCompleto  FROM Usuario WHERE eliminado = 0 and idRol = (select idRol from Rol where nombre = 'conductor') and idUsuario not in (select idUsuario from Ruta r inner join EstadoRuta er on er.idEstadoRuta = r.idEstadoRuta where er.descripcion NOT IN ('Cancelada','Finalizada') AND r.idUsuario IS NOT NULL ) ORDER BY nombre, apellido";

                using var cmd = new SqlCommand(query, connection);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    conductores.Add(new ConductorDisponible
                    {
                        IdConductor = reader.GetInt32(reader.GetOrdinal("idUsuario")),
                        Nombre = reader.GetString(reader.GetOrdinal("nombreCompleto")),
                    });
                }

                return conductores;
            }
            catch(Exception e)
            {
                return new List<ConductorDisponible>();
            }
        }





    }
}