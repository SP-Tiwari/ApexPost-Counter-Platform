using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using ApexPost.Application.Persistence;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApexPost.Web.IntegrationTests;

public sealed class TransactionsControllerTests : IAsyncLifetime
{
	private string _storageDirectory = null!;
	private WebApplicationFactory<Program> _factory = null!;

	public Task InitializeAsync()
	{
		_storageDirectory = Path.Combine(
			Path.GetTempPath(), $"ApexPost-Web-{Guid.NewGuid():N}");
		_factory = new WebApplicationFactory<Program>()
			.WithWebHostBuilder(builder =>
			{
				builder.UseSetting("Storage:Directory", _storageDirectory);
				builder.ConfigureTestServices(services =>
					services.AddAuthentication(options =>
					{
						options.DefaultAuthenticateScheme = TestAuthenticationHandler.TestScheme;
						options.DefaultChallengeScheme = TestAuthenticationHandler.TestScheme;
						options.DefaultForbidScheme = TestAuthenticationHandler.TestScheme;
					}).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
						TestAuthenticationHandler.TestScheme, _ => { }));
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
	public async Task Index_RequiresBranchManagerRole()
	{
		var client = _factory.CreateClient();

		var anonymousResponse = await client.GetAsync("/Transactions");
		Assert.Equal(
			System.Net.HttpStatusCode.Unauthorized,
			anonymousResponse.StatusCode);

		client.DefaultRequestHeaders.Add("X-Test-Role", "CounterClerk");
		var clerkResponse = await client.GetAsync("/Transactions");
		Assert.Equal(System.Net.HttpStatusCode.Forbidden, clerkResponse.StatusCode);
	}

	[Fact]
	public async Task SignIn_WithoutCredentialProviderDoesNotAuthenticateSubmittedIdentity()
	{
		var storageDirectory = Path.Combine(
			Path.GetTempPath(), $"ApexPost-Web-NoAuth-{Guid.NewGuid():N}");
		await using var factory = new WebApplicationFactory<Program>()
			.WithWebHostBuilder(builder =>
			{
				builder.UseSetting("Storage:Directory", storageDirectory);
			});
		try
		{
			var client = factory.CreateClient(
				new WebApplicationFactoryClientOptions
				{
					AllowAutoRedirect = false
				});
			var response = await client.GetAsync("/Transactions");

			Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
			Assert.Equal(
				"/Account/SignIn",
				response.Headers.Location?.AbsolutePath);

			var signInResponse = await client.GetAsync("/Account/SignIn");
			Assert.Equal(System.Net.HttpStatusCode.OK, signInResponse.StatusCode);
			Assert.Contains(
				"no credential validation provider",
				await signInResponse.Content.ReadAsStringAsync(),
				StringComparison.OrdinalIgnoreCase);

			var submittedIdentity = await client.PostAsync(
				"/Account/SignIn",
				new FormUrlEncodedContent(
				[
					new KeyValuePair<string, string>("staffId", "manager-1"),
					new KeyValuePair<string, string>("role", "BranchManager")
				]));
			Assert.Equal(
				System.Net.HttpStatusCode.MethodNotAllowed,
				submittedIdentity.StatusCode);
		}
		finally
		{
			if (Directory.Exists(storageDirectory))
				Directory.Delete(storageDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task SignOut_RequiresAntiforgeryTokenAndClearsCookie()
	{
		var client = _factory.CreateClient(
			new WebApplicationFactoryClientOptions
			{
				AllowAutoRedirect = false
			});
		client.DefaultRequestHeaders.Add("X-Test-Role", "BranchManager");
		var page = await client.GetAsync("/Transactions");
		Assert.Equal(System.Net.HttpStatusCode.OK, page.StatusCode);
		var html = await page.Content.ReadAsStringAsync();
		var token = Regex.Match(
			html,
			"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")
			.Groups[1].Value;
		Assert.False(string.IsNullOrWhiteSpace(token));

		var invalidRequest = await client.PostAsync(
			"/Account/SignOut",
			new FormUrlEncodedContent([]));
		Assert.Equal(
			System.Net.HttpStatusCode.BadRequest,
			invalidRequest.StatusCode);

		var signOut = await client.PostAsync(
			"/Account/SignOut",
			new FormUrlEncodedContent(
			[
				new KeyValuePair<string, string>(
					"__RequestVerificationToken", token)
			]));

		Assert.Equal(System.Net.HttpStatusCode.Redirect, signOut.StatusCode);
		Assert.StartsWith(
			"/Account/SignIn",
			signOut.Headers.Location?.OriginalString);
		Assert.Contains(
			signOut.Headers.GetValues("Set-Cookie"),
			cookie => cookie.Contains(
				".AspNetCore.Cookies=",
				StringComparison.Ordinal) &&
				cookie.Contains("expires=", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task IndexAndDetails_ShowPersistedTransactionForManager()
	{
		var transactionId = Guid.NewGuid();
		var basket = TransactionBasket.Reconstitute(
			transactionId,
			"INR",
			new Dictionary<TaxCategory, decimal>
			{
				[TaxCategory.Standard] = 0.1m
			},
			[
				new TransactionLineItem(
					"Counter service",
					TransactionItemType.Service,
					new Money(100m, "INR"),
					1,
					TaxCategory.Standard)
			],
			TransactionPaymentStatus.Paid,
			[]);
		var repository = _factory.Services.GetRequiredService<IApexPostRepository>();
		await repository.SaveAsync(new ApexPostSnapshot(
			[], [], [], [], [], [], [], [], [basket]));

		var client = _factory.CreateClient();
		client.DefaultRequestHeaders.Add("X-Test-Role", "BranchManager");

		var listResponse = await client.GetAsync("/Transactions");
		Assert.Equal(System.Net.HttpStatusCode.OK, listResponse.StatusCode);
		var listContent = await listResponse.Content.ReadAsStringAsync();
		Assert.Contains(transactionId.ToString(), listContent);
		Assert.Contains("110", listContent);

		var detailsResponse = await client.GetAsync(
			$"/Transactions/{transactionId}");
		Assert.Equal(System.Net.HttpStatusCode.OK, detailsResponse.StatusCode);
		var detailsContent = await detailsResponse.Content.ReadAsStringAsync();
		Assert.Contains("Counter service", detailsContent);
		Assert.Contains("100", detailsContent);
		Assert.Contains("10", detailsContent);
	}

	[Fact]
	public async Task Details_WhenTransactionDoesNotExist_ReturnsNotFound()
	{
		var client = _factory.CreateClient();
		client.DefaultRequestHeaders.Add("X-Test-Role", "BranchManager");

		var response = await client.GetAsync($"/Transactions/{Guid.NewGuid()}");

		Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
	}

	private sealed class TestAuthenticationHandler(
		IOptionsMonitor<AuthenticationSchemeOptions> options,
		ILoggerFactory logger,
		UrlEncoder encoder)
		: AuthenticationHandler<AuthenticationSchemeOptions>(
			options, logger, encoder)
	{
		public const string TestScheme = "Test";

		protected override Task<AuthenticateResult> HandleAuthenticateAsync()
		{
			var role = Request.Headers["X-Test-Role"].ToString();
			if (string.IsNullOrWhiteSpace(role))
				return Task.FromResult(AuthenticateResult.NoResult());

			var identity = new ClaimsIdentity(
				[
					new Claim(ClaimTypes.NameIdentifier, "test-staff"),
					new Claim(ClaimTypes.Role, role)
				],
				TestScheme);
			var ticket = new AuthenticationTicket(
				new ClaimsPrincipal(identity), TestScheme);
			return Task.FromResult(AuthenticateResult.Success(ticket));
		}
	}
}
