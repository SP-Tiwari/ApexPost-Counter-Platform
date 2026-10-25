using Microsoft.AspNetCore.Mvc.Filters;

namespace ApexPost.Web.Filters;

public sealed class PerformanceActionFilter : IAsyncActionFilter
{
	private readonly TimeSpan _slowActionThreshold;
	private readonly TimeProvider _timeProvider;
	private readonly ILogger<PerformanceActionFilter> _logger;

	public static TimeSpan ParseThresholdMilliseconds(double milliseconds)
	{
		if (!double.IsFinite(milliseconds) || milliseconds <= 0d)
		{
			throw new InvalidOperationException(
				"Performance:SlowActionThresholdMilliseconds must be a positive finite number.");
		}

		TimeSpan threshold;
		try
		{
			threshold = TimeSpan.FromMilliseconds(milliseconds);
		}
		catch (Exception exception) when (
			exception is OverflowException or ArgumentOutOfRangeException)
		{
			throw new InvalidOperationException(
				"Performance:SlowActionThresholdMilliseconds must fit within a positive TimeSpan.",
				exception);
		}

		if (threshold <= TimeSpan.Zero)
		{
			throw new InvalidOperationException(
				"Performance:SlowActionThresholdMilliseconds is too small to represent a positive TimeSpan.");
		}

		return threshold;
	}

	public PerformanceActionFilter(
		TimeSpan slowActionThreshold,
		TimeProvider timeProvider,
		ILogger<PerformanceActionFilter> logger)
	{
		if (slowActionThreshold <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(
				nameof(slowActionThreshold),
				"The slow-action threshold must be positive.");
		}

		ArgumentNullException.ThrowIfNull(timeProvider);
		ArgumentNullException.ThrowIfNull(logger);

		_slowActionThreshold = slowActionThreshold;
		_timeProvider = timeProvider;
		_logger = logger;
	}

	public async Task OnActionExecutionAsync(
		ActionExecutingContext context,
		ActionExecutionDelegate next)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(next);

		var startedAt = _timeProvider.GetTimestamp();
		try
		{
			await next().ConfigureAwait(false);
		}
		finally
		{
			var elapsed = _timeProvider.GetElapsedTime(startedAt);
			if (elapsed > _slowActionThreshold)
			{
				var routeValues = context.ActionDescriptor.RouteValues;
				routeValues.TryGetValue("controller", out var controller);
				routeValues.TryGetValue("action", out var action);
				_logger.LogWarning(
					"MVC action {Controller}.{Action} took {ElapsedMilliseconds} ms, exceeding the configured slow-action threshold of {ThresholdMilliseconds} ms.",
					controller ?? "Unknown",
					action ?? "Unknown",
					elapsed.TotalMilliseconds,
					_slowActionThreshold.TotalMilliseconds);
			}
		}
	}
}
