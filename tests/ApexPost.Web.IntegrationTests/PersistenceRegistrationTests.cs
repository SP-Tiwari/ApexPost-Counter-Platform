using ApexPost.Application.Persistence;
using ApexPost.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.Web.IntegrationTests;

public sealed class PersistenceRegistrationTests : IAsyncLifetime
{
	private string _storageDirectory = null!;
	private string? _resolvedStorageDirectory;
	private WebApplicationFactory<Program> _factory = null!;

	public Task InitializeAsync()
	{
		_storageDirectory = $"persistence-test-{Guid.NewGuid():N}";
		_factory = new WebApplicationFactory<Program>()
			.WithWebHostBuilder(builder =>
				builder.UseSetting("Storage:Directory", _storageDirectory));
		return Task.CompletedTask;
	}

	public async Task DisposeAsync()
	{
		await _factory.DisposeAsync();
		if (_resolvedStorageDirectory is not null &&
			Directory.Exists(_resolvedStorageDirectory))
		{
			Directory.Delete(_resolvedStorageDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task Services_ResolveRepositoryUsingConfiguredStorageDirectory()
	{
		var services = _factory.Services;
		var repository = services.GetRequiredService<IApexPostRepository>();
		var environment = services.GetRequiredService<IWebHostEnvironment>();
		_resolvedStorageDirectory = Path.Combine(
			environment.ContentRootPath,
			_storageDirectory);

		Assert.IsType<JsonApexPostRepository>(repository);
		Assert.Empty((await repository.LoadAsync()).Branches);
		await repository.SaveAsync(ApexPostSnapshot.Empty);
		Assert.True(File.Exists(Path.Combine(
			_resolvedStorageDirectory,
			"apexpost.json")));
	}
}
