using System.Net;
using System.Text.Json;

namespace ApexPost.Web.IntegrationTests;

public sealed class PartnerSecurityEndpointsTests : IAsyncLifetime
{
	private const string AllowedOrigin = "https://tracking.example.test";
	private string _rootDirectory = null!;
	private WebApplicationFactory<Program> _factory = null!;

	public Task InitializeAsync()
	{
		_rootDirectory = Path.Combine(
			Path.GetTempPath(), $"ApexPost-PartnerEndpoints-{Guid.NewGuid():N}");
		Directory.CreateDirectory(_rootDirectory);
		_factory = new WebApplicationFactory<Program>()
			.WithWebHostBuilder(builder =>
			{
				builder.UseSetting(
					"Storage:Directory",
					Path.Combine(_rootDirectory, "data"));
				builder.UseSetting(
					"Authentication:CredentialFile",
					Path.Combine(_rootDirectory, "credentials.json"));
				builder.UseSetting(
					"ParcelTracking:AllowedOrigins:0",
					AllowedOrigin);
			});
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
	public async Task Health_ReturnsNonSensitiveHealthyResponse()
	{
		using var client = _factory.CreateClient();

		var response = await client.GetAsync("/health");
		var body = await response.Content.ReadAsStringAsync();
		using var json = JsonDocument.Parse(body);

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Equal(
			"Healthy",
			json.RootElement.GetProperty("status").GetString());
		Assert.DoesNotContain(_rootDirectory, body, StringComparison.Ordinal);
		Assert.DoesNotContain("ApiSecurity", body, StringComparison.Ordinal);
	}

	[Fact]
	public async Task Health_AllowsConfiguredOriginWithoutCredentials()
	{
		using var client = _factory.CreateClient();
		using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
		request.Headers.Add("Origin", AllowedOrigin);

		var response = await client.SendAsync(request);

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Equal(
			AllowedOrigin,
			Assert.Single(response.Headers.GetValues(
				"Access-Control-Allow-Origin")));
		Assert.False(response.Headers.Contains(
			"Access-Control-Allow-Credentials"));
	}

	[Fact]
	public async Task Health_DoesNotAllowUnconfiguredOrigin()
	{
		using var client = _factory.CreateClient();
		using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
		request.Headers.Add("Origin", "https://unapproved.example.test");

		var response = await client.SendAsync(request);

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.False(response.Headers.Contains(
			"Access-Control-Allow-Origin"));
	}

	[Fact]
	public async Task Health_PreflightAllowsOnlyConfiguredReadMethod()
	{
		using var client = _factory.CreateClient();
		using var request = new HttpRequestMessage(
			HttpMethod.Options, "/health");
		request.Headers.Add("Origin", AllowedOrigin);
		request.Headers.Add(
			"Access-Control-Request-Method", HttpMethod.Get.Method);
		request.Headers.Add("Access-Control-Request-Headers", "Accept");

		var response = await client.SendAsync(request);

		Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
		Assert.Equal(
			AllowedOrigin,
			Assert.Single(response.Headers.GetValues(
				"Access-Control-Allow-Origin")));
		Assert.Contains(
			"GET",
			Assert.Single(response.Headers.GetValues(
				"Access-Control-Allow-Methods")),
			StringComparison.OrdinalIgnoreCase);
		Assert.False(response.Headers.Contains(
			"Access-Control-Allow-Credentials"));
	}
}
