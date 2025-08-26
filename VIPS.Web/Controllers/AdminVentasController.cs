using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using VIPS.Web.Attributes;

namespace VIPS.Web.Controllers
{
    [AuthorizeRole("adminVentas")]
    public class AdminVentasController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}