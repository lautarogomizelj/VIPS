using System.ComponentModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using VIPS.Web.Attributes;
using Microsoft.Data.SqlClient;
using VIPS.Web.Services;
using System.Security.Claims; 
using System.Data;

namespace VIPS.Web.Controllers
{
    public class AuthController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IHashService _hashService;
        private readonly UserService _userService;
        private readonly EmailService _emailService;
        private readonly LogService _logService;



        private readonly string _connectionString;

        public AuthController(IConfiguration configuration, IHashService hashService, UserService userService, EmailService emailService, LogService logService)
        {
            _configuration = configuration;
            _hashService = hashService;
            _userService = userService;
            _emailService = emailService;
            _logService = logService;
            _connectionString = _configuration.GetConnectionString("MainConnectionString");
        }

        public IActionResult Login()
        {
            // Verificar si hay mensajes en TempData para mostrar
            if (TempData["ErrorMessage"] != null)
            {
                ViewBag.ErrorMessage = TempData["ErrorMessage"];
            }
            if (TempData["SuccessMessage"] != null)
            {
                ViewBag.SuccessMessage = TempData["SuccessMessage"];
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password)
        {
            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "IP desconocida";
            bool loginExitoso = false;
            string rol = string.Empty;

            try
            {
                // Verificar intentos fallidos previos
                var puedeIntentarResult = await PuedeIntentarLogin(username);
                if (!puedeIntentarResult.PuedeIntentar)
                {
                    _logService.AgregarLog(username, DateTime.Now, "Login", "Intento de login bloqueado - Límite de intentos excedido", ipAddress);
                    TempData["ErrorMessage"] = puedeIntentarResult.Mensaje;
                    return RedirectToAction("Login");
                }



                string query = @"SELECT u.idUsuario, u.usuario, r.nombre as rol, u.contraseniaHash, 
                                u.fechaUltimoIntentoFallido, u.intentosFallidosLogin, u.idiomaInterfaz
                                FROM Usuario u 
                                INNER JOIN Rol r ON r.idRol = u.idRol 
                                WHERE u.usuario = @usuario";

                using var conn = new SqlConnection(_connectionString);
                using var command = new SqlCommand(query, conn);
                command.Parameters.AddWithValue("@usuario", username);

                await conn.OpenAsync();

                using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    string hashFromDb = reader["contraseniaHash"].ToString();
                    rol = reader["rol"].ToString();
                    string idiomaInterfaz = reader["idiomaInterfaz"].ToString();

                    if (_hashService.VerifyPassword(password, hashFromDb))
                    {

                        // Login exitoso
                        loginExitoso = true;

                        // Resetear intentos fallidos y actualizar fechas
                        await ActualizarUsuarioLoginExitoso(username);

                        // Crear claims y cookie de autenticación
                        var claims = new List<Claim>
                        {
                            new Claim(ClaimTypes.Name, username),
                            new Claim(ClaimTypes.Role, rol),
                            new Claim("Lang", idiomaInterfaz),
                        };

                        var claimsIdentity = new ClaimsIdentity(
                            claims, CookieAuthenticationDefaults.AuthenticationScheme);

                        var authProperties = new AuthenticationProperties
                        {
                            IsPersistent = true,
                            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1)
                        };

                        await HttpContext.SignInAsync(
                            CookieAuthenticationDefaults.AuthenticationScheme,
                            new ClaimsPrincipal(claimsIdentity),
                            authProperties);

                        // Agregar log de éxito
                        _logService.AgregarLog(username, DateTime.Now, "Login", "Inicio de sesión exitoso", ipAddress);


                        // Redirigir según rol
                        return rol switch
                        {
                            "adminGeneral" => RedirectToAction("Index", "AdminGeneral"),
                            "adminLogistico" => RedirectToAction("Index", "AdminLogistico"),
                            "adminVentas" => RedirectToAction("Index", "AdminVentas"),
                            _ => RedirectToAction("Login", "Auth")
                        };
                    }
                }

                // Login fallido
                await ActualizarUsuarioLoginFallido(username);
                _logService.AgregarLog(username, DateTime.Now, "Login", "Intento de inicio de sesión fallido", ipAddress);


                // Obtener intentos restantes
                var intentosInfo = await ObtenerIntentosRestantes(username);
                if (intentosInfo.IntentosRestantes == 0)
                {
                    TempData["ErrorMessage"] = "¡Has superado el límite de intentos! Debes esperar 4 horas o contactar al administrador.";
                }
                else
                {
                    TempData["ErrorMessage"] = $"Usuario o contraseña incorrectos. Te quedan {intentosInfo.IntentosRestantes} intentos.";
                }
            }
            catch (Exception ex)
            {
                _logService.AgregarLog(username, DateTime.Now, "Login", $"Error durante el login: {ex.Message}", ipAddress);
                TempData["ErrorMessage"] = "Error interno del sistema. Intente nuevamente.";

            }

            return RedirectToAction("Login");
        }


        public IActionResult RestorePassword()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> RestorePassword(string correo)
        {
            if (!string.IsNullOrEmpty(correo))
            {
                // 1. Validar que el correo exista
                bool correoValido = _userService.VerificarEmailExistente(correo);


                // 2. Para seguridad, no revelamos si existe o no
                // Mostramos el mismo mensaje siempre
                TempData["Message"] = "Si el correo está registrado, recibirás un enlace de recuperación.";

                if (correoValido)
                {
                    // 3. Generar token seguro
                    string token = Guid.NewGuid().ToString();

                    // 4. Guardar token y fecha de expiración en la base de datos
                    _userService.GuardarTokenRestablecimiento(_userService.RetornarIdUsuarioConEmail(correo), token, DateTime.UtcNow.AddMinutes(60));

                    // 5. Enviar correo con enlace
                    string enlace = Url.Action("ResetPassword", "Auth", new { token = token }, Request.Scheme);

                    // 6. Enviar correo
                    string cuerpo = $@"
                        <p>Hola,</p>
                        <p>Hemos recibido una solicitud para restablecer tu contraseña.</p>
                        <p>Haz clic en el siguiente enlace para cambiar tu contraseña:</p>
                        <p><a href='{enlace}'>{enlace}</a></p>
                        <p>Si no solicitaste esto, ignora este correo. El enlace expirará en 60 minutos.</p>";

                    bool conf = await _emailService.EnviarCorreo(correo, "Recuperación de Contraseña", cuerpo);

                    string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "IP desconocida";
                    _logService.AgregarLog(_userService.RetornarUsuarioConEmail(correo), DateTime.Now, "Recuperar contraseña", "Envio de token para la recuperacion de contraseña", ipAddress);
                }
            }

            return View("RestorePassword");

        }

        //validar que el token exista, aunque tenga validaciones de logica, si el token no es valido que ni entre a la pagina
        public IActionResult ResetPassword(string token)
        {
            if (string.IsNullOrEmpty(token) || !_userService.ValidarToken(token))
            {
                return RedirectToAction("Login", "Auth");
            }

            // podés pasar el token a la vista
            return View(model: token);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(string token, string nuevaPassword, string confirmarPassword)
        {
            if (string.IsNullOrEmpty(token) || !_userService.ValidarToken(token))
            {
                return RedirectToAction("Login", "Auth");
            }

            if (nuevaPassword != confirmarPassword)
            {
                TempData["ErrorMessage"] = "Las contraseñas no coinciden.";
                return View();
            }

            // 1️ Validar token y obtener idUsuario
            int idUsuario = _userService.RetornarIdUsuarioConToken(token);
            if (idUsuario == -1)
            {
                TempData["ErrorMessage"] = "Token inválido o expirado.";
                return View();
            }


            string contraseniaHash = _hashService.HashPassword(nuevaPassword); 

            // 3️ Actualizar contraseña en la tabla Usuario
            await _userService.ActualizarPassword(idUsuario, contraseniaHash);

            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "IP desconocida";
            _logService.AgregarLog(_userService.RetornarUsuarioConToken(token), DateTime.Now, "Recuperar contraseña", "Contraseña cambiada", ipAddress);

            // 4️Eliminar token usado
            await _userService.EliminarToken(token);


            return RedirectToAction("Login", "Auth");
        }


        public IActionResult ChangePasswordAccount()
        {
            return View();
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePasswordAccount(string passwordActual, string nuevaPassword, string confirmarPassword)
        {
            if (string.IsNullOrEmpty(passwordActual) || string.IsNullOrEmpty(nuevaPassword) || string.IsNullOrEmpty(confirmarPassword))
            {
                TempData["ErrorMessageChange"] = "Completa los inputs.";
                return View();
            }
           
            if (nuevaPassword != confirmarPassword)
            {
                TempData["ErrorMessageChange"] = "Las contraseñas no coinciden.";
                return View();
            }


            string usuario = User.FindFirstValue(ClaimTypes.Name);

            if (!_hashService.VerifyPassword(passwordActual, _userService.retornarContraseniaHashConUsuario(usuario)))
            {
                TempData["ErrorMessageChange"] = "La contraseña actual es incorrecta.";
                return View();
            }

            var hash = _hashService.HashPassword(nuevaPassword);
            await _userService.ActualizarPassword(_userService.RetornarIdUsuarioConUsuario(usuario), hash);

            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "IP desconocida";
            _logService.AgregarLog(usuario, DateTime.Now, "Cambio de contraseña", "Contraseña cambiada correctamente", ipAddress);

            TempData["SuccessMessageChange"] = "Tu contraseña ha sido cambiada con éxito.";

            return RedirectToAction("ReturnToPanel", "Auth");
           
        }

        [Authorize]
        public IActionResult ReturnToPanel()
        { 
            if (User.IsInRole("adminGeneral"))
                return RedirectToAction("MyAccount", "AdminGeneral");

            if (User.IsInRole("adminLogistico"))
                return RedirectToAction("MyAccount", "AdminLogistico");

            if (User.IsInRole("adminVentas"))
                return RedirectToAction("MyAccount", "AdminVentas");

            // fallback
            return RedirectToAction("Login", "Auth");
        }





        public async Task<IActionResult> Logout()
        {

            var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "IP desconocida";
            _logService.AgregarLog(nombreUsuario, DateTime.Now, "Logout", "Logout exitoso", ipAddress);

            Response.Cookies.Delete("Lang");

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }


        private async Task<(bool PuedeIntentar, string Mensaje)> PuedeIntentarLogin(string username)
        {
            string query = @"SELECT intentosFallidosLogin, fechaUltimoIntentoFallido 
                            FROM Usuario 
                            WHERE usuario = @usuario";

            using var conn = new SqlConnection(_connectionString);
            using var command = new SqlCommand(query, conn);
            command.Parameters.AddWithValue("@usuario", username);

            await conn.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                int intentosFallidos = reader.GetInt32(0);
                DateTime? ultimoIntentoFallido = reader.IsDBNull(1) ? null : reader.GetDateTime(1);

                // Si tiene 3 intentos fallidos, verificar si han pasado más de 4 horas
                if (intentosFallidos >= 3 && ultimoIntentoFallido.HasValue)
                {
                    TimeSpan tiempoTranscurrido = DateTime.Now - ultimoIntentoFallido.Value;
                    if (tiempoTranscurrido.TotalHours < 4)
                    {
                        var tiempoRestante = TimeSpan.FromHours(4) - tiempoTranscurrido;
                        return (false, $"¡Has superado el límite de intentos! Debes esperar {tiempoRestante.Hours} horas y {tiempoRestante.Minutes} minutos.");
                    }
                    else
                    {
                        // Resetear intentos si han pasado más de 4 horas
                        await ResetearIntentosFallidos(username);
                        return (true, string.Empty);
                    }
                }
            }

            return (true, string.Empty); //puede intentar login
        }

        private async Task ResetearIntentosFallidos(string username)
        {
            string query = @"UPDATE Usuario 
                            SET intentosFallidosLogin = 0, 
                                fechaUltimoIntentoFallido = NULL
                            WHERE usuario = @usuario";

            using var conn = new SqlConnection(_connectionString);
            using var command = new SqlCommand(query, conn);
            command.Parameters.AddWithValue("@usuario", username);

            await conn.OpenAsync();
            await command.ExecuteNonQueryAsync();
        }

        private async Task ActualizarUsuarioLoginExitoso(string username)
        {
            string query = @"UPDATE Usuario 
                            SET fechaUltimoLogin = GETDATE(),
                                intentosFallidosLogin = 0,
                                fechaUltimoIntentoFallido = NULL
                            WHERE usuario = @usuario";

            using var conn = new SqlConnection(_connectionString);
            using var command = new SqlCommand(query, conn);
            command.Parameters.AddWithValue("@usuario", username);

            await conn.OpenAsync();
            await command.ExecuteNonQueryAsync();
        }

        private async Task ActualizarUsuarioLoginFallido(string username)
        {
            string query = @"UPDATE Usuario 
                            SET intentosFallidosLogin = intentosFallidosLogin + 1,
                                fechaUltimoIntentoFallido = GETDATE()
                            WHERE usuario = @usuario";

            using var conn = new SqlConnection(_connectionString);
            using var command = new SqlCommand(query, conn);
            command.Parameters.AddWithValue("@usuario", username);

            await conn.OpenAsync();
            await command.ExecuteNonQueryAsync();
        }

        private async Task<(int IntentosFallidos, int IntentosRestantes)> ObtenerIntentosRestantes(string username)
        {
            string query = @"SELECT intentosFallidosLogin 
                            FROM Usuario 
                            WHERE usuario = @usuario";

            using var conn = new SqlConnection(_connectionString);
            using var command = new SqlCommand(query, conn);
            command.Parameters.AddWithValue("@usuario", username);

            await conn.OpenAsync();
            var intentosFallidos = (int?)await command.ExecuteScalarAsync() ?? 0;

            int intentosRestantes = Math.Max(0, 3 - intentosFallidos);

            return (intentosFallidos, intentosRestantes);
        }

        
    }
}
