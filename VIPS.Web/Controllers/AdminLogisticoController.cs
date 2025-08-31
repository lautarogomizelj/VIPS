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



        public AdminLogisticoController(IConfiguration configuration, IHashService hashService, LogService logService, ClientService clientService, OrderService orderService, UserService userService, FleetService fleetService)
        {
            _configuration = configuration;
            _hashService = hashService;
            _clientService = clientService;
            _userService = userService;
            _logService = logService;
            _orderService = orderService;
            _fleetService = fleetService;

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
        public IActionResult OrderManagement(string columna = "fechaCreacion", string orden = "desc")
        {
            try
            {
                // Leer claims desde la cookie
                var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
                var rolUsuario = User.FindFirstValue(ClaimTypes.Role);

                // Pasar al layout
                ViewBag.NombreUsuario = nombreUsuario;
                ViewBag.RolUsuario = rolUsuario;

                var pedidos = _orderService.ObtenerPedidos(columna, orden);

                return View(pedidos);
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error al cargar los pedidos";
                return View(new List<OrderViewModel>());
            }
        }

        [HttpGet]
        public IActionResult FleetManagement(string columna = "fechaCreacion", string orden = "desc")
        {
            try
            {
                // Leer claims desde la cookie
                var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
                var rolUsuario = User.FindFirstValue(ClaimTypes.Role);

                // Pasar al layout
                ViewBag.NombreUsuario = nombreUsuario;
                ViewBag.RolUsuario = rolUsuario;

                var flota = _fleetService.ObtenerFlota(columna, orden);

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

                // Validaciones básicas
                if (string.IsNullOrEmpty(fleetModel.Patente) ||
                    fleetModel.Ancho <= 0 ||
                    fleetModel.Largo <= 0 ||
                    fleetModel.Alto <= 0 ||
                    fleetModel.CapacidadPeso <= 0 ||
                    fleetModel.CapacidadVolumen <= 0 ||
                    (fleetModel.Estado < 0 || fleetModel.Estado > 1))
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

            // Pasar al layout
            ViewBag.NombreUsuario = nombreUsuario;
            ViewBag.RolUsuario = rolUsuario;
            ViewBag.patente = patente;

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessageEditFleet"] = "Ocurrio un error.";

                return View(fleetmodel);
            }

            // Validaciones básicas
            if (fleetmodel.Ancho <= 0 ||
                fleetmodel.Largo <= 0 ||
                fleetmodel.Alto <= 0 ||
                fleetmodel.CapacidadPeso <= 0 ||
                fleetmodel.CapacidadVolumen <= 0 ||
                (fleetmodel.Estado < 0 || fleetmodel.Estado > 1))
            {
                TempData["ErrorMessageEditFleet"] = "Todos los campos obligatorios deben ser completados correctamente";
                return View(fleetmodel);
            }

            //verifico que no exista vehiculo con esa patente

            var fleetDb = _fleetService.retornarFleetModelConPatente(patente);

            if (fleetDb != null)
            {
                TempData["ErrorMessageEditFleet"] = "Ya existe un vehiculo con esa patente.";

                return View(fleetmodel);
            }


            var conflicto = _fleetService.ExisteConflicto(fleetmodel);

            if (conflicto.Ancho || conflicto.Largo || conflicto.Alto ||
                conflicto.CapacidadPeso || conflicto.CapacidadVolumen || conflicto.Estado)
            {
               if (conflicto.Ancho)
                    TempData["ErrorMessageEditFleet"] = "El ancho ya está en uso por otro vehículo.";
                else if (conflicto.Largo)
                    TempData["ErrorMessageEditFleet"] = "El largo ya está en uso por otro vehículo.";
                else if (conflicto.Alto)
                    TempData["ErrorMessageEditFleet"] = "El alto ya está en uso por otro vehículo.";
                else if (conflicto.CapacidadPeso)
                    TempData["ErrorMessageEditFleet"] = "La capacidad de peso ya está en uso por otro vehículo.";
                else if (conflicto.CapacidadVolumen)
                    TempData["ErrorMessageEditFleet"] = "La capacidad de volumen ya está en uso por otro vehículo.";
                else if (conflicto.Estado)
                    TempData["ErrorMessageEditFleet"] = "El estado ya está en uso por otro vehículo.";

                return View(fleetmodel);
            }



            if (!ModelState.IsValid)
            {
                return View(fleetmodel);
            }

            ResultadoOperacion resultado = _fleetService.UpdateFleet(fleetmodel);

            if (!resultado.Exito)
            {
                TempData["ErrorMessageEditUser"] = resultado.Mensaje;
                return View(fleetmodel);
            }

            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "IP desconocida";
            _logService.AgregarLog(nombreUsuario, DateTime.Now, "Edicion flota", nombreUsuario + " Edito el vehiculo con patente : " + patente, ipAddress);

            TempData["MensajeExito"] = resultado.Mensaje;
            return View(fleetmodel);
        }




    }
}