using System.Security.Claims;
using System.Text.Json;
using System.Text.Encodings.Web;
using ApexPost.Application.Auditing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApexPost.Web.IntegrationTests;

public sealed class AuditActionFilterTests : IAsyncLifetime
{
	private static readonly JsonSerializerOptions AuditJsonOptions = new()
	{
		PropertyNameCaseInsensitive = true
	};
	private string _storageDirectory = null!;
	private WebApplicationFactory<Program> _factory = null!;
	private string AuditFilePath =>
		Path.Combine(_storageDirectory, "audit-events.jsonl");

	public Task InitializeAsync()
	{
		_storageDirectory = Path.Combine(
			Path.GetTempPath(), $"ApexPost-Web-Audit-{Guid.NewGuid():N}");
		Directory.CreateDirectory(_storageDirectory);
		_factory = new WebApplicationFactory<Program>()
			.WithWebHostBuilder(builder =>
			{
				builder.UseSetting(
					"Storage:Directory", _storageDirectory);
				builder.UseSetting(
					"Authentication:CredentialFile",
					Path.Combine(_storageDirectory, "credentials.json"));
				builder.UseSetting("Audit:FilePath", AuditFilePath);
				builder.ConfigureTestServices(services =>
				{
					services.AddControllersWithViews()
						.AddApplicationPart(typeof(AuditProbeController).Assembly);
					services.AddAuthentication(options =>
					{
						options.DefaultAuthenticateScheme =
							AuditTestAuthenticationHandler.TestScheme;
						options.DefaultChallengeScheme =
							AuditTestAuthenticationHandler.TestScheme;
						options.DefaultForbidScheme =
							AuditTestAuthenticationHandler.TestScheme;
					}).AddScheme<AuthenticationSchemeOptions,
						AuditTestAuthenticationHandler>(
						AuditTestAuthenticationHandler.TestScheme,
						_ => { });
				});
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
	public async Task SuccessfulAnonymousAction_IsAuditedWithoutIdentityClaims()
	{
		const string correlationId = "audit-anonymous-correlation";
		var client = _factory.CreateClient();
		client.DefaultRequestHeaders.Add(
			"X-Correlation-ID", correlationId);

		var response = await client.GetAsync("/__test/audit/anonymous");

		Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
		var record = Assert.Single(await ReadAuditRecordsAsync());
		Assert.Equal("AuditProbe", record.Controller);
		Assert.Equal("Anonymous", record.Action);
		Assert.Equal("Succeeded", record.Outcome);
		Assert.Null(record.StaffId);
		Assert.Null(record.BranchCode);
		Assert.Equal(correlationId, record.CorrelationId);
		Assert.Equal(TimeSpan.Zero, record.TimestampUtc.Offset);
	}

	[Fact]
	public async Task AuthenticatedAction_RecordsTrustedStaffAndBranchClaims()
	{
		var client = _factory.CreateClient();
		client.DefaultRequestHeaders.Add(
			"X-Audit-StaffId", "verified-staff-007");
		client.DefaultRequestHeaders.Add(
			"X-Audit-BranchCode", "BR001");

		var response = await client.GetAsync("/__test/audit/authenticated");

		Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
		var record = Assert.Single(await ReadAuditRecordsAsync());
		Assert.Equal("verified-staff-007", record.StaffId);
		Assert.Equal("BR001", record.BranchCode);
	}

	[Fact]
	public async Task FailedAction_IsAuditedWithoutPersistingExceptionDetails()
	{
		var client = _factory.CreateClient();

		var response = await client.GetAsync("/__test/audit/failure");

		Assert.Equal(
			System.Net.HttpStatusCode.InternalServerError,
			response.StatusCode);
		var record = Assert.Single(await ReadAuditRecordsAsync());
		Assert.Equal("Failure", record.Action);
		Assert.Equal("Failed", record.Outcome);
		Assert.DoesNotContain(
			"private action failure",
			await File.ReadAllTextAsync(AuditFilePath),
			StringComparison.Ordinal);
	}

	private async Task<AuditRecord[]> ReadAuditRecordsAsync()
	{
		var lines = await File.ReadAllLinesAsync(AuditFilePath);
		return lines.Select(line =>
			JsonSerializer.Deserialize<AuditRecord>(
				line, AuditJsonOptions)
				?? throw new InvalidDataException(
					"Audit record could not be read."))
			.ToArray();
	}
}

[ApiController]
[Route("__test/audit")]
public sealed class AuditProbeController : ControllerBase
{
	[AllowAnonymous]
	[HttpGet("anonymous")]
	public IActionResult Anonymous() => Ok();

	[Authorize]
	[HttpGet("authenticated")]
	public IActionResult Authenticated() => Ok();

	[AllowAnonymous]
	[HttpGet("failure")]
	public IActionResult Failure() =>
		throw new InvalidOperationException("private action failure");
}

public sealed class AuditTestAuthenticationHandler(
	IOptionsMonitor<AuthenticationSchemeOptions> options,
	ILoggerFactory logger,
	UrlEncoder encoder)
	: AuthenticationHandler<AuthenticationSchemeOptions>(
		options, logger, encoder)
{
	public const string TestScheme = "AuditTest";

	protected override Task<AuthenticateResult> HandleAuthenticateAsync()
	{
		var staffId = Request.Headers["X-Audit-StaffId"].ToString();
		var branchCode = Request.Headers["X-Audit-BranchCode"].ToString();
		if (string.IsNullOrWhiteSpace(staffId))
			return Task.FromResult(AuthenticateResult.NoResult());

		var identity = new ClaimsIdentity(
			[
				new Claim("StaffId", staffId),
				new Claim("BranchCode", branchCode)
			],
			TestScheme);
		var principal = new ClaimsPrincipal(identity);
		var ticket = new AuthenticationTicket(principal, TestScheme);
		return Task.FromResult(AuthenticateResult.Success(ticket));
	}
}
