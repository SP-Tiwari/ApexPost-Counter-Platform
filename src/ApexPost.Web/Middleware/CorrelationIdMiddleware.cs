using System.Diagnostics;
using Microsoft.Extensions.Primitives;

namespace ApexPost.Web.Middleware;

public sealed class CorrelationIdMiddleware(
	RequestDelegate next,
	ILogger<CorrelationIdMiddleware> logger)
{
	public const string HeaderName = "X-Correlation-ID";
	public const string HttpContextItemKey = "ApexPost.CorrelationId";
	private const int MaximumIdLength = 128;

	public async Task InvokeAsync(HttpContext context)
	{
		var correlationId = GetValidIncomingId(
			context.Request.Headers[HeaderName]) ??
			Guid.NewGuid().ToString("N");
		context.Items[HttpContextItemKey] = correlationId;
		context.Response.Headers[HeaderName] = correlationId;

		var startedAt = Stopwatch.GetTimestamp();
		using var scope = logger.BeginScope(
			new Dictionary<string, object>
			{
				["CorrelationId"] = correlationId
			});

		try
		{
			await next(context).ConfigureAwait(false);
		}
		finally
		{
			var elapsedMilliseconds =
				Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
			logger.LogInformation(
				"HTTP {RequestMethod} {RequestPath} responded {ResponseStatusCode} in {ElapsedMilliseconds} ms with correlation ID {CorrelationId}",
				context.Request.Method,
				context.Request.Path.Value ?? string.Empty,
				context.Response.StatusCode,
				elapsedMilliseconds,
				correlationId);
		}
	}

	private static string? GetValidIncomingId(StringValues values)
	{
		if (values.Count != 1)
			return null;

		var value = values[0];
		if (string.IsNullOrEmpty(value) ||
			value.Length > MaximumIdLength)
		{
			return null;
		}

		foreach (var character in value)
		{
			if (!IsAllowedCharacter(character))
				return null;
		}

		return value;
	}

	private static bool IsAllowedCharacter(char character) =>
		character is >= 'a' and <= 'z' or
			>= 'A' and <= 'Z' or
			>= '0' and <= '9' or '-' or '_' or '.';
}
