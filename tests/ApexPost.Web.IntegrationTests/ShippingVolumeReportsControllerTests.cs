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

public sealed class ShippingVolumeReportsControllerTests : IAsyncLifetime
{
	private const string ReportStaffId = "shipping-report-user";
	private string _storageDirectory = null!;
	private WebApplicationFactory<Program> _factory = null!;

	public Task InitializeAsync()
	{
		_storageDirectory = Path.Combine(
			Path.GetTempPath(), $"ApexPost-ShippingVolume-{Guid.NewGuid():N}");
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
	public async Task ShippingVolume_RendersPersistedCountsIgnoringTamperedValues()
	{
		await SeedAsync(
			[CreateStaff(ReportStaffId, StaffRole.Auditor)],
			[
				CreateItem("ZONE-A", ShippingServiceLevel.Standard),
				CreateItem("ZONE-A", ShippingServiceLevel.Standard),
				CreateItem("ZONE-A", ShippingServiceLevel.Express),
				CreateItem("ZONE-B", ShippingServiceLevel.Registered)
			]);
		using var client = CreateClient(
			"Auditor", ReportStaffId, branchCode: "BR02");

		var tampered = await client.GetAsync(
			"/Reports/ShippingVolume?branchCode=BR02&zone=ZONE-B");
		var plain = await client.GetAsync("/Reports/ShippingVolume");

		Assert.Equal(HttpStatusCode.OK, tampered.StatusCode);
		var html = await tampered.Content.ReadAsStringAsync();
		Assert.Equal(ExtractTable(html), ExtractTable(
			await plain.Content.ReadAsStringAsync()));
		Assert.Matches(
			@"ZONE-A</td>\s*<td>Standard</td>\s*<td>2</td>", html);
		Assert.Matches(
			@"ZONE-A</td>\s*<td>Express</td>\s*<td>1</td>", html);
		Assert.Matches(
			@"ZONE-B</td>\s*<td>Registered</td>\s*<td>1</td>", html);
	}

	[Fact]
	public async Task ShippingVolume_EmptyDataRendersEmptyReport()
	{
		await SeedAsync([CreateStaff(ReportStaffId, StaffRole.Administrator)], []);
		using var client = CreateClient("Administrator", ReportStaffId);

		var response = await client.GetAsync("/Reports/ShippingVolume");

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Contains(
			"No shipping items were found.",
			await response.Content.ReadAsStringAsync());
	}

	[Theory]
	[InlineData("Clerk", StaffRole.CounterClerk)]
	[InlineData("BranchManager", StaffRole.BranchManager)]
	public async Task ShippingVolume_NonReportRolesAreForbidden(
		string claimRole,
		StaffRole persistedRole)
	{
		await SeedAsync(
			[CreateStaff(ReportStaffId, persistedRole)],
			[CreateItem("ZONE-A", ShippingServiceLevel.Standard)]);
		using var client = CreateClient(claimRole, ReportStaffId);

		var response = await client.GetAsync("/Reports/ShippingVolume");

		Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
		Assert.DoesNotContain(
			"ZONE-A", await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task ShippingVolume_PersistedNonReportRoleWithReportClaimIsForbidden()
	{
		await SeedAsync(
			[CreateStaff(ReportStaffId, StaffRole.CounterClerk)],
			[CreateItem("ZONE-A", ShippingServiceLevel.Standard)]);
		using var client = CreateClient("Auditor", ReportStaffId);

		var response = await client.GetAsync("/Reports/ShippingVolume");

		Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
	}

	[Fact]
	public async Task ShippingVolume_InactiveReportUserIsForbidden()
	{
		var reportUser = CreateStaff(ReportStaffId, StaffRole.Auditor);
		reportUser.Deactivate();
		await SeedAsync([reportUser], []);
		using var client = CreateClient("Auditor", ReportStaffId);

		var response = await client.GetAsync("/Reports/ShippingVolume");

		Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
	}

	[Fact]
	public async Task ShippingVolume_UnknownAndMissingIdentityAreForbidden()
	{
		await SeedAsync([CreateStaff(ReportStaffId, StaffRole.Auditor)], []);
		using var unknown = CreateClient("Auditor", "unknown-staff");
		using var missing = CreateClient("Auditor", null);

		Assert.Equal(
			HttpStatusCode.Forbidden,
			(await unknown.GetAsync("/Reports/ShippingVolume")).StatusCode);
		Assert.Equal(
			HttpStatusCode.Forbidden,
			(await missing.GetAsync("/Reports/ShippingVolume")).StatusCode);
	}

	private static string ExtractTable(string html)
	{
		var start = html.IndexOf("<table", StringComparison.Ordinal);
		var end = html.IndexOf("</table>", StringComparison.Ordinal);
		Assert.True(start >= 0 && end > start);
		return html[start..end];
	}

	private HttpClient CreateClient(
		string role,
		string? staffId,
		string? branchCode = null)
	{
		var client = _factory.CreateClient();
		client.DefaultRequestHeaders.Add("X-Low-Stock-Role", role);
		if (staffId is not null)
			client.DefaultRequestHeaders.Add("X-Low-Stock-StaffId", staffId);
		if (branchCode is not null)
			client.DefaultRequestHeaders.Add(
				"X-Low-Stock-BranchCode", branchCode);
		return client;
	}

	private Task SeedAsync(
		IEnumerable<Staff> staff,
		IEnumerable<ShippingItem> items) =>
		_factory.Services.GetRequiredService<IApexPostRepository>()
			.SaveAsync(new ApexPostSnapshot(
				[], staff, [], [], items, [], [], [], []));

	private static Staff CreateStaff(string id, StaffRole role) =>
		new(id, $"Test {id}", $"{id}@example.test", role,
			new BranchCode("BR01"));

	private static ShippingItem CreateItem(
		string zone,
		ShippingServiceLevel level) =>
		new(
			Guid.NewGuid().ToString(),
			ShippingItemType.Parcel,
			new Weight(1m),
			new Dimensions(1m, 1m, 1m),
			zone,
			level);
}
