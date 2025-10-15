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
    [AuthorizeRole("adminLogistico")]
    public class AdminLogisticoController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IHashService _hashService;
        private readonly LogService _logService;
        private readonly UserService _userService;
        private readonly ClientService _clientService;
        private readonly OrderService _orderService;
        private readonly FleetService _fleetService;
        private readonly RouteService _routeService;




        public AdminLogisticoController(IConfiguration configuration, IHashService hashService, LogService logService, ClientService clientService, OrderService orderService, UserService userService, FleetService fleetService, RouteService routeService)
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
        public IActionResult RouteManagement(string columna = "r.fechaCreacion", string orden = "desc")
        {
            try
            {
                // Leer claims desde la cookie
                var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
                var rolUsuario = User.FindFirstValue(ClaimTypes.Role);

                // Pasar al layout
                ViewBag.NombreUsuario = nombreUsuario;
                ViewBag.RolUsuario = rolUsuario;


                var rutas = _routeService.ObtenerRutas(columna, orden);

                return View(rutas);
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error al cargar las rutas";
                return View(new List<RouteViewModel>());
            }
        }


        [HttpGet]
        public async Task<IActionResult> GenerateRoutes()
        {
            // Leer claims desde la cookie
            var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
            var rolUsuario = User.FindFirstValue(ClaimTypes.Role);

            // Pasar al layout
            ViewBag.NombreUsuario = nombreUsuario;
            ViewBag.RolUsuario = rolUsuario;

            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "IP desconocida";
           
            var resultado = await _routeService.GenerateRoutesAsync();

            if (resultado.Exito)
            {
                _logService.AgregarLog(nombreUsuario, DateTime.Now, "Generacion de rutas", "Se generaron " + resultado.CantidadRutasGeneradas + " rutas", ipAddress);

            }
            else
            {
                _logService.AgregarLog(nombreUsuario, DateTime.Now, "Generacion de rutas", "Hubo un error al generar rutas. Codigo de error: " + resultado.CodigoError, ipAddress);

            }

            return View(resultado);




            // Llama al servicio que envía el JSON de prueba a Vroom
            //var resultadoJson = await _routeService.GetRouteAsync();

            // Devuelve la respuesta directamente como JSON
            //return Content(resultadoJson, "application/json");
        }

        [HttpGet]
        public async Task<IActionResult> AssignDriver()
        {
            // Leer claims desde la cookie
            var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
            var rolUsuario = User.FindFirstValue(ClaimTypes.Role);
            
            var rutas = await _routeService.ObtenerRutasSinAsignarAsync();

            // Pasar al layout
            ViewBag.NombreUsuario = nombreUsuario;
            ViewBag.RolUsuario = rolUsuario;

            ViewBag.Conductores    = await _userService.ObtenerConductoresDisponiblesAsync();
            ViewBag.PedidosPorRuta = await _orderService.ObtenerPedidosPorRutaAsync();

            return View(rutas);


            // Llama al servicio que envía el JSON de prueba a Vroom
            //var resultadoJson = await _routeService.GetRouteAsync();

            // Devuelve la respuesta directamente como JSON
            //return Content(resultadoJson, "application/json");
        }

        [HttpPost]
        public async Task<IActionResult> AsignarConductor(int idRuta, int idConductor, string patente)
        {
            if (idRuta <= 0 || idConductor <= 0)
            {
                TempData["ErrorMessageAssignDriver"] = "Debe seleccionar un conductor válido.";
                return RedirectToAction("AssignDriver");
            }

            string enlace = Url.Action("Index", "Conductor", null, Request.Scheme);
            var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);

            var exito = await _routeService.AsignarConductorAsync(idRuta, idConductor, patente, enlace);

            if (exito)
            {
                TempData["SuccessAsignarConductor"] = "Conductor asignado correctamente.";

                string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "IP desconocida";
                _logService.AgregarLog(nombreUsuario, DateTime.Now, "Asignacion de conductor", "Se asigno el conductor con id: "+ idConductor + " al camion con patente " + patente, ipAddress);
            }
            else
            {
                TempData["ErrorMessageAssignDriver"] = "No se pudo asignar el conductor. Verifique los datos.";
            }


            return RedirectToAction("AssignDriver");
        }


        public async Task<IActionResult> ShowRoute(string idRuta)
        {
            // Leer claims desde la cookie
            var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
            var rolUsuario = User.FindFirstValue(ClaimTypes.Role);

            // Pasar al layout
            ViewBag.NombreUsuario = nombreUsuario;
            ViewBag.RolUsuario = rolUsuario;
            ViewBag.idRuta = idRuta;

            var puntoPartida = _routeService.obtenerPuntoPartidaActual();
            ViewBag.PuntoPartidaActual = puntoPartida.Direccion;

            ViewBag.Pedidos = await _orderService.ObtenerPedidosPorIdRutaAsync(idRuta);

            var rutas = await _routeService.RetornarRutaConIdRuta(idRuta);

            return View(rutas);
        }

        [HttpGet("EditRoute/{idRuta}")]
        public async Task<IActionResult> EditRoute(string idRuta)
        {
            // Leer claims desde la cookie
            var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
            var rolUsuario = User.FindFirstValue(ClaimTypes.Role);

            // Pasar al layout
            ViewBag.NombreUsuario = nombreUsuario;
            ViewBag.RolUsuario = rolUsuario;
            ViewBag.idRuta = idRuta;

            return View();
        }

        [HttpGet("DeleteRoute/{idRuta}")]
        public async Task<IActionResult> DeleteRoute(string idRuta)
        {
            // Leer claims desde la cookie
            var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
            var rolUsuario = User.FindFirstValue(ClaimTypes.Role);

            // Pasar al layout
            ViewBag.idRuta = idRuta;

            return View();
        }



        [HttpPost("DeleteRoute/{idRuta}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRoute(string eleccion, string idRuta)
        {
            // Leer claims desde la cookie
            var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
            var rolUsuario = User.FindFirstValue(ClaimTypes.Role);

            // Pasar al layout
            ViewBag.idRuta = idRuta;

            Console.WriteLine("idruta: " + idRuta);

            if (eleccion == "no")
            {
                TempData["ErrorMessageDeleteRoute"] = "Ocurrio un error.";

                return View();
            }

            var rutaDb = _routeService.retornarRouteModelConIdRuta(idRuta);

            if (rutaDb == null)
            {
                TempData["ErrorMessageDeleteRoute"] = "No se puede cancelar una ruta finalizada";
                return View();
            }


            var resultado = await _routeService.EliminarRoute(idRuta);


            if (!resultado.Exito)
            {
                // Mostrar error en la misma vista
                TempData["ErrorMessageDeleteRoute"] = $"Error al cancelar ruta: {resultado.Mensaje}";
            }

            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "IP desconocida";
            _logService.AgregarLog(nombreUsuario, DateTime.Now, "Edicion ruta", nombreUsuario + " Cancelo la ruta con id: " + idRuta, ipAddress);

            TempData["SuccessDeleteMessage"] = $"Ruta con id '{idRuta}' cancelada correctamente";

            return View();
        }

        [HttpGet("ExportarRutaAGpx/{idRuta}")]
        public async Task<IActionResult> ExportarRutaAGpx(string idRuta)
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

            var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "IP desconocida";
            _logService.AgregarLog(nombreUsuario, DateTime.Now, "Exportar rutas a gpx", "Se creo un archivo gpx con ruta con id" + idRuta, ipAddress);

            var gpxBytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(gpxBytes, "application/gpx+xml", "ruta_logistica.gpx");
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

                var flota = _fleetService.ObtenerFlota(columna, orden, patente);

                return View(flota);

            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error al cargar los camiones";
                return View(new List<FleetViewModel>());
            }
        }

        [HttpGet]
        public IActionResult MyAccount()
        {
            // Leer claims desde la cookie
            var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
            var rolUsuario = User.FindFirstValue(ClaimTypes.Role);

            // Pasar al layout
            ViewBag.NombreUsuario = nombreUsuario;
            ViewBag.RolUsuario = rolUsuario;

            var puntoPartida = _routeService.obtenerPuntoPartidaActual();
            ViewBag.PuntoPartidaActual = puntoPartida.Direccion;



            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GuardarPuntoPartida(string DomicilioPartida, string CiudadPartida, string ProvinciaPartida)
        {
            // Leer claims desde la cookie
            var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
            var rolUsuario = User.FindFirstValue(ClaimTypes.Role);

            // Pasar al layout
            ViewBag.NombreUsuario = nombreUsuario;
            ViewBag.RolUsuario = rolUsuario;

            try
            {
                // Validaciones básicas: que no estén vacíos
                if (string.IsNullOrWhiteSpace(DomicilioPartida) ||
                    string.IsNullOrWhiteSpace(CiudadPartida) ||
                    string.IsNullOrWhiteSpace(ProvinciaPartida)) 
                {
                    TempData["ErrorMessageDeparturePoint"] = "Todos los campos obligatorios deben ser completados.";
                    return RedirectToAction("MyAccount");
                }


                // Espera a que se complete la tarea
                ResultadoOperacion resultado = await _orderService.CambiarPuntoPartidaFlota(DomicilioPartida, CiudadPartida, ProvinciaPartida);


                if (!resultado.Exito)
                {
                    // Mostrar error en la misma vista
                    TempData["ErrorMessageDeparturePoint"] = resultado.Mensaje;

                    return RedirectToAction("MyAccount");
                }

                string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "IP desconocida";
                _logService.AgregarLog(nombreUsuario, DateTime.Now, "Modificacion punto partida flota", "Se cambio el punto de partida de los camiones", ipAddress);

                TempData["SuccessMessageDeparturePoint"] = resultado.Mensaje;
                return RedirectToAction("MyAccount");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessageDeparturePoint"] = $"Error interno del servidor: {ex.Message}";
                return RedirectToAction("MyAccount");
            }


            return RedirectToAction("MyAccount");
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


        

        /*+-----------------------------ABM FLEET-----------------------------------------*/
        /*---------------GET-------------*/

        [HttpGet]
        public IActionResult CreateFleet()
        {
            // Leer claims desde la cookie
            var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
            var rolUsuario = User.FindFirstValue(ClaimTypes.Role);

            // Pasar al layout
            ViewBag.NombreUsuario = nombreUsuario;
            ViewBag.RolUsuario = rolUsuario;

            return View();
        }

        [HttpGet("DeleteFleet/{patente}")]
        public IActionResult DeleteFleet(string patente)
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

        [HttpGet("EditFleet/{patente}")]
        public IActionResult EditFleet(string patente)
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


        /*---------------POST-------------*/


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateFleet(FleetModel fleetModel)
        {
            // Leer claims desde la cookie
            var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
            var rolUsuario = User.FindFirstValue(ClaimTypes.Role);

            // Pasar al layout
            ViewBag.NombreUsuario = nombreUsuario;
            ViewBag.RolUsuario = rolUsuario;


            try
            {
                if (!ModelState.IsValid)
                {
                    TempData["ErrorMessageCreateFleet"] = "Ocurrio un error.";

                    return View(fleetModel);
                }

                // Validaciones básicas: que no estén vacíos ni negativos
                if (string.IsNullOrWhiteSpace(fleetModel.Ancho) ||
                    string.IsNullOrWhiteSpace(fleetModel.Largo) ||
                    string.IsNullOrWhiteSpace(fleetModel.Alto) ||
                    string.IsNullOrWhiteSpace(fleetModel.CapacidadPeso) ||
                    string.IsNullOrWhiteSpace(fleetModel.CapacidadVolumen))
                {
                    TempData["ErrorMessageCreateFleet"] = "Todos los campos obligatorios deben ser completados.";
                    return View(fleetModel);
                }

                // No se permiten valores negativos al menos en la validación básica
                if ((decimal.TryParse(fleetModel.Ancho.Replace(",", "."), out var ancho) && ancho < 0) ||
                    (decimal.TryParse(fleetModel.Largo.Replace(",", "."), out var largo) && largo < 0) ||
                    (decimal.TryParse(fleetModel.Alto.Replace(",", "."), out var alto) && alto < 0) ||
                    (decimal.TryParse(fleetModel.CapacidadPeso.Replace(",", "."), out var peso) && peso < 0) ||
                    (decimal.TryParse(fleetModel.CapacidadVolumen.Replace(",", "."), out var volumen) && volumen < 0))
                {
                    TempData["ErrorMessageCreateFleet"] = "No se permiten valores negativos.";
                    return View(fleetModel);
                }

                // Validaciones básicas
                if (fleetModel.Estado < 0 || fleetModel.Estado > 1)
                {
                    TempData["ErrorMessageCreateFleet"] = "Todos los campos obligatorios deben ser completados correctamente";
                    return View(fleetModel);
                }
                // Verificar si la patente ya existe

                var vehiculoDb = _fleetService.retornarFleetModelConPatente(fleetModel.Patente);
                if (vehiculoDb != null)
                {
                    TempData["ErrorMessageCreateFleet"] = "Patente ya ingresada, ingrese devuelta..";

                    return View(fleetModel);
                }


                var resultado = _fleetService.CrearFleet(fleetModel);


                if (!resultado.Exito)
                {
                    // Mostrar error en la misma vista
                    TempData["ErrorMessageCreateClient"] = resultado.Mensaje;

                    return View(fleetModel);
                }

                string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "IP desconocida";
                _logService.AgregarLog(nombreUsuario, DateTime.Now, "Crear vehiculo", "Se creo el vehiculo con patenete: " + fleetModel.Patente, ipAddress);

                TempData["MensajeExitoFormularioCrearVehiculo"] = resultado.Mensaje;
                return View();
            }
            catch (Exception ex)
            {
                TempData["ErrorMessageCreateFleet"] = $"Error interno del servidor: {ex.Message}";
                return View(fleetModel);
            }
        }


        [HttpPost("DeleteFleet/{patente}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteFleet(string eleccion, string patente)
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

        [HttpPost("EditFleet/{patente}")]
        [ValidateAntiForgeryToken]
        public IActionResult EditFleet(string patente, FleetModel fleetmodel)
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


        public IActionResult ExportarRutasPdf(string columna = "r.fechaCreacion", string orden = "desc")
        {
            var pdfBytes = _routeService.ExportarRutasPdf(columna, orden);

            // Agregar log de exportación (opcional)
            var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "IP desconocida";
            _logService.AgregarLog(nombreUsuario, DateTime.Now, "Exportar rutas", "Exportación de tabla ruta", ipAddress);

            return File(pdfBytes, "application/pdf", "ReporteRutas.pdf");
        }

    }
}