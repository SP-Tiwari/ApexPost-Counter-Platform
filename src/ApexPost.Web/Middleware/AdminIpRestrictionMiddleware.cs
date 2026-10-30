using ApexPost.Web.Security;

namespace ApexPost.Web.Middleware;

// Returns 403 for endpoints marked [RequireAdminNetwork] when the caller's IP
// is not in AdminSecurity:AllowedIpAddresses. An empty allowlist disables the
// restriction, as for ApiSecurity:AllowedIpAddresses. It runs after routing
// (to read endpoint metadata) and before authentication, so a blocked network
// is refused even before sign-in.
public sealed class AdminIpRestrictionMiddleware(
	RequestDelegate next,
	IConfiguration configuration)
{
	private readonly IpAllowlist _allowedIpAddresses =
		IpAllowlist.FromConfiguration(
			configuration, "AdminSecurity:AllowedIpAddresses");

	public async Task InvokeAsync(HttpContext context)
	{
		if (_allowedIpAddresses.IsEnabled &&
			context.GetEndpoint()?.Metadata
				.GetMetadata<RequireAdminNetworkAttribute>() is not null &&
			!_allowedIpAddresses.Allows(context.Connection.RemoteIpAddress))
		{
			context.Response.StatusCode = StatusCodes.Status403Forbidden;
			return;
		}

		await next(context).ConfigureAwait(false);
	}
}
