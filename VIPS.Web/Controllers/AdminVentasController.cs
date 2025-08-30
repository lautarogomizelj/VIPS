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
    [AuthorizeRole("adminVentas")]
    public class AdminVentasController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IHashService _hashService;
        private readonly LogService _logService;
        private readonly UserService _userService;
        private readonly ClientService _clientService;
        private readonly OrderService _orderService;


        public AdminVentasController(IConfiguration configuration, IHashService hashService, LogService logService, ClientService clientService, OrderService orderService, UserService userService)
        {
            _configuration = configuration;
            _hashService = hashService;
            _clientService = clientService;
            _userService = userService;
            _logService = logService;
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
        public IActionResult ClientManagement(string columna = "fechaCreacion", string orden = "desc")
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

                var clientes = _clientService.ObtenerClientes(columna, orden);

                return View(clientes);
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error al cargar los usuarios";
                return View(new List<UsuarioViewModel>());
            }

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
        public IActionResult CreateClient()
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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateClient(ClienteModel clienteModel)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    TempData["ErrorMessageCreateClient"] = "Ocurrio un error.";

                    return View(clienteModel);
                }

                // Validaciones básicas
                if (string.IsNullOrEmpty(clienteModel.Nombre) ||
                    string.IsNullOrEmpty(clienteModel.Apellido) ||
                    string.IsNullOrEmpty(clienteModel.Email) ||
                    string.IsNullOrEmpty(clienteModel.Telefono) ||
                    string.IsNullOrEmpty(clienteModel.DomicilioLegal) ||
                    string.IsNullOrEmpty(clienteModel.Dni))
                {
                    TempData["ErrorMessageCreateClient"] = "Todos los campos obligatorios deben ser completados";

                    return View(clienteModel);
                }

                // Verificar si el DNI ya existe

                var dniExistente = _clientService.VerificarDniExistente(clienteModel.Dni);
                if (dniExistente)
                {
                    TempData["ErrorMessageCreateClient"] = "Dni invalido, ingrese devuelta..";

                    return View(clienteModel);
                }


                // Verificar si el email ya existe

                var emailExistente = _clientService.VerificarEmailExistente(clienteModel.Email);
                if (emailExistente)
                {
                    TempData["ErrorMessageCreateClient"] = "Email invalido, ingrese devuelta.";

                    return View(clienteModel);
                }


                var resultado = _clientService.CrearCliente(clienteModel);


                if (!resultado.Exito)
                {
                    // Mostrar error en la misma vista
                    TempData["ErrorMessageCreateClient"] = resultado.Mensaje;

                    return View(clienteModel);
                }

                var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
                string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "IP desconocida";
                _logService.AgregarLog(nombreUsuario, DateTime.Now, "Crear cliente", "Se creo cliente con nombre: " + clienteModel.Nombre, ipAddress);

                TempData["MensajeExitoFormularioCrearCliente"] = resultado.Mensaje;
                return RedirectToAction("CreateClient");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessageCreateClient"] = $"Error interno del servidor: {ex.Message}";
                return View(clienteModel);
            }
        }

        [HttpGet("EditClient/{idCliente}")]
        public IActionResult EditClient(int idCliente)
        {
            // Leer claims desde la cookie
            var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
            var rolUsuario = User.FindFirstValue(ClaimTypes.Role);

            // Pasar al layout
            ViewBag.NombreUsuario = nombreUsuario;
            ViewBag.RolUsuario = rolUsuario;

            ViewBag.idCliente = idCliente;

            return View(_clientService.retornarClienteModelConIdCliente(idCliente));
        }

        [HttpPost("EditClient/{idCliente}")]
        [ValidateAntiForgeryToken]
        public IActionResult EditClient(int idCliente, ClienteModel clienteModel)
        {
            // Leer claims desde la cookie
            var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
            var rolUsuario = User.FindFirstValue(ClaimTypes.Role);

            // Pasar al layout
            ViewBag.NombreUsuario = nombreUsuario;
            ViewBag.RolUsuario = rolUsuario;

            ViewBag.idCliente = idCliente;


            if (!ModelState.IsValid)
            {
                TempData["ErrorMessageEditClient"] = "Ocurrio un error.";

                return View(clienteModel);
            }

            // Validaciones básicas
            if (string.IsNullOrEmpty(clienteModel.Dni) ||
                string.IsNullOrEmpty(clienteModel.Nombre) ||
                string.IsNullOrEmpty(clienteModel.Apellido) ||
                string.IsNullOrEmpty(clienteModel.Email) ||
                string.IsNullOrEmpty(clienteModel.Telefono) ||
                string.IsNullOrEmpty(clienteModel.DomicilioLegal))

            {
                TempData["ErrorMessageEditClient"] = "Todos los campos obligatorios deben ser completados";

                return View(clienteModel);
            }



            var clienteDb = _clientService.retornarClienteModelConIdCliente(idCliente);

            if (clienteDb == null)
            {
                TempData["ErrorMessageEditClient"] = "El cliente no existe.";

                return View(clienteModel);
            }


            var conflicto = _clientService.ExisteConflicto(clienteModel);

            if (conflicto.Dni || conflicto.Email || conflicto.Telefono || conflicto.DomicilioLegal)
            {
                if (conflicto.Dni) TempData["ErrorMessageEditClient"] = "El DNI ya está en uso por otro cliente.";
                else if (conflicto.Email) TempData["ErrorMessageEditClient"] = "El correo ya está en uso.";
                else if (conflicto.Telefono) TempData["ErrorMessageEditClient"] = "El teléfono ya está en uso.";
                else if (conflicto.DomicilioLegal) TempData["ErrorMessageEditClient"] = "El domicilio legal ya está en uso.";

                return View(clienteModel);
            }




            if (!ModelState.IsValid)
            {
                return View(clienteModel);
            }

            ResultadoOperacion resultado = _clientService.UpdateClient(clienteModel);

            if (!resultado.Exito)
            {
                TempData["ErrorMessageEditUser"] = resultado.Mensaje;
                return View(clienteModel);
            }

            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "IP desconocida";
            _logService.AgregarLog(nombreUsuario, DateTime.Now, "Edicion cliente", nombreUsuario + " Edito el cliente con id: " + idCliente, ipAddress);

            TempData["MensajeExito"] = resultado.Mensaje;
            return View(clienteModel);
        }

        [HttpGet("DeleteClient/{idCliente}")]
        public IActionResult DeleteClient(int idCliente)
        {
            // Leer claims desde la cookie
            var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
            var rolUsuario = User.FindFirstValue(ClaimTypes.Role);

            // Pasar al layout
            ViewBag.NombreUsuario = nombreUsuario;
            ViewBag.RolUsuario = rolUsuario;

            ViewBag.idCliente = idCliente;

            Console.WriteLine(idCliente);

            return View();
        }
        
        [HttpPost("DeleteClient/{idCliente}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteClient(string eleccion, int idCliente)
        {
            // Leer claims desde la cookie
            var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
            var rolUsuario = User.FindFirstValue(ClaimTypes.Role);

            // Pasar al layout
            ViewBag.NombreUsuario = nombreUsuario;
            ViewBag.RolUsuario = rolUsuario;

            if (eleccion == "no")
            {
                TempData["ErrorMessageDeleteClient"] = "Ocurrio un error.";
                ViewBag.idCliente = idCliente;

                return View();
            }

            Console.WriteLine("id cliente: " + idCliente);
            var clienteDb = _clientService.retornarClienteModelConIdCliente(idCliente);

            if (clienteDb == null)
            {
                TempData["ErrorMessageDeleteClient"] = "El cliente no existe.";
                ViewBag.idCliente = idCliente;
                return View();
            }


            var resultado = _clientService.EliminarCliente(idCliente);


            if (!resultado.Exito)
            {
                // Mostrar error en la misma vista
                ViewBag.idCliente = idCliente;
                TempData["ErrorMessageDeleteUser"] = $"Error al eliminar usuario: {resultado.Mensaje}";
            }

            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "IP desconocida";
            _logService.AgregarLog(nombreUsuario, DateTime.Now, "Edicion cuenta", nombreUsuario + " Elimino el cliente con id: " + idCliente, ipAddress);

            TempData["SuccessDeleteMessage"] = $"Id Cliente '{idCliente}' eliminado correctamente";
            return View();
        }

      




        [HttpGet]
        public IActionResult CreateOrder()
        {
            // Leer claims desde la cookie
            var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
            var rolUsuario = User.FindFirstValue(ClaimTypes.Role);

            // Pasar al layout
            ViewBag.NombreUsuario = nombreUsuario;
            ViewBag.RolUsuario = rolUsuario;

            //Pasar todos los dni de clientes
            ViewBag.Clientes = _clientService.ObtenerListaNombreyDni();


            return View();
        }


        [HttpGet("DeleteOrder/{idPedido}")]
        public IActionResult DeleteOrder(int idPedido)
        {
            // Leer claims desde la cookie
            var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
            var rolUsuario = User.FindFirstValue(ClaimTypes.Role);

            // Pasar al layout
            ViewBag.NombreUsuario = nombreUsuario;
            ViewBag.RolUsuario = rolUsuario;
            ViewBag.idPedido = idPedido;

            return View();
        }

        [HttpGet("EditOrder/{idPedido}")]
        public IActionResult EditOrder(int idPedido)
        {
            // Leer claims desde la cookie
            var nombreUsuario = User.FindFirstValue(ClaimTypes.Name);
            var rolUsuario = User.FindFirstValue(ClaimTypes.Role);

            // Pasar al layout
            ViewBag.NombreUsuario = nombreUsuario;
            ViewBag.RolUsuario = rolUsuario;
            ViewBag.idPedido = idPedido;

            return View();
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
    }
}