using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

[Route("[controller]/[action]")]
public sealed class AccountController : Controller
{
	[AllowAnonymous]
	[HttpGet]
	public IActionResult SignIn() => View();

	[AllowAnonymous]
	[HttpGet]
	public IActionResult AccessDenied() => View();

	[Authorize]
	[HttpPost]
	[ActionName("SignOut")]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> SignOutPost()
	{
		await HttpContext.SignOutAsync(
			CookieAuthenticationDefaults.AuthenticationScheme);
		return RedirectToAction(nameof(SignIn));
	}
}
