using ApexPost.Application.Persistence;
using ApexPost.Application.Auditing;
using ApexPost.Application.Security;
using ApexPost.Application.Services;
using ApexPost.Infrastructure.Persistence;
using ApexPost.Web.Authentication;
using ApexPost.Web.Filters;
using ApexPost.Web.Middleware;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
	options.Filters.AddService<AuditActionFilter>());

builder.Services.AddProblemDetails(options =>
{
	options.CustomizeProblemDetails = context =>
	{
		context.ProblemDetails.Title = "An unexpected error occurred.";

		SecurityHeadersMiddleware.ApplyHeaders(
			context.HttpContext.Response);

		if (context.HttpContext.Items.TryGetValue(
			CorrelationIdMiddleware.HttpContextItemKey,
			out var correlationId))
		{
			context.HttpContext.Response.Headers[
				CorrelationIdMiddleware.HeaderName] =
				correlationId?.ToString();

			context.ProblemDetails.Extensions["correlationId"] =
				correlationId;
		}
	};
});

builder.Services
	.AddAuthentication(options =>
	{
		options.DefaultScheme =
			CookieAuthenticationDefaults.AuthenticationScheme;
	})
	.AddCookie(options =>
	{
		options.LoginPath = "/Account/SignIn";
		options.AccessDeniedPath = "/Account/AccessDenied";
		options.Cookie.HttpOnly = true;
		options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
		options.Cookie.SameSite = SameSiteMode.Lax;
		options.ExpireTimeSpan = TimeSpan.FromHours(8);
		options.SlidingExpiration = true;
		options.EventsType = typeof(StaffCookieAuthenticationEvents);
	});

builder.Services.AddAuthorization();

var storageDirectory = builder.Configuration["Storage:Directory"];

if (string.IsNullOrWhiteSpace(storageDirectory))
{
	throw new InvalidOperationException(
		"Storage:Directory must be configured for JSON persistence.");
}

if (!Path.IsPathRooted(storageDirectory))
{
	storageDirectory = Path.Combine(
		builder.Environment.ContentRootPath,
		storageDirectory);
}

builder.Services.AddSingleton<IApexPostRepository>(
	_ => new JsonApexPostRepository(storageDirectory));

var credentialFile =
	builder.Configuration["Authentication:CredentialFile"];

if (string.IsNullOrWhiteSpace(credentialFile))
{
	throw new InvalidOperationException(
		"Authentication:CredentialFile must be configured.");
}

if (!Path.IsPathRooted(credentialFile))
{
	credentialFile = Path.Combine(
		builder.Environment.ContentRootPath,
		credentialFile);
}

builder.Services.AddSingleton<IStaffCredentialRepository>(
	_ => new JsonStaffCredentialRepository(credentialFile));

builder.Services.AddScoped<TransactionApplicationService>();
builder.Services.AddScoped<StaffAuthenticationService>();
builder.Services.AddScoped<StaffCookieAuthenticationEvents>();
builder.Services.AddScoped<AuditActionFilter>();

var auditFile = builder.Configuration["Audit:FilePath"];

if (string.IsNullOrWhiteSpace(auditFile))
{
	auditFile = Path.Combine(storageDirectory, "audit-events.jsonl");
}
else if (!Path.IsPathRooted(auditFile))
{
	auditFile = Path.Combine(
		builder.Environment.ContentRootPath,
		auditFile);
}

builder.Services.AddSingleton<IAuditStore>(
	_ => new JsonAuditStore(auditFile));

var signInPermitLimit = builder.Configuration.GetValue<int>(
	"Authentication:SignInRateLimit:PermitLimit", 5);

var signInWindowSeconds = builder.Configuration.GetValue<int>(
	"Authentication:SignInRateLimit:WindowSeconds", 60);

if (signInPermitLimit <= 0 || signInWindowSeconds <= 0)
{
	throw new InvalidOperationException(
		"Sign-in rate limit and window settings must be positive.");
}

builder.Services.AddRateLimiter(options =>
{
	options.RejectionStatusCode =
		StatusCodes.Status429TooManyRequests;

	options.AddPolicy("staff-sign-in", context =>
		RateLimitPartition.GetFixedWindowLimiter(
			context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
			_ => new FixedWindowRateLimiterOptions
			{
				PermitLimit = signInPermitLimit,
				Window = TimeSpan.FromSeconds(signInWindowSeconds),
				QueueLimit = 0,
				AutoReplenishment = true
			}));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseMiddleware<CorrelationIdMiddleware>();

app.UseExceptionHandler();

if (!app.Environment.IsDevelopment())
{
	// Add HSTS in non-development environments.
	app.UseHsts();
}

app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseMiddleware<MaintenanceModeMiddleware>();

app.UseHttpsRedirection();
app.UseRouting();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
	name: "default",
	pattern: "{controller=Home}/{action=Index}/{id?}")
	.WithStaticAssets();

app.Run();

public partial class Program { }