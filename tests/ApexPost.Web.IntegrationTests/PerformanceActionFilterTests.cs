using ApexPost.Web.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Routing;

namespace ApexPost.Web.IntegrationTests;

public sealed class PerformanceActionFilterTests
{
	[Theory]
	[InlineData(0d)]
	[InlineData(-1d)]
	[InlineData(double.NaN)]
	[InlineData(double.PositiveInfinity)]
	[InlineData(double.NegativeInfinity)]
	public void ParseThresholdMilliseconds_RejectsNonPositiveOrNonFiniteValues(
		double milliseconds)
	{
		Assert.Throws<InvalidOperationException>(
			() => PerformanceActionFilter.ParseThresholdMilliseconds(milliseconds));
	}

	[Fact]
	public void ParseThresholdMilliseconds_RejectsValueThatRoundsToZero()
	{
		Assert.Throws<InvalidOperationException>(
			() => PerformanceActionFilter.ParseThresholdMilliseconds(1e-10));
	}

	[Fact]
	public void ParseThresholdMilliseconds_RejectsValueOutsideTimeSpanRange()
	{
		Assert.Throws<InvalidOperationException>(
			() => PerformanceActionFilter.ParseThresholdMilliseconds(double.MaxValue));
	}

	[Fact]
	public void ParseThresholdMilliseconds_AcceptsConfiguredDefault()
	{
		Assert.Equal(
			TimeSpan.FromMilliseconds(500d),
			PerformanceActionFilter.ParseThresholdMilliseconds(500d));
	}

	[Fact]
	public async Task ActionWithinThreshold_DoesNotLogWarning()
	{
		var clock = new ManualTimeProvider();
		var logger = new RecordingLogger<PerformanceActionFilter>();
		var filter = CreateFilter(clock, logger);
		var context = CreateContext();

		await filter.OnActionExecutionAsync(context, () =>
		{
			clock.Advance(TimeSpan.FromMilliseconds(50));
			return Task.FromResult(CreateExecutedContext(context));
		});

		Assert.Empty(logger.Entries);
	}

	[Fact]
	public async Task SlowAction_LogsStructuredWarningWithoutRequestData()
	{
		var clock = new ManualTimeProvider();
		var logger = new RecordingLogger<PerformanceActionFilter>();
		var filter = CreateFilter(clock, logger);
		var context = CreateContext();

		await filter.OnActionExecutionAsync(context, () =>
		{
			clock.Advance(TimeSpan.FromMilliseconds(150));
			return Task.FromResult(CreateExecutedContext(context));
		});

		var entry = Assert.Single(logger.Entries);
		Assert.Equal(LogLevel.Warning, entry.Level);
		Assert.Contains("Transactions.Create", entry.Message, StringComparison.Ordinal);
		Assert.Contains("150", entry.Message, StringComparison.Ordinal);
		Assert.DoesNotContain("private-value", entry.Message, StringComparison.Ordinal);
		Assert.Contains(entry.Properties, property =>
			property.Key == "Controller" && Equals(property.Value, "Transactions"));
		Assert.Contains(entry.Properties, property =>
			property.Key == "Action" && Equals(property.Value, "Create"));
		Assert.Contains(entry.Properties, property =>
			property.Key == "ElapsedMilliseconds" &&
			Equals(property.Value, 150d));
		Assert.Contains(entry.Properties, property =>
			property.Key == "ThresholdMilliseconds" &&
			Equals(property.Value, 100d));
	}

	[Fact]
	public async Task FailedSlowAction_StillLogsTimingAndPropagatesException()
	{
		var clock = new ManualTimeProvider();
		var logger = new RecordingLogger<PerformanceActionFilter>();
		var filter = CreateFilter(clock, logger);
		var context = CreateContext();

		var exception = await Assert.ThrowsAsync<InvalidOperationException>(
			() => filter.OnActionExecutionAsync(context, () =>
			{
				clock.Advance(TimeSpan.FromMilliseconds(150));
				throw new InvalidOperationException("private-value");
			}));

		Assert.Equal("private-value", exception.Message);
		Assert.Equal(LogLevel.Warning, Assert.Single(logger.Entries).Level);
		Assert.DoesNotContain(
			"private-value",
			Assert.Single(logger.Entries).Message,
			StringComparison.Ordinal);
	}

	private static PerformanceActionFilter CreateFilter(
		TimeProvider timeProvider,
		ILogger<PerformanceActionFilter> logger) =>
		new(TimeSpan.FromMilliseconds(100), timeProvider, logger);

	private static ActionExecutingContext CreateContext()
	{
		var descriptor = new ControllerActionDescriptor
		{
			ControllerName = "Transactions",
			ActionName = "Create",
			RouteValues = new Dictionary<string, string?>
			{
				["controller"] = "Transactions",
				["action"] = "Create"
			}
		};
		var actionContext = new ActionContext(
			new DefaultHttpContext(),
			new RouteData(),
			descriptor);
		return new ActionExecutingContext(
			actionContext,
			new List<IFilterMetadata>(),
			new Dictionary<string, object?>(),
			new object());
	}

	private static ActionExecutedContext CreateExecutedContext(
		ActionExecutingContext context) =>
		new(context, context.Filters, context.Controller);

	private sealed class ManualTimeProvider : TimeProvider
	{
		private long _timestamp;

		public override long TimestampFrequency => TimeSpan.TicksPerSecond;

		public override long GetTimestamp() =>
			Interlocked.Read(ref _timestamp);

		public void Advance(TimeSpan duration) =>
			Interlocked.Add(ref _timestamp, duration.Ticks);
	}

	private sealed class RecordingLogger<T> : ILogger<T>
	{
		public List<LogEntry> Entries { get; } = [];

		public IDisposable? BeginScope<TState>(TState state)
			where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter)
		{
			var properties = state is IEnumerable<
				KeyValuePair<string, object?>> structuredState
				? structuredState.ToArray()
				: [];
			Entries.Add(new LogEntry(
				logLevel,
				formatter(state, exception),
				properties));
		}
	}

	private sealed record LogEntry(
		LogLevel Level,
		string Message,
		IReadOnlyList<KeyValuePair<string, object?>> Properties);
}
