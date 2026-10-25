using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using System.Text.Json;

namespace ApexPost.Web.Controllers;

[Route("[controller]/[action]")]
public sealed class AccountController : Controller
{
	private readonly StaffAuthenticationService _staffAuthentication;
	private readonly ILogger<AccountController> _logger;

	public AccountController(
		StaffAuthenticationService staffAuthentication,
		ILogger<AccountController> logger)
	{
		ArgumentNullException.ThrowIfNull(staffAuthentication);
		ArgumentNullException.ThrowIfNull(logger);
		_staffAuthentication = staffAuthentication;
		_logger = logger;
	}

	[AllowAnonymous]
	[HttpGet]
	public IActionResult SignIn(string? returnUrl) =>
		View(new StaffSignInViewModel { ReturnUrl = returnUrl });

	[AllowAnonymous]
	[HttpPost]
	[EnableRateLimiting("staff-sign-in")]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> SignIn(
		StaffSignInViewModel model,
		CancellationToken cancellationToken)
	{
		if (!ModelState.IsValid)
		{
			ModelState.Remove(nameof(model.Password));
			model.Password = string.Empty;
			Response.StatusCode = StatusCodes.Status400BadRequest;
			return View(model);
		}

		Staff? staff;
		try
		{
			staff = await _staffAuthentication.AuthenticateAsync(
				model.StaffId, model.Password, cancellationToken);
		}
		catch (Exception exception) when (
			exception is IOException or InvalidDataException or JsonException)
		{
			_logger.LogError(
				exception,
				"Staff sign-in could not read credential or staff data.");
			ModelState.Remove(nameof(model.Password));
			model.Password = string.Empty;
			ModelState.AddModelError(
				string.Empty,
				"Staff sign-in is temporarily unavailable.");
			Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
			return View(model);
		}

		if (staff is null)
		{
			ModelState.Remove(nameof(model.Password));
			model.Password = string.Empty;
			ModelState.AddModelError(
				string.Empty, "Staff ID or password is incorrect.");
			Response.StatusCode = StatusCodes.Status401Unauthorized;
			return View(model);
		}

		var claims = new[]
		{
			new Claim("StaffId", staff.Id),
			new Claim("Role", staff.Role.ToString()),
			new Claim("BranchCode", staff.HomeBranchCode.Value),
			new Claim(ClaimTypes.Name, staff.Name)
		};
		var identity = new ClaimsIdentity(
			claims,
			CookieAuthenticationDefaults.AuthenticationScheme,
			"StaffId",
			"Role");
		var properties = new AuthenticationProperties
		{
			IsPersistent = true,
			ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
			AllowRefresh = true
		};
		await HttpContext.SignInAsync(
			CookieAuthenticationDefaults.AuthenticationScheme,
			new ClaimsPrincipal(identity),
			properties);

		if (!string.IsNullOrEmpty(model.ReturnUrl) &&
			Url.IsLocalUrl(model.ReturnUrl))
		{
			return LocalRedirect(model.ReturnUrl);
		}

		return RedirectToAction("Index", "Home");
	}

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
