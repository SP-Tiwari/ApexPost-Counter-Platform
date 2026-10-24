using ApexPost.Application.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Text.Json;

namespace ApexPost.Web.Authentication;

public sealed class StaffCookieAuthenticationEvents(
	StaffAuthenticationService staffAuthentication,
	ILogger<StaffCookieAuthenticationEvents> logger)
	: CookieAuthenticationEvents
{
	public override async Task ValidatePrincipal(
		CookieValidatePrincipalContext context)
	{
		var staffId = context.Principal?.FindFirst("StaffId")?.Value;
		var role = context.Principal?.FindFirst("Role")?.Value;
		var branchCode = context.Principal?.FindFirst("BranchCode")?.Value;

		try
		{
			var staff = await staffAuthentication.FindActiveStaffAsync(
				staffId ?? string.Empty,
				context.HttpContext.RequestAborted).ConfigureAwait(false);
			if (staff is not null &&
				string.Equals(staff.Id, staffId, StringComparison.Ordinal) &&
				string.Equals(
					staff.Role.ToString(), role, StringComparison.Ordinal) &&
				string.Equals(
					staff.HomeBranchCode.Value,
					branchCode,
					StringComparison.Ordinal))
			{
				return;
			}

			await RejectPrincipalAsync(context).ConfigureAwait(false);
		}
		catch (Exception exception) when (
			exception is IOException or InvalidDataException or JsonException)
		{
			logger.LogError(
				exception,
				"Staff cookie could not be validated against current staff data.");
			await RejectPrincipalAsync(context).ConfigureAwait(false);
		}
	}

	private static async Task RejectPrincipalAsync(
		CookieValidatePrincipalContext context)
	{
		context.RejectPrincipal();
		await context.HttpContext.SignOutAsync(
			CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
	}
}
