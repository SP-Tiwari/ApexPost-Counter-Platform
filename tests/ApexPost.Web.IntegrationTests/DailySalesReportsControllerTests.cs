using System.Net;
using ApexPost.Application.Persistence;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;

namespace ApexPost.Web.IntegrationTests;

public sealed class DailySalesReportsControllerTests : IAsyncLifetime
{
	private const string ManagerId = "daily-sales-manager";
	private static readonly DateOnly ReportDate = new(2026, 1, 15);
	private string _storageDirectory = null!;
	private WebApplicationFactory<Program> _factory = null!;

	public Task InitializeAsync()
	{
		_storageDirectory = Path.Combine(
			Path.GetTempPath(), $"ApexPost-DailySales-{Guid.NewGuid():N}");
		Directory.CreateDirectory(_storageDirectory);
		_factory = new WebApplicationFactory<Program>()
			.WithWebHostBuilder(builder =>
			{
				builder.UseSetting("Storage:Directory", _storageDirectory);
				builder.UseSetting(
					"Authentication:CredentialFile",
					Path.Combine(_storageDirectory, "credentials.json"));
				builder.ConfigureTestServices(services =>
					services.AddAuthentication(options =>
					{
						options.DefaultAuthenticateScheme =
							LowStockTestAuthenticationHandler.SchemeName;
						options.DefaultChallengeScheme =
							LowStockTestAuthenticationHandler.SchemeName;
						options.DefaultForbidScheme =
							LowStockTestAuthenticationHandler.SchemeName;
					}).AddScheme<AuthenticationSchemeOptions,
						LowStockTestAuthenticationHandler>(
						LowStockTestAuthenticationHandler.SchemeName,
						_ => { }));
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
	public async Task DailySales_ShowsRetailAndServiceTotalsForManagersHomeBranch()
	{
		await SeedAsync(
			[CreateStaff(ManagerId, StaffRole.BranchManager, "BR01")],
			[
				CreatePaidBasket("BR01", 100m, 20m),
				CreatePaidBasket("BR02", 80m, 10m)
			]);
		using var client = CreateClient("BranchManager", ManagerId, "BR01");

		var response = await client.GetAsync(
			$"/Reports/DailySales?businessDate={ReportDate:yyyy-MM-dd}");

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		var html = await response.Content.ReadAsStringAsync();
		Assert.Matches(@"BR01</td>\s*<td>\d{1,2}/\d{1,2}/2026</td>\s*<td>100 INR</td>\s*<td>20 INR</td>\s*<td>120 INR</td>", html);
		Assert.DoesNotContain("BR02", html, StringComparison.Ordinal);
	}

	[Fact]
	public async Task DailySales_EmptyDataRendersEmptyState()
	{
		await SeedAsync(
			[CreateStaff(ManagerId, StaffRole.BranchManager, "BR01")],
			[]);
		using var client = CreateClient("BranchManager", ManagerId, "BR01");

		var response = await client.GetAsync(
			$"/Reports/DailySales?businessDate={ReportDate:yyyy-MM-dd}");

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Contains(
			"No paid transactions were found for this branch on the selected date.",
			await response.Content.ReadAsStringAsync());
	}

	[Theory]
	[InlineData("Clerk", StaffRole.CounterClerk)]
	[InlineData("Auditor", StaffRole.Auditor)]
	public async Task DailySales_NonManagersAreForbidden(
		string claimRole,
		StaffRole persistedRole)
	{
		await SeedAsync(
			[CreateStaff(ManagerId, persistedRole, "BR01")],
			[]);
		using var client = CreateClient(claimRole, ManagerId, "BR01");

		var response = await client.GetAsync("/Reports/DailySales");

		Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
	}

	private HttpClient CreateClient(
		string role,
		string? staffId,
		string? branchCode)
	{
		var client = _factory.CreateClient();
		client.DefaultRequestHeaders.Add("X-Low-Stock-Role", role);
		if (staffId is not null)
			client.DefaultRequestHeaders.Add("X-Low-Stock-StaffId", staffId);
		if (branchCode is not null)
			client.DefaultRequestHeaders.Add("X-Low-Stock-BranchCode", branchCode);
		return client;
	}

	private Task SeedAsync(
		IEnumerable<Staff> staff,
		IEnumerable<TransactionBasket> baskets) =>
		_factory.Services.GetRequiredService<IApexPostRepository>()
			.SaveAsync(new ApexPostSnapshot(
				[], staff, [], [], [], [], [], [], baskets));

	private static Staff CreateStaff(
		string id,
		StaffRole role,
		string branchCode) =>
		new(id, $"Test {id}", $"{id}@example.test", role,
			new BranchCode(branchCode));

	private static TransactionBasket CreatePaidBasket(
		string branchCode,
		decimal retail,
		decimal service)
	{
		var taxRates = new Dictionary<TaxCategory, decimal>
		{
			[TaxCategory.Standard] = 0m
		};
		var basket = new TransactionBasket(
			"INR",
			taxRates,
			new BranchCode(branchCode));
		basket.AddLineItem(new TransactionLineItem(
			"Retail",
			TransactionItemType.Goods,
			new Money(retail, "INR"),
			1,
			TaxCategory.Standard,
			new Sku("SKU-1")));
		basket.AddLineItem(new TransactionLineItem(
			"Service",
			TransactionItemType.Service,
			new Money(service, "INR"),
			1,
			TaxCategory.Standard));
		basket.Pay();
		return basket;
	}
}