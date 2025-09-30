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
using System.Text;




namespace VIPS.Web.Controllers
{
    [AuthorizeRole("conductor")]
    public class ConductorController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IHashService _hashService;
        private readonly LogService _logService;
        private readonly UserService _userService;
        private readonly ClientService _clientService;
        private readonly OrderService _orderService;
        private readonly FleetService _fleetService;
        private readonly RouteService _routeService;





        public ConductorController(IConfiguration configuration, IHashService hashService, LogService logService, ClientService clientService, OrderService orderService, UserService userService, FleetService fleetService, RouteService routeService)
        {
            _configuration = configuration;
            _hashService = hashService;
            _clientService = clientService;
            _userService = userService;
            _logService = logService;
            _orderService = orderService;
            _fleetService = fleetService;
            _routeService = routeService;

        }

        [HttpGet("ExportarRutaAGpxConductor/{idRuta}")]
        public async Task<IActionResult> ExportarRutaAGpxConductor(string idRuta)
        {
            var puntoPartida = _routeService.obtenerPuntoPartidaActual();

            var pedidos = await _orderService.ObtenerPedidosPorIdRutaAsync(idRuta);

            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"no\" ?>");
            sb.AppendLine("<gpx version=\"1.1\" creator=\"VIPS\" xmlns=\"http://www.topografix.com/GPX/1/1\">");

            // Punto de partida
            sb.AppendLine($"  <wpt lat=\"{puntoPartida.Latitud}\" lon=\"{puntoPartida.Longitud}\">");
            sb.AppendLine("    <name>Punto de Partida</name>");
            sb.AppendLine("  </wpt>");

            // Waypoints de pedidos
            foreach (var pedido in pedidos)
            {
                sb.AppendLine($"  <wpt lat=\"{pedido.Latitud}\" lon=\"{pedido.Longitud}\">");
                sb.AppendLine($"    <name>Pedido {pedido.IdPedido} - {pedido.Direccion}</name>");
                sb.AppendLine("  </wpt>");
            }

            sb.AppendLine("</gpx>");

            var gpxBytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(gpxBytes, "application/gpx+xml", "ruta_logistica.gpx");
        }

        public async Task<IActionResult> Index()
        {
            // Leer claims desde la cookie
            var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
            var rolUsuario = User.FindFirstValue(ClaimTypes.Role);

            // Pasar al layout
            ViewBag.NombreUsuario = nombreUsuario;
            ViewBag.RolUsuario = rolUsuario;

            var puntoPartida = _routeService.obtenerPuntoPartidaActual();
            ViewBag.PuntoPartidaActual = puntoPartida.Direccion;

            ViewBag.Pedidos = await _orderService.ObtenerPedidosPorNombreUsuarioAsync(nombreUsuario);

            var rutas = await _routeService.RetornarRutaConNombreUsuario(nombreUsuario);

            return View(rutas);
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



        public async Task<IActionResult> IniciarRuta(string idRuta)
        {
            var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
            var resultado = await _routeService.IniciarRutaAsync(idRuta);
            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "IP desconocida";

            if (resultado.Exito)
            {
                _logService.AgregarLog(nombreUsuario, DateTime.Now, "Inicio de ruta", resultado.Mensaje, ipAddress);
                TempData["SuccessMessageConductorRuta"] = resultado.Mensaje;

            }
            else
            {
                _logService.AgregarLog(nombreUsuario, DateTime.Now, "Inicio de ruta", resultado.Mensaje, ipAddress);
                TempData["ErrorMessageConductorRuta"] = resultado.Mensaje;
            }



            return RedirectToAction("Index");
        }

        public async Task<IActionResult> FinalizarRuta()
        {
            return RedirectToAction("Index");

        }

        public async Task<IActionResult> MarcarEntregaEntregada(string idPedido, string idRuta)
        {
            var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
            var resultado = _routeService.MarcarEntregaEntregada(idPedido);
            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "IP desconocida";

            if (resultado.Exito)
            {
                _logService.AgregarLog(nombreUsuario, DateTime.Now, "Actualizacion de estado de pedido en ruta", resultado.Mensaje, ipAddress);
                TempData["SuccessMessageConductorRuta"] = resultado.Mensaje;
            }
            else
            {
                _logService.AgregarLog(nombreUsuario, DateTime.Now, "Actualizacion de estado de pedido en ruta", resultado.Mensaje, ipAddress);
                TempData["ErrorMessageConductorRuta"] = resultado.Mensaje;
            }

            return RedirectToAction("Index");

        }

        public async Task<IActionResult> MarcarEntregaFallida(string idPedido, string idRuta)
        {
            var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
            var resultado = _routeService.MarcarEntregaFallida(idPedido);
            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "IP desconocida";

            if (resultado.Exito)
            {
                _logService.AgregarLog(nombreUsuario, DateTime.Now, "Actualizacion de estado de pedido en ruta", resultado.Mensaje, ipAddress);
                TempData["SuccessMessageConductorRuta"] = resultado.Mensaje;
            }
            else
            {
                _logService.AgregarLog(nombreUsuario, DateTime.Now, "Actualizacion de estado de pedido en ruta", resultado.Mensaje, ipAddress);
                TempData["ErrorMessageConductorRuta"] = resultado.Mensaje;
            }

            return RedirectToAction("Index");

        }







    }
}