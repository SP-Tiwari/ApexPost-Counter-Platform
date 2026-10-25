using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.Web.IntegrationTests;

public sealed class ValidationFilterIntegrationTests : IAsyncLifetime
{
	private string _storageDirectory = null!;
	private WebApplicationFactory<Program> _factory = null!;

	public Task InitializeAsync()
	{
		_storageDirectory = Path.Combine(
			Path.GetTempPath(),
			$"ApexPost-ValidationFilter-{Guid.NewGuid():N}");
		Directory.CreateDirectory(_storageDirectory);

		_factory = new WebApplicationFactory<Program>()
			.WithWebHostBuilder(builder =>
			{
				builder.UseSetting("Storage:Directory", _storageDirectory);
				builder.UseSetting(
					"Authentication:CredentialFile",
					Path.Combine(_storageDirectory, "credentials.json"));
				builder.UseSetting(
					"Audit:FilePath",
					Path.Combine(_storageDirectory, "audit-events.jsonl"));
				builder.ConfigureTestServices(services =>
					services.AddControllersWithViews()
						.AddApplicationPart(
							typeof(ValidationFilterProbeController).Assembly));
			});

		return Task.CompletedTask;
	}

	public async Task DisposeAsync()
	{
		await _factory.DisposeAsync();
		if (Directory.Exists(_storageDirectory))
			Directory.Delete(_storageDirectory, recursive: true);
	}

	[Fact]
	public async Task InvalidApiRequest_ReturnsValidationProblemWithMessage()
	{
		var client = _factory.CreateClient();

		var response = await client.PostAsJsonAsync(
			"/__test/validation", new ValidationProbeRequest());

		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
		using var document = JsonDocument.Parse(
			await response.Content.ReadAsStringAsync());
		Assert.Equal(
			"The Name field is required.",
			document.RootElement
				.GetProperty("errors")
				.GetProperty("Name")[0]
				.GetString());
	}

	[Fact]
	public async Task ValidApiRequest_ReachesAction()
	{
		var client = _factory.CreateClient();

		var response = await client.PostAsJsonAsync(
			"/__test/validation",
			new ValidationProbeRequest { Name = "valid" });

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Equal("valid", await response.Content.ReadAsStringAsync());
	}
}

[ApiController]
[AllowAnonymous]
[Route("__test/validation")]
public sealed class ValidationFilterProbeController : ControllerBase
{
	[HttpPost]
	public IActionResult Post(ValidationProbeRequest request) =>
		Ok(request.Name);
}

public sealed class ValidationProbeRequest
{
	[Required]
	public string? Name { get; set; }
}
