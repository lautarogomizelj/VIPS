using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;


namespace VIPS.Web.Attributes
{
	public class AuthorizeRoleAttribute : ActionFilterAttribute
	{
		private readonly string _role;

		public AuthorizeRoleAttribute(string role)
		{
			_role = role;
		}

		public override void OnActionExecuting(ActionExecutingContext context)
		{
			var user = context.HttpContext.User;
			if (!user.Identity.IsAuthenticated || user.FindFirst(ClaimTypes.Role)?.Value != _role)
			{
				// No está autorizado, redirigir a login
				context.Result = new RedirectToActionResult("Login", "Auth", null);
			}
		}
	}
}
