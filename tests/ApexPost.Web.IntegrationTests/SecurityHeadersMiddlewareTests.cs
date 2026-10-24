using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ApexPost.Web.IntegrationTests;

public sealed class SecurityHeadersMiddlewareTests : IAsyncLifetime
{
	private string _rootDirectory = null!;
	private WebApplicationFactory<Program> _factory = null!;

	public Task InitializeAsync()
	{
		_rootDirectory = Path.Combine(
			Path.GetTempPath(), $"ApexPost-SecurityHeaders-{Guid.NewGuid():N}");
		Directory.CreateDirectory(_rootDirectory);
		_factory = new WebApplicationFactory<Program>()
			.WithWebHostBuilder(builder =>
			{
				builder.UseSetting(
					"Storage:Directory",
					Path.Combine(_rootDirectory, "domain-data"));
				builder.UseSetting(
					"Authentication:CredentialFile",
					Path.Combine(_rootDirectory, "credentials.json"));
			});
		return Task.CompletedTask;
	}

	public async Task DisposeAsync()
	{
		await _factory.DisposeAsync();
		if (Directory.Exists(_rootDirectory))
			Directory.Delete(_rootDirectory, recursive: true);
	}

	[Fact]
	public async Task Responses_IncludeRequiredSecurityHeaders()
	{
		var client = _factory.CreateClient();

		var response = await client.GetAsync("/");

		AssertSecurityHeaders(response);
	}

	[Fact]
	public async Task NotFoundResponse_IncludesRequiredSecurityHeaders()
	{
		var client = _factory.CreateClient();

		var response = await client.GetAsync("/not-a-real-endpoint");

		Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
		AssertSecurityHeaders(response);
	}

	private static void AssertSecurityHeaders(HttpResponseMessage response)
	{
		Assert.Equal(
			"nosniff",
			Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
		Assert.Equal(
			"DENY",
			Assert.Single(response.Headers.GetValues("X-Frame-Options")));
		Assert.Equal(
			"no-referrer",
			Assert.Single(response.Headers.GetValues("Referrer-Policy")));
		Assert.Equal(
			"default-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'",
			Assert.Single(response.Headers.GetValues("Content-Security-Policy")));
	}
}
