
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ApexPost.Web.IntegrationTests;

public sealed class MaintenanceModeMiddlewareTests : IAsyncLifetime
{
	private const string TestPath = "/__test/maintenance-probe";

	private const string MaintenanceMessage =
		"ApexPost is temporarily unavailable for maintenance. Please try again later.";

	private string _rootDirectory = null!;
	private WebApplicationFactory<Program> _enabledFactory = null!;
	private WebApplicationFactory<Program> _disabledFactory = null!;

	public Task InitializeAsync()
	{
		_rootDirectory = Path.Combine(
			Path.GetTempPath(),
			$"ApexPost-Maintenance-{Guid.NewGuid():N}");

		Directory.CreateDirectory(_rootDirectory);

		_enabledFactory = CreateFactory(
			maintenanceEnabled: true,
			"enabled");

		_disabledFactory = CreateFactory(
			maintenanceEnabled: false,
			"disabled");

		return Task.CompletedTask;
	}

	public async Task DisposeAsync()
	{
		await _enabledFactory.DisposeAsync();
		await _disabledFactory.DisposeAsync();

		if (Directory.Exists(_rootDirectory))
		{
			Directory.Delete(_rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task MaintenanceEnabled_Returns503ServiceUnavailable()
	{
		using var client = _enabledFactory.CreateClient();

		var response = await client.GetAsync(TestPath);

		Assert.Equal(
			HttpStatusCode.ServiceUnavailable,
			response.StatusCode);

		Assert.StartsWith(
			"application/problem+json",
			response.Content.Headers.ContentType?.ToString());
	}

	[Fact]
	public async Task MaintenanceEnabled_ReturnsConfiguredMessage()
	{
		using var client = _enabledFactory.CreateClient();

		var response = await client.GetAsync(TestPath);
		var body = await response.Content.ReadAsStringAsync();

		using var problem = JsonDocument.Parse(body);

		Assert.Equal(
			503,
			problem.RootElement.GetProperty("status").GetInt32());

		Assert.Equal(
			"Service temporarily unavailable",
			problem.RootElement.GetProperty("title").GetString());

		Assert.Equal(
			MaintenanceMessage,
			problem.RootElement.GetProperty("detail").GetString());
	}

	[Fact]
	public async Task MaintenanceEnabled_IncludesCorrelationAndSecurityHeaders()
	{
		const string correlationId = "maintenance-test-correlation";

		using var client = _enabledFactory.CreateClient();

		client.DefaultRequestHeaders.Add(
			"X-Correlation-ID",
			correlationId);

		var response = await client.GetAsync(TestPath);
		var body = await response.Content.ReadAsStringAsync();

		using var problem = JsonDocument.Parse(body);

		Assert.Equal(
			correlationId,
			problem.RootElement.GetProperty("correlationId").GetString());

		Assert.Equal(
			correlationId,
			Assert.Single(
				response.Headers.GetValues("X-Correlation-ID")));

		Assert.Equal(
			"nosniff",
			Assert.Single(
				response.Headers.GetValues("X-Content-Type-Options")));

		Assert.Equal(
			"DENY",
			Assert.Single(
				response.Headers.GetValues("X-Frame-Options")));

		Assert.Equal(
			"no-referrer",
			Assert.Single(
				response.Headers.GetValues("Referrer-Policy")));

		Assert.Equal(
			"default-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'",
			Assert.Single(
				response.Headers.GetValues("Content-Security-Policy")));
	}

	[Fact]
	public async Task MaintenanceDisabled_AllowsRequestToContinue()
	{
		using var client = _disabledFactory.CreateClient();

		var response = await client.GetAsync(TestPath);
		var body = await response.Content.ReadAsStringAsync();

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Equal(
			"Request reached the test endpoint.",
			body);
	}

	private WebApplicationFactory<Program> CreateFactory(
		bool maintenanceEnabled,
		string instanceName)
	{
		var instanceDirectory = Path.Combine(
			_rootDirectory,
			instanceName);

		Directory.CreateDirectory(instanceDirectory);

		return new WebApplicationFactory<Program>()
			.WithWebHostBuilder(builder =>
			{
				builder.UseEnvironment(Environments.Production);

				builder.UseSetting(
					"Storage:Directory",
					Path.Combine(instanceDirectory, "domain-data"));

				builder.UseSetting(
					"Authentication:CredentialFile",
					Path.Combine(instanceDirectory, "credentials.json"));

				builder.UseSetting(
					"Maintenance:Enabled",
					maintenanceEnabled.ToString());

				builder.UseSetting(
					"Maintenance:Message",
					MaintenanceMessage);

				builder.ConfigureTestServices(services =>
					services.AddControllersWithViews()
						.AddApplicationPart(
							typeof(MaintenanceModeTestController).Assembly));
			});
	}
}

[ApiController]
[Route("__test")]
public sealed class MaintenanceModeTestController : ControllerBase
{
	[HttpGet("maintenance-probe")]
	public IActionResult Probe() =>
		Ok("Request reached the test endpoint.");
}
