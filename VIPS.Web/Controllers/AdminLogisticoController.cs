using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using VIPS.Web.Attributes;

namespace VIPS.Web.Controllers
{
    [AuthorizeRole("adminLogistico")]
    public class AdminLogisticoController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}