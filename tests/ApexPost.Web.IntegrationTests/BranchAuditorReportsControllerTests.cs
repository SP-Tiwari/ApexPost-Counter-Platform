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

public sealed class BranchAuditorReportsControllerTests : IAsyncLifetime
{
	private const string ReportStaffId = "auditor-report-user";
	private string _storageDirectory = null!;
	private WebApplicationFactory<Program> _factory = null!;

	public Task InitializeAsync()
	{
		_storageDirectory = Path.Combine(
			Path.GetTempPath(), $"ApexPost-BranchAuditor-{Guid.NewGuid():N}");
		Directory.CreateDirectory(_storageDirectory);
		_factory = new ApexPostWebApplicationFactory()
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
	public async Task BranchAuditor_RendersBranchTotalsForAuthorizedUser()
	{
		await SeedAsync(
			[CreateStaff(ReportStaffId, StaffRole.Auditor)],
			[
				CreateMoneyOrder("BR01", true),
				CreateMoneyOrder("BR01", false),
				CreateMoneyOrder("BR02", true)
			]);
		using var client = CreateClient("Auditor", ReportStaffId);

		var response = await client.GetAsync("/Reports/BranchAuditor");

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		var html = await response.Content.ReadAsStringAsync();
		Assert.Matches(@"BR01</td>\s*<td>2</td>\s*<td>2</td>\s*<td>1</td>\s*<td>1</td>", html);
		Assert.Matches(@"BR02</td>\s*<td>1</td>\s*<td>1</td>\s*<td>1</td>\s*<td>0</td>", html);
	}

	[Theory]
	[InlineData("Clerk", StaffRole.CounterClerk)]
	[InlineData("BranchManager", StaffRole.BranchManager)]
	public async Task BranchAuditor_NonReportRolesAreForbidden(
		string claimRole,
		StaffRole persistedRole)
	{
		await SeedAsync(
			[CreateStaff(ReportStaffId, persistedRole)],
			[CreateMoneyOrder("BR01", true)]);
		using var client = CreateClient(claimRole, ReportStaffId);

		var response = await client.GetAsync("/Reports/BranchAuditor");

		Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
	}

	[Fact]
	public async Task BranchAuditor_PersistedNonReportRoleWithReportClaimIsForbidden()
	{
		await SeedAsync(
			[CreateStaff(ReportStaffId, StaffRole.CounterClerk)],
			[CreateMoneyOrder("BR01", true)]);
		using var client = CreateClient("Auditor", ReportStaffId);

		var response = await client.GetAsync("/Reports/BranchAuditor");

		Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
	}

	private HttpClient CreateClient(string role, string? staffId)
	{
		var client = _factory.CreateClient();
		client.DefaultRequestHeaders.Add("X-Low-Stock-Role", role);
		if (staffId is not null)
			client.DefaultRequestHeaders.Add("X-Low-Stock-StaffId", staffId);
		return client;
	}

	private Task SeedAsync(
		IEnumerable<Staff> staff,
		IEnumerable<MoneyOrder> moneyOrders) =>
		_factory.Services.GetRequiredService<IApexPostRepository>()
			.SaveAsync(new ApexPostSnapshot(
				[], staff, [], [], [], [], moneyOrders, [], []));

	private static Staff CreateStaff(string id, StaffRole role) =>
		new(id, $"Test {id}", $"{id}@example.test", role,
			new BranchCode("BR01"));

	private static MoneyOrder CreateMoneyOrder(
		string branchCode,
		bool verificationSuccess)
	{
		var verification = new IdentityVerification(
			"Passport",
			$"ID-{Guid.NewGuid():N}",
			verificationSuccess);
		return new MoneyOrder(
			new Money(100m, "INR"),
			"Sender",
			"Recipient",
			new Money(5m, "INR"),
			verification,
			new BranchCode(branchCode));
	}
}