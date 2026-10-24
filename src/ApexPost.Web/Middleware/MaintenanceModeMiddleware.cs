
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace ApexPost.Web.Middleware;

public sealed class MaintenanceModeMiddleware(
	RequestDelegate next,
	IConfiguration configuration)
{
	public async Task InvokeAsync(HttpContext context)
	{
		var enabled = configuration.GetValue<bool>("Maintenance:Enabled");

		if (!enabled)
		{
			await next(context);
			return;
		}

		context.Response.StatusCode =
			StatusCodes.Status503ServiceUnavailable;

		context.Response.ContentType = "application/problem+json";
		context.Response.Headers["Retry-After"] = "300";

		var message = configuration["Maintenance:Message"]
			?? "The service is temporarily unavailable.";

		var problem = new
		{
			type = "about:blank",
			title = "Service temporarily unavailable",
			status = StatusCodes.Status503ServiceUnavailable,
			detail = message,
			correlationId = context.Items.TryGetValue(
				CorrelationIdMiddleware.HttpContextItemKey,
				out var correlationId)
					? correlationId?.ToString()
					: null
		};

		await JsonSerializer.SerializeAsync(
			context.Response.Body,
			problem,
			cancellationToken: context.RequestAborted);
	}
}
