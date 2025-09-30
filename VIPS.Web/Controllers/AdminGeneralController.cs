using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using VIPS.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using VIPS.Web.Services;
using System.Data;
using VIPS.Web.Attributes;
using System.Security.Claims;
using System.Reflection.Metadata;
using PdfSharpCore.Pdf;
using PdfSharpCore.Drawing;
using System.IO;
using PdfSharpCore.Drawing.Layout; // <- necesario para XTextFormatter

namespace VIPS.Web.Controllers
{
    [AuthorizeRole("adminGeneral")]
    public class AdminGeneralController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IHashService _hashService;
        private readonly LogService _logService;
        private readonly UserService _userService;
        private readonly FleetService _fleetService;
        private readonly OrderService _orderService;




        public AdminGeneralController(IConfiguration configuration, IHashService hashService, LogService logService, UserService userService, FleetService fleetService, OrderService orderService)
        {
            _configuration = configuration;
            _hashService = hashService;
            _userService = userService;
            _logService = logService;
            _fleetService = fleetService;
            _orderService = orderService;


        }

        public IActionResult Index()
        {
            // Leer claims desde la cookie
            var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
            var rolUsuario = User.FindFirstValue(ClaimTypes.Role);

            // Pasar al layout
            ViewBag.NombreUsuario = nombreUsuario;
            ViewBag.RolUsuario = rolUsuario;

            return View();
        }

        [HttpGet]
        public IActionResult UserManagement(string columna = "fechaUltimoLogin", string orden = "desc", string? usuario = null)
        {
            try
            {
                // Leer claims desde la cookie
                var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
                var rolUsuario = User.FindFirstValue(ClaimTypes.Role);

                // Pasar al layout
                ViewBag.NombreUsuario = nombreUsuario;
                ViewBag.RolUsuario = rolUsuario;

                ViewBag.Columna = columna;
                ViewBag.Orden = orden;
                ViewBag.Usuario = usuario;

                var usuarios = _userService.ObtenerUsuarios(columna, orden, usuario);
                return View(usuarios);
            }
            catch (Exception ex)

            {   // Manejar errores
                ViewBag.Error = "Error al cargar los usuarios";
                return View(new List<UsuarioViewModel>());
            }



            return View();
        }


        [HttpGet]
        public IActionResult CreateUser()
        {
            // Leer claims desde la cookie
            var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
            var rolUsuario = User.FindFirstValue(ClaimTypes.Role);

            // Pasar al layout
            ViewBag.NombreUsuario = nombreUsuario;
            ViewBag.RolUsuario = rolUsuario;
            var roles = _userService.ObtenerRoles();
            ViewBag.Roles = roles;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(UsuarioModel usuarioModel)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    ViewBag.Roles = _userService.ObtenerRoles();
                    TempData["ErrorMessageCreateUser"] = "Ocurrio un error.";

                    return View(usuarioModel);
                }

                // Validaciones básicas
                if (string.IsNullOrEmpty(usuarioModel.Dni) ||
                    string.IsNullOrEmpty(usuarioModel.Nombre) ||
                    string.IsNullOrEmpty(usuarioModel.Apellido) ||
                    string.IsNullOrEmpty(usuarioModel.Email) ||
                    string.IsNullOrEmpty(usuarioModel.Telefono) ||
                    string.IsNullOrEmpty(usuarioModel.Contrasenia))
                {
                    TempData["ErrorMessageCreateUser"] = "Todos los campos obligatorios deben ser completados";

                    ViewBag.Roles = _userService.ObtenerRoles();
                    return View(usuarioModel);
                }

                // Verificar si el DNI ya existe

                var usuarioExistente = _userService.VerificarUsuarioExistente(usuarioModel.Dni);
                if (usuarioExistente)
                {
                    TempData["ErrorMessageCreateUser"] = "Usuario invalido, ingrese devuelta..";

                    ViewBag.Roles = _userService.ObtenerRoles();
                    return View(usuarioModel);
                }

                // Verificar si el DNI ya existe
                var dniExistente = _userService.VerificarDniExistente(usuarioModel.Dni);
                if (dniExistente)
                {
                    TempData["ErrorMessageCreateUser"] = "Dni invalido, ingrese devuelta.";

                    ViewBag.Roles = _userService.ObtenerRoles();
                    return View(usuarioModel);
                }

                // Verificar si el email ya existe
                var emailExistente = _userService.VerificarEmailExistente(usuarioModel.Email);
                if (emailExistente)
                {
                    TempData["ErrorMessageCreateUser"] = "Email invalido, ingrese devuelta.";

                    ViewBag.Roles = _userService.ObtenerRoles();
                    return View(usuarioModel);
                }

                // Hashear la contraseña
                var contraseniaHash = _hashService.HashPassword(usuarioModel.Contrasenia);

                var resultado = _userService.CrearUsuario(usuarioModel, contraseniaHash);

                Console.WriteLine(resultado.Mensaje);


                if (!resultado.Exito)
                {
                    // Mostrar error en la misma vista
                    TempData["ErrorMessageCreateUser"] = resultado.Mensaje;

                    ViewBag.Roles = _userService.ObtenerRoles();
                    return View(usuarioModel);
                }

                string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "IP desconocida";
                _logService.AgregarLog(usuarioModel.Usuario, DateTime.Now, "Crear cuenta", "Se creo la cuenta con usuario: " + usuarioModel.Usuario, ipAddress);

                TempData["MensajeExitoFormularioCrearUsuario"] = resultado.Mensaje;
                return RedirectToAction("CreateUser");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessageCreateUser"] = $"Error interno del servidor: {ex.Message}";
                return View(usuarioModel);
            }
        }

        [HttpGet]
        public IActionResult OrderManagement(string columna = "fechaCreacion", string orden = "desc", string? nombreCliente = null)
        {
            try
            {
                // Leer claims desde la cookie
                var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
                var rolUsuario = User.FindFirstValue(ClaimTypes.Role);

                // Pasar al layout
                ViewBag.NombreUsuario = nombreUsuario;
                ViewBag.RolUsuario = rolUsuario;
                ViewBag.NombreCliente = nombreCliente;

                ViewBag.Columna = columna;
                ViewBag.Orden = orden;

                var pedidos = _orderService.ObtenerPedidos(columna, orden, nombreCliente);

                return View(pedidos);
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error al cargar los pedidos";
                return View(new List<OrderViewModel>());
            }
        }

        [HttpGet]
        public IActionResult FleetManagement(string columna = "fechaCreacion", string orden = "desc", string? patente = null)
        {
            try
            {
                // Leer claims desde la cookie
                var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
                var rolUsuario = User.FindFirstValue(ClaimTypes.Role);

                // Pasar al layout
                ViewBag.NombreUsuario = nombreUsuario;
                ViewBag.RolUsuario = rolUsuario;
                ViewBag.Patente = patente;

                ViewBag.Columna = columna;
                ViewBag.Orden = orden;

                var flota = _fleetService.ObtenerFlota(columna, orden, patente);

                return View(flota);

            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error al cargar los camiones";
                return View(new List<FleetViewModel>());
            }
        }







        [HttpGet("EditFleetGeneral/{patente}")]
        public IActionResult EditFleetGeneral(string patente)
        {
            // Leer claims desde la cookie
            var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
            var rolUsuario = User.FindFirstValue(ClaimTypes.Role);

            // Pasar al layout
            ViewBag.NombreUsuario = nombreUsuario;
            ViewBag.RolUsuario = rolUsuario;
            ViewBag.patente = patente;


            return View(_fleetService.retornarFleetModelConPatente(patente));
        }



        [HttpPost("EditFleetGeneral/{patente}")]
        [ValidateAntiForgeryToken]
        public IActionResult EditFleetGeneral(string patente, FleetModel fleetmodel)
        {
            // Leer claims desde la cookie
            var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
            var rolUsuario = User.FindFirstValue(ClaimTypes.Role);

            Console.WriteLine("entro 1");

            // Pasar al layout
            ViewBag.NombreUsuario = nombreUsuario;
            ViewBag.RolUsuario = rolUsuario;
            ViewBag.patente = patente;

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessageEditFleet"] = "Ocurrio un error.";

                return View(fleetmodel);
            }

            // Validaciones básicas: que no estén vacíos ni negativos
            if (string.IsNullOrWhiteSpace(fleetmodel.Ancho) ||
                string.IsNullOrWhiteSpace(fleetmodel.Largo) ||
                string.IsNullOrWhiteSpace(fleetmodel.Alto) ||
                string.IsNullOrWhiteSpace(fleetmodel.CapacidadPeso) ||
                string.IsNullOrWhiteSpace(fleetmodel.CapacidadVolumen))
            {
                TempData["ErrorMessageEditFleet"] = "Todos los campos obligatorios deben ser completados.";
                return View(fleetmodel);
            }

            // No se permiten valores negativos al menos en la validación básica
            if ((decimal.TryParse(fleetmodel.Ancho.Replace(",", "."), out var ancho) && ancho < 0) ||
                (decimal.TryParse(fleetmodel.Largo.Replace(",", "."), out var largo) && largo < 0) ||
                (decimal.TryParse(fleetmodel.Alto.Replace(",", "."), out var alto) && alto < 0) ||
                (decimal.TryParse(fleetmodel.CapacidadPeso.Replace(",", "."), out var peso) && peso < 0) ||
                (decimal.TryParse(fleetmodel.CapacidadVolumen.Replace(",", "."), out var volumen) && volumen < 0))
            {
                TempData["ErrorMessageEditFleet"] = "No se permiten valores negativos.";
                return View(fleetmodel);
            }

            // Validaciones básicas
            if (fleetmodel.Estado < 0 || fleetmodel.Estado > 1)
            {
                TempData["ErrorMessageEditFleet"] = "Todos los campos obligatorios deben ser completados correctamente";
                return View(fleetmodel);
            }

            //verifico que no exista vehiculo con esa patente

            var fleetDb = _fleetService.retornarFleetModelConPatente(patente);

            if (fleetDb == null)
            {
                TempData["ErrorMessageEditFleet"] = "Seleccione un vehiculo valido.";

                return View(fleetmodel);
            }



            if (!ModelState.IsValid)
            {
                TempData["ErrorMessageEditFleet"] = "Ocurrio un error.";

                return View(fleetmodel);
            }

            ResultadoOperacion resultado = _fleetService.UpdateFleet(fleetmodel);

            if (!resultado.Exito)
            {
                TempData["ErrorMessageEditFleet"] = resultado.Mensaje;
                return View(fleetmodel);
            }

            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "IP desconocida";
            _logService.AgregarLog(nombreUsuario, DateTime.Now, "Edicion flota", nombreUsuario + " Edito el vehiculo con patente : " + patente, ipAddress);

            TempData["MensajeExito"] = resultado.Mensaje;
            return View(fleetmodel);
        }



        [HttpGet("DeleteFleetGeneral/{patente}")]
        public IActionResult DeleteFleetGeneral(string patente)
        {
            // Leer claims desde la cookie
            var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
            var rolUsuario = User.FindFirstValue(ClaimTypes.Role);

            // Pasar al layout
            ViewBag.NombreUsuario = nombreUsuario;
            ViewBag.RolUsuario = rolUsuario;
            ViewBag.patente = patente;

            return View();
        }

        [HttpPost("DeleteFleetGeneral/{patente}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteFleetGeneral(string eleccion, string patente)
        {
            // Leer claims desde la cookie
            var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
            var rolUsuario = User.FindFirstValue(ClaimTypes.Role);

            // Pasar al layout
            ViewBag.NombreUsuario = nombreUsuario;
            ViewBag.RolUsuario = rolUsuario;
            ViewBag.patente = patente;


            if (eleccion == "no")
            {
                TempData["ErrorMessageDeleteFleet"] = "Ocurrio un error.";

                return View();
            }

            var flotaDb = _fleetService.retornarFleetModelConPatente(patente);

            if (flotaDb == null)
            {
                TempData["ErrorMessageDeleteFleet"] = "El vehiculo no existe.";
                return View();
            }


            var resultado = _fleetService.EliminarFleet(patente);


            if (!resultado.Exito)
            {
                // Mostrar error en la misma vista
                TempData["ErrorMessageDeleteFleet"] = $"Error al eliminar vehiculo: {resultado.Mensaje}";
            }

            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "IP desconocida";
            _logService.AgregarLog(nombreUsuario, DateTime.Now, "Edicion flota", nombreUsuario + " Elimino el vehiculo con patente: " + patente, ipAddress);

            TempData["SuccessDeleteMessage"] = $"Vehiculo con patente '{patente}' eliminado correctamente";

            return View();
        }



        [HttpGet]
        public IActionResult SystemLogs(int cantLogs = 10, string columna = "FechaHora", string orden = "desc", string? detalle = null)
        {
            try
            {
                // Leer claims desde la cookie
                var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
                var rolUsuario = User.FindFirstValue(ClaimTypes.Role);

                // Pasar al layout
                ViewBag.NombreUsuario = nombreUsuario;
                ViewBag.RolUsuario = rolUsuario;

                ViewBag.Columna = columna;
                ViewBag.Orden = orden;
                ViewBag.CantLogs = cantLogs;
                ViewBag.Detalle = detalle;


                var logs = _logService.ObtenerLogs(cantLogs, columna, orden, detalle);
                return View(logs);
            }
            catch (Exception ex)

            {   // Manejar errores
                ViewBag.Error = "Error al cargar los usuarios";
                return View(new List<LogViewModel>());
            }

        }

        public IActionResult UpdateAndRestore()
        {

            // Leer claims desde la cookie
            var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
            var rolUsuario = User.FindFirstValue(ClaimTypes.Role);

            // Pasar al layout
            ViewBag.NombreUsuario = nombreUsuario;
            ViewBag.RolUsuario = rolUsuario;
            return View();
        }

        [HttpGet("DeleteUser/{username}")]
        public async Task<IActionResult> DeleteUser(string username)
        {
            // Leer claims desde la cookie
            var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
            var rolUsuario = User.FindFirstValue(ClaimTypes.Role);

            // Pasar al layout
            ViewBag.NombreUsuario = nombreUsuario;
            ViewBag.RolUsuario = rolUsuario;

            ViewBag.Usuario = username;

            return View();
        }

        [HttpPost("DeleteUser/{username}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string eleccion, string username)
        {
            if (eleccion == "no")
            {
                TempData["ErrorMessageDeleteUser"] = "Ocurrio un error.";
                ViewBag.Usuario = username;
                return RedirectToAction("DeleteUser");
            }

            var usuarioDb = _userService.retornarUsuarioModelEditConIdUsuario(_userService.RetornarIdUsuarioConUsuario(username));

            if (usuarioDb == null)
            {
                TempData["ErrorMessageDeleteUser"] = "El usuario no existe.";
                ViewBag.Usuario = username;
                return RedirectToAction("DeleteUser");
            }


            if (_userService.EsAdminGeneral(usuarioDb.IdRol))
            {
                TempData["ErrorMessageDeleteUser"] = "No se puede elmninar el admin general.";
                ViewBag.Usuario = username;
                return RedirectToAction("DeleteUser");
            }

            var resultado = _userService.EliminarUsuario(username);


            if (!resultado.Exito)
            {
                // Mostrar error en la misma vista
                ViewBag.Usuario = username;
                TempData["ErrorMessageDeleteUser"] = $"Error al eliminar usuario: {resultado.Mensaje}";
            }

            var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "IP desconocida";
            _logService.AgregarLog(nombreUsuario, DateTime.Now, "Edicion cuenta", nombreUsuario + " Elimino la cuenta con usuario: " + username, ipAddress);

            TempData["SuccessDeleteMessage"] = $"Usuario '{username}' eliminado correctamente";
            return View();
        }

        public async Task<IActionResult> ExportarUsuariosPdf(string columna = "fechaUltimoLogin", string orden = "desc", string? usuario = null)
        {
            var usuarios = _userService.ObtenerUsuarios(columna, orden, usuario);

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
                gfx.DrawString("Usuario", font, XBrushes.Black, new XRect(startX, startY, 80, rowHeight), XStringFormats.Center);
                gfx.DrawString("Rol", font, XBrushes.Black, new XRect(startX + 80, startY, 60, rowHeight), XStringFormats.Center);
                gfx.DrawString("Fecha Alta", font, XBrushes.Black, new XRect(startX + 140, startY, 100, rowHeight), XStringFormats.Center);
                gfx.DrawString("Último Acceso", font, XBrushes.Black, new XRect(startX + 240, startY, 100, rowHeight), XStringFormats.Center);
                gfx.DrawString("Último Acceso Fallido", font, XBrushes.Black, new XRect(startX + 340, startY, 120, rowHeight), XStringFormats.Center);

                // Línea debajo del encabezado
                gfx.DrawLine(XPens.Black, startX, startY + rowHeight, startX + 460, startY + rowHeight);

                // Dibujar filas
                double y = startY + rowHeight;
                foreach (var user in usuarios)
                {
                    y += rowHeight;
                    gfx.DrawString(user.Usuario, font, XBrushes.Black, new XRect(startX, y, 80, rowHeight), XStringFormats.Center);
                    gfx.DrawString(user.Rol, font, XBrushes.Black, new XRect(startX + 80, y, 60, rowHeight), XStringFormats.Center);
                    gfx.DrawString(user.FechaAlta.ToString("dd/MM/yyyy"), font, XBrushes.Black, new XRect(startX + 140, y, 100, rowHeight), XStringFormats.Center);
                    gfx.DrawString(user.FechaUltimoAcceso?.ToString("dd/MM/yyyy") ?? "-", font, XBrushes.Black, new XRect(startX + 240, y, 100, rowHeight), XStringFormats.Center);
                    gfx.DrawString(user.FechaUltimoAccesoFallido?.ToString("dd/MM/yyyy") ?? "-", font, XBrushes.Black, new XRect(startX + 340, y, 120, rowHeight), XStringFormats.Center);

                    // Línea debajo de la fila
                    gfx.DrawLine(XPens.Gray, startX, y + rowHeight, startX + 460, y + rowHeight);
                }

                // Guardar PDF en memoria y devolver al navegador
                document.Save(ms, false);

                var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
                string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "IP desconocida";
                _logService.AgregarLog(nombreUsuario, DateTime.Now, "Exportar usuario", "Exportacion de tabla usuarios", ipAddress);

                return File(ms.ToArray(), "application/pdf", "ReporteUsuarios.pdf");
            }
        }


        public async Task<IActionResult> ExportarLogsPdf(int cantLogs = 10, string columna = "FechaHora", string orden = "desc", string? detalle = null)
        {
            var logs = _logService.ObtenerLogs(cantLogs, columna, orden, detalle);

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
                gfx.DrawString("Id log Actividad", font, XBrushes.Black, new XRect(startX, startY, 80, rowHeight), XStringFormats.Center);
                gfx.DrawString("Usuario", font, XBrushes.Black, new XRect(startX + 80, startY, 60, rowHeight), XStringFormats.Center);
                gfx.DrawString("Fecha y hora", font, XBrushes.Black, new XRect(startX + 140, startY, 100, rowHeight), XStringFormats.Center);
                gfx.DrawString("Accion", font, XBrushes.Black, new XRect(startX + 240, startY, 100, rowHeight), XStringFormats.Center);
                gfx.DrawString("Detalle", font, XBrushes.Black, new XRect(startX + 340, startY, 180, rowHeight), XStringFormats.Center);

                // Línea debajo del encabezado
                gfx.DrawLine(XPens.Black, startX, startY + rowHeight, startX + 520, startY + rowHeight);

                // Dibujar filas
                double y = startY + rowHeight;
                foreach (var log in logs)
                {
                    y += rowHeight;
                    gfx.DrawString(log.idLogActividad, font, XBrushes.Black, new XRect(startX, y, 80, rowHeight), XStringFormats.Center);
                    gfx.DrawString(log.Usuario, font, XBrushes.Black, new XRect(startX + 80, y, 60, rowHeight), XStringFormats.Center);
                    gfx.DrawString(log.FechaHora.ToString("dd/MM/yyyy"), font, XBrushes.Black, new XRect(startX + 140, y, 100, rowHeight), XStringFormats.Center);
                    gfx.DrawString(log.Accion, font, XBrushes.Black, new XRect(startX + 240, y, 100, rowHeight), XStringFormats.Center);
                    gfx.DrawString(log.Detalle ?? "-", font, XBrushes.Black, new XRect(startX + 340, y, 180, rowHeight), XStringFormats.Center);


                    // Línea debajo de la fila
                    gfx.DrawLine(XPens.Gray, startX, y + rowHeight, startX + 520, y + rowHeight);
                }

                // Guardar PDF en memoria y devolver al navegador
                document.Save(ms, false);

                var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
                string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "IP desconocida";
                _logService.AgregarLog(nombreUsuario, DateTime.Now, "Exportar logs", "Exportacion de tabla logs", ipAddress);

                return File(ms.ToArray(), "application/pdf", "ReporteLogs.pdf");
            }
        }

        public IActionResult ExportarPedidosPdf(string columna = "fechaCreacion", string orden = "desc", string? nombreCliente = null)
        {
            var pdfBytes = _orderService.ExportarPedidosPdf(columna, orden, nombreCliente);

            // Log de exportación
            var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "IP desconocida";
            _logService.AgregarLog(nombreUsuario, DateTime.Now, "Exportar pedidos", "Exportación de tabla pedidos", ipAddress);

            return File(pdfBytes, "application/pdf", "ReportePedidos.pdf");
        }


        public IActionResult ExportarFlotaPdf(string columna = "fechaCreacion", string orden = "desc", string? patente = null)
        {
            var pdfBytes = _fleetService.ExportarFlotaPdf(columna, orden, patente);

            // Agregar log de exportación (opcional)
            var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "IP desconocida";
            _logService.AgregarLog(nombreUsuario, DateTime.Now, "Exportar flota", "Exportación de tabla flota", ipAddress);

            return File(pdfBytes, "application/pdf", "ReporteFlota.pdf");
        }


        public IActionResult MyAccount()
        {
            // Leer claims desde la cookie
            var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
            var rolUsuario = User.FindFirstValue(ClaimTypes.Role);

            // Pasar al layout
            ViewBag.NombreUsuario = nombreUsuario;
            ViewBag.RolUsuario = rolUsuario;
            

            return View();
        }


        [HttpGet("EditUser/{username}")]
        public IActionResult EditUser(string username)
        { 
            var roles = _userService.ObtenerRoles();
            ViewBag.Roles = roles;

            // Leer claims desde la cookie
            var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
            var rolUsuario = User.FindFirstValue(ClaimTypes.Role);

            // Pasar al layout
            ViewBag.NombreUsuario = nombreUsuario;
            ViewBag.RolUsuario = rolUsuario;

            return View(_userService.retornarUsuarioModelEditConIdUsuario(_userService.RetornarIdUsuarioConUsuario(username)));
        }


        [HttpPost("EditUser/{username}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(string username,UsuarioModelEdit usuarioModel)
        {

            if (!ModelState.IsValid)
            {
                ViewBag.Roles = _userService.ObtenerRoles();
                var errores = ModelState.Values
                            .SelectMany(v => v.Errors)
                            .Select(e => e.ErrorMessage)
                            .ToList();

                TempData["ErrorMessageEditUser"] = "Ocurrio un error.";

                return View(usuarioModel);
            }

            // Validaciones básicas
            if (string.IsNullOrEmpty(usuarioModel.Dni) ||
                string.IsNullOrEmpty(usuarioModel.Nombre) ||
                string.IsNullOrEmpty(usuarioModel.Apellido) ||
                string.IsNullOrEmpty(usuarioModel.Email) ||
                string.IsNullOrEmpty(usuarioModel.Telefono))
            {
                TempData["ErrorMessageEditUser"] = "Todos los campos obligatorios deben ser completados";

                ViewBag.Roles = _userService.ObtenerRoles();
                return View(usuarioModel);
            }



            var usuarioDb = _userService.retornarUsuarioModelEditConIdUsuario(usuarioModel.IdUsuario);

            if (usuarioDb == null)
            {
                TempData["ErrorMessageEditUser"] = "El usuario no existe.";

                ViewBag.Roles = _userService.ObtenerRoles();
                return View(usuarioModel);
            }


            if (_userService.EsAdminGeneral(usuarioDb.IdRol))
            {
                usuarioModel.IdRol = usuarioDb.IdRol;
            }

            var conflicto = _userService.ExisteConflicto(usuarioModel);

            if (conflicto.Dni || conflicto.Usuario || conflicto.Email || conflicto.Telefono)
            {
                if (conflicto.Dni) TempData["ErrorMessageEditUser"] = "El DNI ya está en uso por otro usuario.";
                else if (conflicto.Usuario) TempData["ErrorMessageEditUser"] = "El nombre de usuario ya está en uso.";
                else if (conflicto.Email) TempData["ErrorMessageEditUser"] = "El correo ya está en uso.";
                else if (conflicto.Telefono) TempData["ErrorMessageEditUser"] = "El teléfono ya está en uso.";

                return View(usuarioModel);
            }




            if (!ModelState.IsValid)
            {
                ViewBag.Roles = _userService.ObtenerRoles();

                return View(usuarioModel);
            }


            ResultadoOperacion resultado = _userService.UpdateUser(usuarioModel);

            if (!resultado.Exito)
            {
                TempData["ErrorMessageEditUser"] = resultado.Mensaje;
                ViewBag.Roles = _userService.ObtenerRoles();
                return View(usuarioModel);
            }

            var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "IP desconocida";
            _logService.AgregarLog(nombreUsuario, DateTime.Now, "Edicion cuenta", nombreUsuario + " Edito la cuenta con usuario: " + usuarioModel.Usuario, ipAddress);

            TempData["MensajeExito"] = resultado.Mensaje;
            ViewBag.Roles = _userService.ObtenerRoles();
            return View(usuarioModel);
        }

        [HttpPost]
        public async Task<IActionResult> CambiarIdioma(string nuevoLang)
        {
            // Validación básica
            if (string.IsNullOrEmpty(nuevoLang) || (nuevoLang != "es" && nuevoLang != "en"))
            {
                TempData["ErrorMessageChangeLanguage"] = "Ocurrió un error";
                return RedirectToAction("MyAccount");
            }

            // Obtener el username del claim del usuario logueado
            string username = User.FindFirstValue(ClaimTypes.Name);
            if (string.IsNullOrEmpty(username))
            {
                TempData["ErrorMessageChangeLanguage"] = "Ocurrió un error";
                return RedirectToAction("MyAccount");
            }

            // Actualizar idioma en la base de datos
            ResultadoOperacion resultado = _userService.ActualizarIdioma(username, nuevoLang);

            if (!resultado.Exito)
            {
                TempData["ErrorMessageChangeLanguage"] = resultado.Mensaje ?? "Ocurrió un error al actualizar el idioma";
                return RedirectToAction("MyAccount");
            }

            // Actualizar cookie
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, User.FindFirstValue(ClaimTypes.Name)),
                new Claim(ClaimTypes.Role, User.FindFirstValue(ClaimTypes.Role)),
                new Claim("Lang", nuevoLang)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                new AuthenticationProperties { IsPersistent = true });

            // Mensaje de éxito
            TempData["SuccessMessageChangeLanguage"] = (nuevoLang == "es")
                ? "Idioma cambiado a español"
                : "Language changed to English";


            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "IP desconocida";
            _logService.AgregarLog(username, DateTime.Now, "Cambio idioma", "Cambio de idioma a: " + nuevoLang, ipAddress);

            return RedirectToAction("MyAccount");
        }

    }
}
