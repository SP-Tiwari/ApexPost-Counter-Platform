using System.Collections.Concurrent;
using ApexPost.Web.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ApexPost.Web.IntegrationTests;

public sealed class CorrelationIdMiddlewareTests : IAsyncLifetime
{
	private string _storageDirectory = null!;
	private string _rootDirectory = null!;
	private WebApplicationFactory<Program> _factory = null!;
	private readonly CapturedLogProvider _logs = new();

	public Task InitializeAsync()
	{
		_rootDirectory = Path.Combine(
			Path.GetTempPath(), $"ApexPost-Correlation-{Guid.NewGuid():N}");
		Directory.CreateDirectory(_rootDirectory);
		_storageDirectory = Path.Combine(_rootDirectory, "domain-data");
		_factory = new ApexPostWebApplicationFactory()
			.WithWebHostBuilder(builder =>
			{
				builder.UseSetting("Storage:Directory", _storageDirectory);
				builder.UseSetting(
					"Authentication:CredentialFile",
					Path.Combine(_rootDirectory, "credentials.json"));
				builder.ConfigureLogging(logging =>
					logging.AddProvider(_logs));
			});
		return Task.CompletedTask;
	}

	public async Task DisposeAsync()
	{
		await _factory.DisposeAsync();
		_logs.Dispose();
		if (Directory.Exists(_rootDirectory))
			Directory.Delete(_rootDirectory, recursive: true);
	}

	[Fact]
	public async Task Request_WithoutCorrelationId_GeneratesAndLogsId()
	{
		var client = _factory.CreateClient();

		var response = await client.GetAsync("/");

		Assert.True(response.Headers.TryGetValues(
			CorrelationIdMiddleware.HeaderName, out var values));
		var correlationId = Assert.Single(values!);
		Assert.Matches("^[0-9a-f]{32}$", correlationId);
		AssertRequestLog(response.StatusCode, correlationId);
	}

	[Fact]
	public async Task Request_WithValidCorrelationId_PreservesItInResponseAndLog()
	{
		const string correlationId = "trace-id_123.test";
		var client = _factory.CreateClient();
		client.DefaultRequestHeaders.Add(
			CorrelationIdMiddleware.HeaderName, correlationId);

		var response = await client.GetAsync("/");

		Assert.Equal(
			correlationId,
			Assert.Single(response.Headers.GetValues(
				CorrelationIdMiddleware.HeaderName)));
		AssertRequestLog(response.StatusCode, correlationId);
	}

	[Theory]
	[InlineData("invalid value")]
	public async Task Request_WithInvalidCorrelationId_GeneratesSafeId(
		string suppliedId)
	{
		var client = _factory.CreateClient();
		client.DefaultRequestHeaders.Add(
			CorrelationIdMiddleware.HeaderName, suppliedId);

		var response = await client.GetAsync("/");

		var correlationId = Assert.Single(response.Headers.GetValues(
			CorrelationIdMiddleware.HeaderName));
		Assert.NotEqual(suppliedId, correlationId);
		Assert.Matches("^[0-9a-f]{32}$", correlationId);
		AssertRequestLog(response.StatusCode, correlationId);
	}

	[Fact]
	public async Task Request_WithOversizedCorrelationId_GeneratesSafeId()
	{
		var client = _factory.CreateClient();
		client.DefaultRequestHeaders.Add(
			CorrelationIdMiddleware.HeaderName, new string('x', 129));

		var response = await client.GetAsync("/");

		var correlationId = Assert.Single(response.Headers.GetValues(
			CorrelationIdMiddleware.HeaderName));
		Assert.NotEqual(new string('x', 129), correlationId);
		Assert.Matches("^[0-9a-f]{32}$", correlationId);
		AssertRequestLog(response.StatusCode, correlationId);
	}

	[Fact]
	public async Task Request_WithMultipleCorrelationIds_GeneratesNewId()
	{
		var client = _factory.CreateClient();
		client.DefaultRequestHeaders.TryAddWithoutValidation(
			CorrelationIdMiddleware.HeaderName,
			["first-id", "second-id"]);

		var response = await client.GetAsync("/");

		var correlationId = Assert.Single(response.Headers.GetValues(
			CorrelationIdMiddleware.HeaderName));
		Assert.False(new[] { "first-id", "second-id" }.Contains(correlationId));
		Assert.Matches("^[0-9a-f]{32}$", correlationId);
		AssertRequestLog(response.StatusCode, correlationId);
	}

	[Fact]
	public async Task DownstreamException_RetainsCorrelationIdAndLogsRequest()
	{
		const string correlationId = "exception-trace-42";
		var context = new DefaultHttpContext();
		context.Request.Method = "POST";
		context.Request.Path = "/exception-test";
		context.Request.Headers[CorrelationIdMiddleware.HeaderName] =
			correlationId;
		var observedCorrelationId = string.Empty;
		using var loggerFactory = LoggerFactory.Create(logging =>
			logging.AddProvider(_logs));
		var logger = loggerFactory.CreateLogger<CorrelationIdMiddleware>();
		var middleware = new CorrelationIdMiddleware(
			downstream =>
			{
				observedCorrelationId =
					downstream.Items[CorrelationIdMiddleware.HttpContextItemKey]
						?.ToString() ?? string.Empty;
				Assert.Equal(
					correlationId,
					downstream.Response.Headers[
						CorrelationIdMiddleware.HeaderName].ToString());
				downstream.Response.StatusCode =
					StatusCodes.Status500InternalServerError;
				throw new InvalidOperationException("Test downstream failure.");
			},
			logger);

		await Assert.ThrowsAsync<InvalidOperationException>(
			() => middleware.InvokeAsync(context));

		Assert.Equal(correlationId, observedCorrelationId);
		Assert.Equal(
			correlationId,
			context.Items[CorrelationIdMiddleware.HttpContextItemKey]);
		var log = Assert.Single(
			_logs.Entries,
			entry => entry.Category ==
				typeof(CorrelationIdMiddleware).FullName);
		Assert.Equal("POST", log.Properties["RequestMethod"]);
		Assert.Equal("/exception-test", log.Properties["RequestPath"]);
		Assert.Equal(
			StatusCodes.Status500InternalServerError,
			log.Properties["ResponseStatusCode"]);
		Assert.Equal(correlationId, log.Properties["CorrelationId"]);
	}

	private void AssertRequestLog(
		System.Net.HttpStatusCode responseStatusCode,
		string correlationId)
	{
		var log = Assert.Single(
			_logs.Entries,
			entry => entry.Category ==
				typeof(CorrelationIdMiddleware).FullName);
		Assert.Equal(LogLevel.Information, log.LogLevel);
		Assert.Equal("GET", log.Properties["RequestMethod"]);
		Assert.Equal("/", log.Properties["RequestPath"]);
		Assert.Equal((int)responseStatusCode, log.Properties["ResponseStatusCode"]);
		Assert.Equal(correlationId, log.Properties["CorrelationId"]);
		Assert.IsType<double>(log.Properties["ElapsedMilliseconds"]);
	}

	private sealed class CapturedLogProvider : ILoggerProvider
	{
		private readonly ConcurrentQueue<CapturedLogEntry> _entries = new();

		public IReadOnlyCollection<CapturedLogEntry> Entries => _entries.ToArray();

		public ILogger CreateLogger(string categoryName) =>
			new CapturedLogger(categoryName, _entries);

		public void Dispose()
		{
		}
	}

	private sealed class CapturedLogger(
		string categoryName,
		ConcurrentQueue<CapturedLogEntry> entries) : ILogger
	{
		public IDisposable? BeginScope<TState>(TState state)
			where TState : notnull => NullScope.Instance;

		public bool IsEnabled(LogLevel logLevel) =>
			logLevel != LogLevel.None;

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter)
		{
			if (state is not IEnumerable<KeyValuePair<string, object?>> values)
				return;

			entries.Enqueue(new CapturedLogEntry(
				categoryName,
				logLevel,
				values.ToDictionary(
					pair => pair.Key,
					pair => pair.Value,
					StringComparer.Ordinal)));
		}
	}

	private sealed record CapturedLogEntry(
		string Category,
		LogLevel LogLevel,
		IReadOnlyDictionary<string, object?> Properties);

	private sealed class NullScope : IDisposable
	{
		public static NullScope Instance { get; } = new();

		public void Dispose()
		{
		}
	}
}
