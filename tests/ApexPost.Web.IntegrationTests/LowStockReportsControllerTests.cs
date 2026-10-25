using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using ApexPost.Application.Persistence;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApexPost.Web.IntegrationTests;

public sealed class LowStockReportsControllerTests : IAsyncLifetime
{
	private const string ManagerId = "low-stock-manager";
	private string _storageDirectory = null!;
	private WebApplicationFactory<Program> _factory = null!;

	public Task InitializeAsync()
	{
		_storageDirectory = CreateStorageDirectory();
		_factory = CreateFactory(_storageDirectory, "2");
		return Task.CompletedTask;
	}

	public async Task DisposeAsync()
	{
		await _factory.DisposeAsync();
		DeleteStorageDirectory(_storageDirectory);
	}

	[Fact]
	public async Task LowStock_UsesPersistedHomeBranchDespiteTamperedBranchValues()
	{
		await SeedAsync(
			[CreateManager()],
			[
				new BranchStock(new BranchCode("BR01"), new Sku("LOCAL-LOW"), 1),
				new BranchStock(new BranchCode("BR02"), new Sku("FOREIGN-LOW"), 0)
			]);
		using var client = CreateClient(
			"BranchManager", ManagerId, branchCode: "BR02");

		var response = await client.GetAsync(
			"/Reports/LowStock?branchCode=BR02");

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		var html = await response.Content.ReadAsStringAsync();
		Assert.Contains("LOCAL-LOW", html);
		Assert.Contains("BR01", html);
		Assert.DoesNotContain("FOREIGN-LOW", html);
		Assert.DoesNotContain("BR02", html);
	}

	[Fact]
	public async Task LowStock_NonManagerRoleIsDenied()
	{
		await SeedAsync(
			[CreateStaff("clerk-1", StaffRole.CounterClerk, "BR01")]);
		using var client = CreateClient("Clerk", "clerk-1");

		var response = await client.GetAsync("/Reports/LowStock");

		Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
	}

	[Fact]
	public async Task LowStock_PersistedNonManagerIsDenied()
	{
		await SeedAsync(
			[CreateStaff(ManagerId, StaffRole.CounterClerk, "BR01")]);
		using var client = CreateClient("BranchManager", ManagerId);

		var response = await client.GetAsync("/Reports/LowStock");

		Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
	}

	[Fact]
	public async Task LowStock_InactiveManagerIsDenied()
	{
		var manager = CreateManager();
		manager.Deactivate();
		await SeedAsync([manager]);
		using var client = CreateClient("BranchManager", ManagerId);

		var response = await client.GetAsync("/Reports/LowStock");

		Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
	}

	[Fact]
	public async Task LowStock_UnknownStaffIsDenied()
	{
		await SeedAsync([CreateManager()]);
		using var client = CreateClient("BranchManager", "unknown-staff");

		var response = await client.GetAsync("/Reports/LowStock");

		Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
	}

	[Fact]
	public async Task LowStock_MissingStaffIdentityIsDenied()
	{
		await SeedAsync([CreateManager()]);
		using var client = CreateClient("BranchManager", null);

		var response = await client.GetAsync("/Reports/LowStock");

		Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
	}

	[Fact]
	public async Task LowStock_EmptyStockRendersEmptyReport()
	{
		await SeedAsync([CreateManager()]);
		using var client = CreateClient("BranchManager", ManagerId);

		var response = await client.GetAsync("/Reports/LowStock");

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		var html = await response.Content.ReadAsStringAsync();
		Assert.Contains(
			"No low-stock items were found for your home branch.", html);
	}

	[Fact]
	public async Task LowStock_MissingThresholdReturnsServiceUnavailable()
	{
		var storageDirectory = CreateStorageDirectory();
		await using var factory = CreateFactory(storageDirectory, null);
		try
		{
			await SeedAsync(factory, [CreateManager()], []);
			using var client = CreateClient(factory, "BranchManager", ManagerId);

			var response = await client.GetAsync("/Reports/LowStock");

			Assert.Equal(
				HttpStatusCode.ServiceUnavailable, response.StatusCode);
			Assert.Contains(
				"threshold is not configured",
				await response.Content.ReadAsStringAsync(),
				StringComparison.OrdinalIgnoreCase);
		}
		finally
		{
			DeleteStorageDirectory(storageDirectory);
		}
	}

	[Theory]
	[InlineData("-1")]
	[InlineData("not-an-integer")]
	public void LowStock_InvalidThresholdPreventsApplicationStartup(
		string threshold)
	{
		var storageDirectory = CreateStorageDirectory();
		using var factory = CreateFactory(storageDirectory, threshold);
		try
		{
			Assert.Throws<InvalidOperationException>(
				() => factory.CreateClient());
		}
		finally
		{
			factory.Dispose();
			DeleteStorageDirectory(storageDirectory);
		}
	}

	private HttpClient CreateClient(
		string? role,
		string? staffId,
		string? branchCode = null) =>
		CreateClient(_factory, role, staffId, branchCode);

	private static HttpClient CreateClient(
		WebApplicationFactory<Program> factory,
		string? role,
		string? staffId,
		string? branchCode = null)
	{
		var client = factory.CreateClient();
		if (role is not null)
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
		IEnumerable<BranchStock>? stocks = null) =>
		SeedAsync(_factory, staff, stocks ?? []);

	private static Task SeedAsync(
		WebApplicationFactory<Program> factory,
		IEnumerable<Staff> staff,
		IEnumerable<BranchStock> stocks) =>
		factory.Services.GetRequiredService<IApexPostRepository>()
			.SaveAsync(new ApexPostSnapshot(
				[],
				staff,
				[],
				stocks,
				[],
				[],
				[],
				[],
				[]));

	private static WebApplicationFactory<Program> CreateFactory(
		string storageDirectory,
		string? threshold) =>
		new WebApplicationFactory<Program>()
			.WithWebHostBuilder(builder =>
			{
				builder.UseSetting("Storage:Directory", storageDirectory);
				builder.UseSetting(
					"Authentication:CredentialFile",
					Path.Combine(storageDirectory, "credentials.json"));
				if (threshold is not null)
					builder.UseSetting(
						"Reports:LowStockThreshold", threshold);
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

	private static Staff CreateManager() =>
		CreateStaff(ManagerId, StaffRole.BranchManager, "BR01");

	private static Staff CreateStaff(
		string id,
		StaffRole role,
		string branchCode) =>
		new(
			id,
			$"Test {id}",
			$"{id}@example.test",
			role,
			new BranchCode(branchCode));

	private static string CreateStorageDirectory()
	{
		var path = Path.Combine(
			Path.GetTempPath(), $"ApexPost-LowStock-{Guid.NewGuid():N}");
		Directory.CreateDirectory(path);
		return path;
	}

	private static void DeleteStorageDirectory(string path)
	{
		if (Directory.Exists(path))
			Directory.Delete(path, recursive: true);
	}
}

public sealed class LowStockTestAuthenticationHandler(
	IOptionsMonitor<AuthenticationSchemeOptions> options,
	ILoggerFactory logger,
	UrlEncoder encoder)
	: AuthenticationHandler<AuthenticationSchemeOptions>(
		options, logger, encoder)
{
	public const string SchemeName = "LowStockTest";

	protected override Task<AuthenticateResult> HandleAuthenticateAsync()
	{
		var role = Request.Headers["X-Low-Stock-Role"].ToString();
		if (string.IsNullOrWhiteSpace(role))
			return Task.FromResult(AuthenticateResult.NoResult());

		var claims = new List<Claim>
		{
			new(ClaimTypes.Role, role)
		};
		var staffId = Request.Headers["X-Low-Stock-StaffId"].ToString();
		if (!string.IsNullOrWhiteSpace(staffId))
			claims.Add(new Claim("StaffId", staffId));

		var branchCode = Request.Headers["X-Low-Stock-BranchCode"].ToString();
		if (!string.IsNullOrWhiteSpace(branchCode))
			claims.Add(new Claim("BranchCode", branchCode));

		var identity = new ClaimsIdentity(claims, SchemeName);
		return Task.FromResult(AuthenticateResult.Success(
			new AuthenticationTicket(
				new ClaimsPrincipal(identity), SchemeName)));
	}
}
