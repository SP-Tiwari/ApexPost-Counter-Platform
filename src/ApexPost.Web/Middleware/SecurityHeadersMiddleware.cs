namespace ApexPost.Web.Middleware;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
	public async Task InvokeAsync(HttpContext context)
	{
		// Applied in OnStarting, just before the headers are sent, so they are
		// present even when later code (for example the exception handler)
		// clears the response headers.
		context.Response.OnStarting(static state =>
		{
			ApplyHeaders((HttpResponse)state);
			return Task.CompletedTask;
		}, context.Response);

		await next(context).ConfigureAwait(false);
	}

	public static void ApplyHeaders(HttpResponse response)
	{
		response.Headers["X-Content-Type-Options"] = "nosniff";
		response.Headers["X-Frame-Options"] = "DENY";
		response.Headers["Referrer-Policy"] = "no-referrer";
		response.Headers["Content-Security-Policy"] =
			"default-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
	}
}
