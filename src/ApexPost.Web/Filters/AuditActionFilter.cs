using System.Runtime.ExceptionServices;
using ApexPost.Application.Auditing;
using ApexPost.Web.Middleware;
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

		var record = new AuditRecord(
			DateTimeOffset.UtcNow,
			context.ActionDescriptor.RouteValues["controller"] ?? "Unknown",
			context.ActionDescriptor.RouteValues["action"] ?? "Unknown",
			outcome,
			context.HttpContext.User.FindFirst("StaffId")?.Value,
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
}
