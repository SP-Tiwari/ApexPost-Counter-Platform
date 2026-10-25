using System.Runtime.ExceptionServices;
using System.Diagnostics;
using ApexPost.Application.Auditing;
using ApexPost.Web.Middleware;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ApexPost.Web.Filters;

public sealed class AuditActionFilter(
	IAuditStore auditStore,
	ILogger<AuditActionFilter> logger) : IAsyncActionFilter
{
	public async Task OnActionExecutionAsync(
		ActionExecutingContext context,
		ActionExecutionDelegate next)
	{
		var startedAt = Stopwatch.GetTimestamp();
		ActionExecutedContext? executedContext = null;
		Exception? escapedException = null;

		try
		{
			executedContext = await next().ConfigureAwait(false);
		}
		catch (Exception exception)
		{
			escapedException = exception;
		}

		var outcome = escapedException is not null ||
			executedContext?.Exception is not null
				? "Failed"
				: executedContext?.Canceled == true
					? "Canceled"
					: "Succeeded";
		var actionFailed = escapedException is not null ||
			executedContext?.Exception is not null;
		var elapsedMilliseconds =
			Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
		var controller =
			context.ActionDescriptor.RouteValues["controller"] ?? "Unknown";
		var action =
			context.ActionDescriptor.RouteValues["action"] ?? "Unknown";
		var statusCode = ResolveStatusCode(
			executedContext,
			context.HttpContext.Response.StatusCode,
			actionFailed);

		var record = new AuditRecord(
			DateTimeOffset.UtcNow,
			controller,
			action,
			$"Executed {controller}.{action}",
			outcome,
			statusCode,
			elapsedMilliseconds,
			GetSanitizedRouteValues(context),
			context.HttpContext.User.FindFirst("StaffId")?.Value,
			context.HttpContext.User.FindFirst("Role")?.Value,
			context.HttpContext.User.FindFirst("BranchCode")?.Value,
			context.HttpContext.Items.TryGetValue(
				CorrelationIdMiddleware.HttpContextItemKey,
				out var correlationId)
					? correlationId?.ToString()
					: null);

		try
		{
			await auditStore.AppendAsync(
				record,
				context.HttpContext.RequestAborted).ConfigureAwait(false);
		}
		catch (Exception auditException) when (actionFailed)
		{
			logger.LogError(
				auditException,
				"Failed to append audit record while preserving an MVC action exception.");
		}

		if (escapedException is not null)
		{
			ExceptionDispatchInfo.Capture(escapedException).Throw();
		}
	}

	private static int ResolveStatusCode(
		ActionExecutedContext? executedContext,
		int responseStatusCode,
		bool actionFailed)
	{
		if (executedContext?.Result is IStatusCodeActionResult statusResult &&
			statusResult.StatusCode is int statusCode)
		{
			return statusCode;
		}

		if (actionFailed && responseStatusCode == StatusCodes.Status200OK)
			return StatusCodes.Status500InternalServerError;

		return responseStatusCode;
	}

	private static IReadOnlyDictionary<string, string> GetSanitizedRouteValues(
		ActionExecutingContext context)
	{
		var sanitized = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (var (key, value) in context.RouteData.Values)
		{
			if (!IsAllowedRouteKey(key) || value is null)
				continue;

			var text = value.ToString();
			if (string.IsNullOrWhiteSpace(text))
				continue;

			sanitized[key] = text.Trim();
		}

		return sanitized;
	}

	private static bool IsAllowedRouteKey(string key)
	{
		if (string.IsNullOrWhiteSpace(key))
			return false;

		if (IsSensitiveRouteKey(key))
			return false;

		return key.Equals("controller", StringComparison.OrdinalIgnoreCase) ||
			key.Equals("action", StringComparison.OrdinalIgnoreCase) ||
			key.Equals("id", StringComparison.OrdinalIgnoreCase) ||
			key.Equals("transactionId", StringComparison.OrdinalIgnoreCase) ||
			key.Equals("branchCode", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsSensitiveRouteKey(string key) =>
		key.Contains("password", StringComparison.OrdinalIgnoreCase) ||
		key.Contains("api", StringComparison.OrdinalIgnoreCase) &&
			key.Contains("key", StringComparison.OrdinalIgnoreCase) ||
		key.Contains("token", StringComparison.OrdinalIgnoreCase) ||
		key.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
		key.Contains("credential", StringComparison.OrdinalIgnoreCase) ||
		key.Contains("document", StringComparison.OrdinalIgnoreCase) ||
		key.Contains("card", StringComparison.OrdinalIgnoreCase);
}
