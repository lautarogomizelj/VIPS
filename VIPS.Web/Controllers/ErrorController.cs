using Microsoft.AspNetCore.Mvc;

namespace VIPS.Web.Controllers
{
    public class ErrorController : Controller
    {
        // Ruta que manejará los errores globales
        [Route("Error")]
        public IActionResult General()
        {
            return View();
        }
    }
}
