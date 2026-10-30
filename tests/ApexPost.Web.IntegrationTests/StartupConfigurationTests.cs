using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Hosting;

namespace ApexPost.Web.IntegrationTests;

// Unspecified business values have no defaults, so invalid configuration
// must stop the application at startup rather than run with a guess.
public sealed class StartupConfigurationTests : IAsyncLifetime
{
	private string _storageDirectory = null!;

	public Task InitializeAsync()
	{
		_storageDirectory = Path.Combine(
			Path.GetTempPath(), $"ApexPost-Startup-{Guid.NewGuid():N}");
		return Task.CompletedTask;
	}

	public Task DisposeAsync()
	{
		if (Directory.Exists(_storageDirectory))
			Directory.Delete(_storageDirectory, recursive: true);
		return Task.CompletedTask;
	}

	[Fact]
	public async Task TestHost_DoesNotLoadDeveloperUserSecrets()
	{
		await using var factory = CreateFactory([]);

		var configuration = Assert.IsAssignableFrom<IConfigurationRoot>(
			factory.Services.GetRequiredService<IConfiguration>());

		Assert.DoesNotContain(
			configuration.Providers.OfType<JsonConfigurationProvider>(),
			provider => ApexPostWebApplicationFactory.IsUserSecrets(provider.Source));
	}

	[Theory]
	[InlineData("Transactions:PaymentMethods:1", "cash", "Transactions:PaymentMethods")]
	[InlineData("Transactions:TaxRates:Standard", "-0.1", "Transactions:TaxRates:Standard")]
	[InlineData("Shipping:TaxCategory", "Luxury", "Shipping:TaxCategory")]
	[InlineData("Shipping:MaximumParcelWeightKilograms:Express", "0", "Shipping:MaximumParcelWeightKilograms:Express")]
	[InlineData("MoneyOrders:Fee:Amount", "25", "MoneyOrders:HighValueThreshold")]
	[InlineData("Reports:LowStockThreshold", "-1", "Reports:LowStockThreshold")]
	[InlineData("DemoData:StaffPassword", " ", "DemoData:StaffPassword")]
	public async Task InvalidBusinessConfiguration_StopsStartup(
		string key,
		string value,
		string expectedInMessage)
	{
		await using var factory = CreateFactory(
		[
			new("Transactions:PaymentMethods:0", "Cash"),
			new(key, value)
		]);

		AssertStartupFails(factory, expectedInMessage);
	}

	[Fact]
	public async Task DemoPassword_OutsideDevelopment_StopsStartup()
	{
		await using var factory = CreateFactory(
			[new("DemoData:StaffPassword", "Some-Local-Password-1")],
			Environments.Production);

		AssertStartupFails(factory, "only supported in the Development environment");
	}

	[Fact]
	public async Task ValidConfiguration_Starts()
	{
		await using var factory = CreateFactory(
		[
			new("Transactions:PaymentMethods:0", "Cash"),
			new("Transactions:PaymentMethods:1", "Card"),
			new("Transactions:TaxRates:Standard", "0.18"),
			new("Shipping:TaxCategory", "Standard"),
			new("Shipping:MaximumParcelWeightKilograms:Express", "10")
		]);
		using var client = factory.CreateClient();

		Assert.Equal(
			System.Net.HttpStatusCode.OK,
			(await client.GetAsync("/")).StatusCode);
	}

	private static void AssertStartupFails(
		WebApplicationFactory<Program> factory,
		string expectedInMessage)
	{
		var exception = Assert.ThrowsAny<Exception>(() => factory.Services);
		var messages = new List<string>();
		for (var current = exception; current is not null; current = current.InnerException)
			messages.Add(current.Message);
		Assert.Contains(
			messages,
			message => message.Contains(expectedInMessage, StringComparison.Ordinal));
	}

	private WebApplicationFactory<Program> CreateFactory(
		IReadOnlyList<KeyValuePair<string, string>> settings,
		string? environment = null) =>
		new ApexPostWebApplicationFactory().WithWebHostBuilder(builder =>
		{
			if (environment is not null)
				builder.UseEnvironment(environment);
			builder.UseSetting("Storage:Directory", _storageDirectory);
			builder.UseSetting(
				"Authentication:CredentialFile",
				Path.Combine(_storageDirectory, "credentials.json"));
			builder.UseSetting(
				"Audit:FilePath", Path.Combine(_storageDirectory, "audit.jsonl"));
			foreach (var (key, value) in settings)
				builder.UseSetting(key, value);
		});
}
