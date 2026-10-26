using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using ApexPost.Application.Persistence;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApexPost.Web.IntegrationTests;

public sealed class MoneyOrdersControllerTests : IAsyncLifetime
{
	private const string StaffId = "money-order-manager";
	private const string FullDocumentIdentifier = "DOC-SECRET-987654";
	private string _storageDirectory = null!;
	private WebApplicationFactory<Program> _factory = null!;

	public Task InitializeAsync()
	{
		_storageDirectory = Path.Combine(
			Path.GetTempPath(), $"ApexPost-MoneyOrders-{Guid.NewGuid():N}");
		_factory = new WebApplicationFactory<Program>()
			.WithWebHostBuilder(builder =>
			{
				builder.UseSetting("Storage:Directory", _storageDirectory);
				builder.UseSetting(
					"Authentication:CredentialFile",
					Path.Combine(_storageDirectory, "credentials.json"));
				builder.UseSetting(
					"MoneyOrders:HighValueThreshold:Amount", "1000");
				builder.UseSetting(
					"MoneyOrders:HighValueThreshold:Currency", "INR");
				builder.ConfigureTestServices(services =>
					services.AddAuthentication(options =>
					{
						options.DefaultAuthenticateScheme =
							MoneyOrderTestAuthenticationHandler.AuthenticationSchemeName;
						options.DefaultChallengeScheme =
							MoneyOrderTestAuthenticationHandler.AuthenticationSchemeName;
						options.DefaultForbidScheme =
							MoneyOrderTestAuthenticationHandler.AuthenticationSchemeName;
					}).AddScheme<AuthenticationSchemeOptions,
						MoneyOrderTestAuthenticationHandler>(
						MoneyOrderTestAuthenticationHandler.AuthenticationSchemeName,
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
	public async Task Create_HighValueOrderWithManagerAttestationPersistsOnlyMaskedDocument()
	{
		await SeedManagerAsync();
		using var client = CreateClient("BranchManager");
		var token = await GetAntiforgeryTokenAsync(client);

		var response = await client.PostAsync(
			"/MoneyOrders/Create",
			CreateForm(1500m, true, true, token));

		Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
		Assert.Equal("/MoneyOrders/Index", response.Headers.Location?.OriginalString);
		var repository = _factory.Services.GetRequiredService<IApexPostRepository>();
		var snapshot = await repository.LoadAsync();
		var order = Assert.Single(snapshot.MoneyOrders);
		Assert.Equal(new Money(1500m, "INR"), order.Amount);
		var expectedMaskedIdentifier =
			new string('*', FullDocumentIdentifier.Length - 4)
			+ FullDocumentIdentifier[^4..];
		Assert.Equal(
			expectedMaskedIdentifier,
			order.IdentityVerification?.MaskedDocumentIdentifier);
		Assert.Equal(new BranchCode("BR01"), order.ProcessingBranchCode);
		Assert.Equal(order.IdentityVerification, Assert.Single(snapshot.IdentityVerifications));

		var json = await File.ReadAllTextAsync(
			Path.Combine(_storageDirectory, "apexpost.json"));
		Assert.DoesNotContain(FullDocumentIdentifier, json, StringComparison.Ordinal);
		Assert.Contains(expectedMaskedIdentifier, json, StringComparison.Ordinal);
	}

	[Fact]
	public async Task Create_FailedIdentityCheckBelowThresholdPersistsOrder()
	{
		await SeedManagerAsync();
		using var client = CreateClient("BranchManager");
		var token = await GetAntiforgeryTokenAsync(client);

		var response = await client.PostAsync(
			"/MoneyOrders/Create",
			CreateForm(500m, true, false, token));

		Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
		Assert.Equal("/MoneyOrders/Index", response.Headers.Location?.OriginalString);
		var snapshot = await LoadSnapshotAsync();
		var order = Assert.Single(snapshot.MoneyOrders);
		Assert.Equal(new Money(500m, "INR"), order.Amount);
		Assert.NotNull(order.IdentityVerification);
		Assert.False(order.IdentityVerification!.IsSuccessful);
		Assert.Equal(order.IdentityVerification, Assert.Single(snapshot.IdentityVerifications));
	}

	[Fact]
	public async Task Create_HighValueOrderWithoutSuccessfulVerificationIsRejected()
	{
		await SeedManagerAsync();
		using var client = CreateClient("BranchManager");
		var token = await GetAntiforgeryTokenAsync(client);

		var response = await client.PostAsync(
			"/MoneyOrders/Create",
			CreateForm(1500m, false, false, token));

		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
		Assert.Contains(
			"Identity verification did not succeed or is required",
			await response.Content.ReadAsStringAsync(),
			StringComparison.Ordinal);
		Assert.Empty((await LoadSnapshotAsync()).MoneyOrders);
	}

	[Fact]
	public async Task Create_InvalidAmountIsRejectedWithoutPersistence()
	{
		await SeedManagerAsync();
		using var client = CreateClient("BranchManager");
		var token = await GetAntiforgeryTokenAsync(client);
		var form = CreateForm(0m, false, false, token);

		var response = await client.PostAsync("/MoneyOrders/Create", form);

		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
		Assert.Empty((await LoadSnapshotAsync()).MoneyOrders);
	}

	[Fact]
	public async Task Create_RequiresAntiforgeryToken()
	{
		await SeedManagerAsync();
		using var client = CreateClient("BranchManager");

		var response = await client.PostAsync(
			"/MoneyOrders/Create",
			CreateForm(500m, false, false));

		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
		Assert.Empty((await LoadSnapshotAsync()).MoneyOrders);
	}

	[Fact]
	public async Task MoneyOrderPages_RequireBranchManager()
	{
		using var anonymousClient = CreateClient(null);
		var anonymousResponse = await anonymousClient.GetAsync(
			"/MoneyOrders/Create");
		Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

		using var clerkClient = CreateClient("CounterClerk");
		var clerkResponse = await clerkClient.GetAsync("/MoneyOrders/Create");
		Assert.Equal(HttpStatusCode.Forbidden, clerkResponse.StatusCode);
	}

	[Fact]
	public async Task Index_ShowsOnlyKnownOrdersFromAuthenticatedManagersPersistedBranch()
	{
		await SeedOrdersAsync();
		using var client = CreateClient("BranchManager");
		client.DefaultRequestHeaders.Add("X-Money-Order-Branch", "BR02");

		var response = await client.GetAsync("/MoneyOrders/Index");
		var content = await response.Content.ReadAsStringAsync();

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Contains("Local Sender", content, StringComparison.Ordinal);
		Assert.DoesNotContain("Other Branch Sender", content, StringComparison.Ordinal);
		Assert.DoesNotContain("Legacy Sender", content, StringComparison.Ordinal);
	}

	[Fact]
	public async Task Create_SubmittedBranchCannotOverridePersistedManagersHomeBranch()
	{
		await SeedManagerAsync();
		using var client = CreateClient("BranchManager");
		var token = await GetAntiforgeryTokenAsync(client);

		var response = await client.PostAsync(
			"/MoneyOrders/Create",
			CreateForm(500m, false, false, token, "BR02"));

		Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
		Assert.Equal(
			new BranchCode("BR01"),
			Assert.Single((await LoadSnapshotAsync()).MoneyOrders)
				.ProcessingBranchCode);
	}

	[Fact]
	public async Task Create_LowValueOrderDoesNotRequireIdentityCheck()
	{
		await SeedManagerAsync();
		using var client = CreateClient("BranchManager");
		var token = await GetAntiforgeryTokenAsync(client);

		var response = await client.PostAsync(
			"/MoneyOrders/Create",
			CreateForm(500m, false, false, token));

		Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
		var order = Assert.Single((await LoadSnapshotAsync()).MoneyOrders);
		Assert.Null(order.IdentityVerification);
	}

	private HttpClient CreateClient(string? role)
	{
		var client = _factory.CreateClient(
			new WebApplicationFactoryClientOptions
			{
				AllowAutoRedirect = false
			});
		if (role is not null)
			client.DefaultRequestHeaders.Add("X-Money-Order-Role", role);
		return client;
	}

	private async Task SeedManagerAsync()
	{
		await _factory.Services.GetRequiredService<IApexPostRepository>()
			.SaveAsync(new ApexPostSnapshot(
				[],
				[
					new Staff(
						StaffId,
						"Test Manager",
						"money-order-manager@example.test",
						StaffRole.BranchManager,
						new BranchCode("BR01"))
				],
				[],
				[],
				[],
				[],
				[],
				[],
				[]));
	}

	private async Task SeedOrdersAsync()
	{
		var repository = _factory.Services
			.GetRequiredService<IApexPostRepository>();
		await repository.SaveAsync(new ApexPostSnapshot(
			[],
			[
				new Staff(
					StaffId,
					"Test Manager",
					"money-order-manager@example.test",
					StaffRole.BranchManager,
					new BranchCode("BR01"))
			],
			[],
			[],
			[],
			[],
			[
				CreateMoneyOrder("Local Sender", "BR01"),
				CreateMoneyOrder("Other Branch Sender", "BR02"),
				CreateMoneyOrder("Legacy Sender", null)
			],
			[],
			[]));
	}

	private async Task<ApexPostSnapshot> LoadSnapshotAsync() =>
		await _factory.Services.GetRequiredService<IApexPostRepository>()
			.LoadAsync();

	private static async Task<string> GetAntiforgeryTokenAsync(
		HttpClient client)
	{
		var response = await client.GetAsync("/MoneyOrders/Create");
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		var html = await response.Content.ReadAsStringAsync();
		var token = Regex.Match(
			html,
			"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")
			.Groups[1].Value;
		Assert.False(string.IsNullOrWhiteSpace(token));
		return token;
	}

	private static FormUrlEncodedContent CreateForm(
		decimal amount,
		bool documentCheckedInPerson,
		bool identityCheckPassed,
		string? antiforgeryToken = null,
		string? branchCode = null)
	{
		var values = new List<KeyValuePair<string, string>>
		{
			new("Amount", amount.ToString(
				System.Globalization.CultureInfo.InvariantCulture)),
			new("Sender", "Test Sender"),
			new("Recipient", "Test Recipient"),
			new("Fee", "10"),
			new("DocumentCheckedInPerson", documentCheckedInPerson.ToString()),
			new("IdentityCheckPassed", identityCheckPassed.ToString())
		};
		if (documentCheckedInPerson)
		{
			values.Add(new("DocumentType", "Passport"));
			values.Add(new("DocumentIdentifier", FullDocumentIdentifier));
		}

		if (antiforgeryToken is not null)
			values.Add(new("__RequestVerificationToken", antiforgeryToken));
		if (branchCode is not null)
			values.Add(new("BranchCode", branchCode));

		return new FormUrlEncodedContent(values);
	}

	private static MoneyOrder CreateMoneyOrder(
		string sender,
		string? branchCode) =>
		new(
			new Money(500m, "INR"),
			sender,
			"Recipient",
			new Money(10m, "INR"),
			processingBranchCode: branchCode is null
				? null
				: new BranchCode(branchCode));
}

public sealed class MoneyOrderTestAuthenticationHandler(
	IOptionsMonitor<AuthenticationSchemeOptions> options,
	ILoggerFactory logger,
	UrlEncoder encoder)
	: AuthenticationHandler<AuthenticationSchemeOptions>(
		options, logger, encoder)
{
	public const string AuthenticationSchemeName = "MoneyOrderTest";

	protected override Task<AuthenticateResult> HandleAuthenticateAsync()
	{
		var role = Request.Headers["X-Money-Order-Role"].ToString();
		if (string.IsNullOrWhiteSpace(role))
			return Task.FromResult(AuthenticateResult.NoResult());

		var identity = new ClaimsIdentity(
			[
				new Claim("StaffId", MoneyOrdersControllerTestsStaff.Id),
				new Claim(ClaimTypes.Role, role)
			],
			AuthenticationSchemeName);
		return Task.FromResult(AuthenticateResult.Success(
			new AuthenticationTicket(
				new ClaimsPrincipal(identity), AuthenticationSchemeName)));
	}
}

internal static class MoneyOrdersControllerTestsStaff
{
	public const string Id = "money-order-manager";
}
