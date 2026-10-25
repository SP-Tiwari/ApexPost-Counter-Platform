using System.Net;
using Microsoft.AspNetCore.Builder;

namespace ApexPost.Web.IntegrationTests;

public sealed class ApiKeyAndIpRestrictionMiddlewareTests : IAsyncLifetime
{
	private const string TestApiKey = "test-api-key-never-used-in-production";
	private const string ProtectedPath = "/__test/partner/probe";
	private string _rootDirectory = null!;
	private WebApplicationFactory<Program> _factory = null!;

	public Task InitializeAsync()
	{
		_rootDirectory = Path.Combine(
			Path.GetTempPath(), $"ApexPost-ApiSecurity-{Guid.NewGuid():N}");
		Directory.CreateDirectory(_rootDirectory);
		_factory = CreateFactory(
			"api-security",
			TestApiKey,
			["192.0.2.10"]);
		return Task.CompletedTask;
	}

	public async Task DisposeAsync()
	{
		await _factory.DisposeAsync();
		if (Directory.Exists(_rootDirectory))
		{
			Directory.Delete(_rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task ProtectedEndpoint_WithValidKeyAndAllowedIp_Continues()
	{
		using var client = _factory.CreateClient();
		client.DefaultRequestHeaders.Add("X-API-Key", TestApiKey);

		var response = await client.GetAsync(ProtectedPath);

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Equal(
			"Partner endpoint reached.",
			await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task ProtectedEndpoint_WithInvalidKey_ReturnsUnauthorizedWithoutDisclosure()
	{
		using var client = _factory.CreateClient();
		client.DefaultRequestHeaders.Add("X-API-Key", "incorrect-key");

		var response = await client.GetAsync(ProtectedPath);
		var body = await response.Content.ReadAsStringAsync();

		Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
		Assert.DoesNotContain(TestApiKey, body, StringComparison.Ordinal);
	}

	[Fact]
	public async Task ProtectedEndpoint_WithoutKey_ReturnsUnauthorized()
	{
		using var client = _factory.CreateClient();

		var response = await client.GetAsync(ProtectedPath);

		Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
	}

	[Fact]
	public async Task ProtectedEndpoint_WithBlockedIp_ReturnsForbidden()
	{
		await using var blockedFactory = CreateFactory(
			"blocked-ip",
			TestApiKey,
			["203.0.113.42"]);
		using var client = blockedFactory.CreateClient();
		client.DefaultRequestHeaders.Add("X-API-Key", TestApiKey);

		var response = await client.GetAsync(ProtectedPath);

		Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
	}

	[Fact]
	public async Task ProtectedEndpoint_WithoutConfiguredKey_FailsClosed()
	{
		await using var unconfiguredFactory = CreateFactory(
			"missing-key",
			apiKey: null,
			allowedIpAddresses: []);
		using var client = unconfiguredFactory.CreateClient();
		client.DefaultRequestHeaders.Add("X-API-Key", TestApiKey);

		var response = await client.GetAsync(ProtectedPath);

		Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
	}

	[Fact]
	public async Task ProtectedEndpoint_ExceedingValidatedPartnerLimit_ReturnsTooManyRequests()
	{
		await using var limitedFactory = CreateFactory(
			"partner-rate-limit",
			TestApiKey,
			["192.0.2.10"],
			partnerPermitLimit: 1);
		using var client = limitedFactory.CreateClient();
		client.DefaultRequestHeaders.Add("X-API-Key", TestApiKey);
		client.DefaultRequestHeaders.Add("X-Partner-Id", "caller-controlled");

		var firstResponse = await client.GetAsync(ProtectedPath);
		var secondResponse = await client.GetAsync(ProtectedPath);

		Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
		Assert.Equal(
			HttpStatusCode.TooManyRequests,
			secondResponse.StatusCode);
	}

	[Fact]
	public async Task UnrelatedMvcRoute_DoesNotRequireApiKeyOrIpAllowlist()
	{
		using var client = _factory.CreateClient();

		var response = await client.GetAsync("/Account/SignIn");

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
	}

	private WebApplicationFactory<Program> CreateFactory(
		string instanceName,
		string? apiKey,
		string[] allowedIpAddresses,
		int partnerPermitLimit = 100,
		int partnerWindowSeconds = 60)
	{
		var instanceDirectory = Path.Combine(_rootDirectory, instanceName);
		Directory.CreateDirectory(instanceDirectory);

		return new WebApplicationFactory<Program>()
			.WithWebHostBuilder(builder =>
			{
				builder.UseSetting(
					"Storage:Directory",
					Path.Combine(instanceDirectory, "data"));
				builder.UseSetting(
					"Authentication:CredentialFile",
					Path.Combine(instanceDirectory, "credentials.json"));
				builder.UseSetting(
					"ApiSecurity:ProtectedPathPrefixes:0",
					"/__test/partner");
				builder.UseSetting(
					"ApiSecurity:PartnerRateLimit:PermitLimit",
					partnerPermitLimit.ToString());
				builder.UseSetting(
					"ApiSecurity:PartnerRateLimit:WindowSeconds",
					partnerWindowSeconds.ToString());
				if (apiKey is not null)
				{
					builder.UseSetting("ApiSecurity:ApiKey", apiKey);
				}

				for (var index = 0; index < allowedIpAddresses.Length; index++)
				{
					builder.UseSetting(
						$"ApiSecurity:AllowedIpAddresses:{index}",
						allowedIpAddresses[index]);
				}

				builder.ConfigureTestServices(services =>
				{
					services.AddSingleton<IStartupFilter>(
						new FixedRemoteIpAddressStartupFilter(
							IPAddress.Parse("192.0.2.10")));
					services.AddControllersWithViews()
						.AddApplicationPart(
							typeof(ApiSecurityProbeController).Assembly);
				});
			});
	}
}

internal sealed class FixedRemoteIpAddressStartupFilter(
	IPAddress remoteAddress) : IStartupFilter
{
	public Action<IApplicationBuilder> Configure(
		Action<IApplicationBuilder> next) =>
		app =>
		{
			app.Use(async (context, nextMiddleware) =>
			{
				context.Connection.RemoteIpAddress = remoteAddress;
				await nextMiddleware();
			});
			next(app);
		};
}

[ApiController]
[Route("__test/partner")]
public sealed class ApiSecurityProbeController : ControllerBase
{
	[HttpGet("probe")]
	public IActionResult Probe() => Ok("Partner endpoint reached.");
}
