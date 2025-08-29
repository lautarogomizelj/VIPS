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
        private readonly ClientService _clientService;

        public AdminVentasController(IConfiguration configuration, IHashService hashService, LogService logService, ClientService clientService)
        {
            _configuration = configuration;
            _hashService = hashService;
            _clientService = clientService;
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
            } catch(Exception ex)
            {
                ViewBag.Error = "Error al cargar los usuarios";
                return View(new List<UsuarioViewModel>());
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
    }
}