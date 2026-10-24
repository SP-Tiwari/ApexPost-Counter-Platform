namespace ApexPost.Web.Middleware;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
	public async Task InvokeAsync(HttpContext context)
	{
		context.Response.Headers["X-Content-Type-Options"] = "nosniff";
		context.Response.Headers["X-Frame-Options"] = "DENY";
		context.Response.Headers["Referrer-Policy"] = "no-referrer";
		context.Response.Headers["Content-Security-Policy"] =
			"default-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";

		await next(context).ConfigureAwait(false);
	}
}
