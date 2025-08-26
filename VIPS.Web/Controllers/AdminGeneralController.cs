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



namespace VIPS.Web.Controllers
{
    [AuthorizeRole("adminGeneral")]
    public class AdminGeneralController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IHashService _hashService;
        private readonly LogService _logService;
        private readonly UserService _userService;
        //private readonly PdfService _pdfService;


        public AdminGeneralController(IConfiguration configuration, IHashService hashService, LogService logService, UserService userService)
        {
            _configuration = configuration;
            _hashService = hashService;
            _userService = userService;
            _logService = logService;
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
        public IActionResult UserManagement(string columna = "fechaUltimoLogin", string orden = "desc")
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

                var usuarios = _userService.ObtenerUsuarios(columna, orden);
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
            Console.WriteLine("create user");
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
                    ModelState.AddModelError("", "Todos los campos obligatorios deben ser completados");
                    ViewBag.Roles = _userService.ObtenerRoles();
                    return View(usuarioModel);
                }

                // Verificar si el DNI ya existe

                var usuarioExistente = await _userService.VerificarUsuarioExistente(usuarioModel.Dni);
                if (usuarioExistente)
                {
                    ModelState.AddModelError("Usuario", "Usuario invalido, ingrese devuelta.");
                    ViewBag.Roles = _userService.ObtenerRoles();
                    return View(usuarioModel);
                }

                // Verificar si el DNI ya existe
                var dniExistente = await _userService.VerificarDniExistente(usuarioModel.Dni);
                if (dniExistente)
                {
                    ModelState.AddModelError("Dni", "Dni invalido, ingrese devuelta");
                    ViewBag.Roles = _userService.ObtenerRoles();
                    return View(usuarioModel);
                }

                // Verificar si el email ya existe
                var emailExistente = await _userService.VerificarEmailExistente(usuarioModel.Email);
                if (emailExistente)
                {
                    ModelState.AddModelError("Email", "Email invalido, ingrese devuelta");
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
                    ModelState.AddModelError("", resultado.Mensaje);
                    ViewBag.Roles = _userService.ObtenerRoles();
                    return View(usuarioModel);
                }

                TempData["MensajeExitoFormularioCrearUsuario"] = resultado.Mensaje;
                return RedirectToAction("UserManagement");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error interno del servidor: {ex.Message}");
                return View(usuarioModel);
            }
        }

        public IActionResult OrderManagement()
        {

            // Leer claims desde la cookie
            var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
            var rolUsuario = User.FindFirstValue(ClaimTypes.Role);

            // Pasar al layout
            ViewBag.NombreUsuario = nombreUsuario;
            ViewBag.RolUsuario = rolUsuario;
            return View();
        }

        public IActionResult FleetManagement()
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
        public IActionResult SystemLogs(int cantLogs = 10, string columna = "FechaHora", string orden = "desc")
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


                var logs = _logService.ObtenerLogs(cantLogs, columna, orden);
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
            var resultado = _userService.EliminarUsuario(username);


            if (!resultado.Exito)
            {
                // Mostrar error en la misma vista
                TempData["ErrorDeleteMessage"] = $"Error al eliminar usuario: {resultado.Mensaje}";
            }

            TempData["SuccessDeleteMessage"] = $"Usuario '{username}' eliminado correctamente";
            return RedirectToAction("UserManagement");
        }

        public async Task<IActionResult> ExportarUsuariosPdf(string columna = "fechaUltimoLogin", string orden = "desc")
        {
            var usuarios = _userService.ObtenerUsuarios(columna, orden);

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
                return File(ms.ToArray(), "application/pdf", "ReporteUsuarios.pdf");
            }
        }


        public async Task<IActionResult> ExportarLogsPdf(int cantLogs = 10, string columna = "FechaHora", string orden = "desc")
        {
            var logs = _logService.ObtenerLogs(cantLogs, columna, orden);

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
                return File(ms.ToArray(), "application/pdf", "ReporteLogs.pdf");
            }
        }



    }
}
