using System.Globalization;
using ApexPost.Application.Persistence;
using ApexPost.Application.Auditing;
using ApexPost.Application.Security;
using ApexPost.Application.Services;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;
using ApexPost.Infrastructure.Persistence;
using ApexPost.Web.Authentication;
using ApexPost.Web.Filters;
using ApexPost.Web.Middleware;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
	options.Filters.AddService<AuditActionFilter>();
	options.Filters.AddService<ValidationActionFilter>();
	options.Filters.AddService<PerformanceActionFilter>();
});

builder.Services.Configure<ApiBehaviorOptions>(options =>
	options.SuppressModelStateInvalidFilter = true);

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

var parcelTrackingOrigins = builder.Configuration
	.GetSection("ParcelTracking:AllowedOrigins")
	.Get<string[]>() ?? [];
foreach (var configuredOrigin in parcelTrackingOrigins)
{
	if (!Uri.TryCreate(configuredOrigin, UriKind.Absolute, out var origin) ||
		(origin.Scheme != Uri.UriSchemeHttp &&
			origin.Scheme != Uri.UriSchemeHttps) ||
		!string.IsNullOrEmpty(origin.UserInfo) ||
		origin.AbsolutePath != "/" ||
		!string.IsNullOrEmpty(origin.Query) ||
		!string.IsNullOrEmpty(origin.Fragment) ||
		configuredOrigin.Contains('*'))
	{
		throw new InvalidOperationException(
			"ParcelTracking:AllowedOrigins must contain exact HTTP or HTTPS origins without paths, credentials, queries, fragments, or wildcards.");
	}
}

builder.Services.AddCors(options =>
	options.AddPolicy("ParcelTracking", policy =>
	{
		if (parcelTrackingOrigins.Length > 0)
		{
			policy.WithOrigins(parcelTrackingOrigins)
				.WithMethods("GET")
				.WithHeaders("Accept");
		}
	}));

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
builder.Services.AddSingleton<DemoDataInitializer>();

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
builder.Services.AddScoped<ValidationActionFilter>();

var slowActionThresholdMilliseconds = builder.Configuration.GetValue<double>(
	"Performance:SlowActionThresholdMilliseconds", 500d);
var slowActionThreshold = PerformanceActionFilter.ParseThresholdMilliseconds(
	slowActionThresholdMilliseconds);

builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddScoped<PerformanceActionFilter>(services =>
	new PerformanceActionFilter(
		slowActionThreshold,
		services.GetRequiredService<TimeProvider>(),
		services.GetRequiredService<ILogger<PerformanceActionFilter>>()));

var moneyOrderThresholdAmount =
	builder.Configuration["MoneyOrders:HighValueThreshold:Amount"];
var moneyOrderThresholdCurrency =
	builder.Configuration["MoneyOrders:HighValueThreshold:Currency"];
Money? moneyOrderHighValueThreshold = null;

if (!string.IsNullOrWhiteSpace(moneyOrderThresholdAmount) ||
	!string.IsNullOrWhiteSpace(moneyOrderThresholdCurrency))
{
	if (string.IsNullOrWhiteSpace(moneyOrderThresholdAmount) ||
		string.IsNullOrWhiteSpace(moneyOrderThresholdCurrency) ||
		!decimal.TryParse(
			moneyOrderThresholdAmount,
			NumberStyles.Number,
			CultureInfo.InvariantCulture,
			out var thresholdAmount))
	{
		throw new InvalidOperationException(
			"Configure both MoneyOrders:HighValueThreshold:Amount and Currency with valid values.");
	}

	moneyOrderHighValueThreshold = new Money(
		thresholdAmount, moneyOrderThresholdCurrency);
}

builder.Services.AddScoped<MoneyOrderApplicationService>(services =>
	new MoneyOrderApplicationService(
		services.GetRequiredService<IApexPostRepository>(),
		moneyOrderHighValueThreshold));

var configuredTaxRates = new Dictionary<TaxCategory, decimal>();
foreach (var taxCategory in Enum.GetValues<TaxCategory>())
{
	var configuredRate = builder.Configuration[
		$"Transactions:TaxRates:{taxCategory}"];
	if (configuredRate is null)
		continue;
	if (!decimal.TryParse(
		configuredRate,
		NumberStyles.Number,
		CultureInfo.InvariantCulture,
		out var taxRate) ||
		taxRate < 0m)
	{
		throw new InvalidOperationException(
			$"Transactions:TaxRates:{taxCategory} must be a non-negative decimal.");
	}

	configuredTaxRates.Add(taxCategory, taxRate);
}

var configuredMaximumParcelWeights =
	new Dictionary<ShippingServiceLevel, Weight>();
foreach (var serviceLevel in Enum.GetValues<ShippingServiceLevel>())
{
	var configuredWeight = builder.Configuration[
		$"Shipping:MaximumParcelWeightKilograms:{serviceLevel}"];
	if (configuredWeight is null)
		continue;
	if (!decimal.TryParse(
		configuredWeight,
		NumberStyles.Number,
		CultureInfo.InvariantCulture,
		out var kilograms) ||
		kilograms <= 0m)
	{
		throw new InvalidOperationException(
			$"Shipping:MaximumParcelWeightKilograms:{serviceLevel} must be a positive decimal.");
	}

	configuredMaximumParcelWeights.Add(
		serviceLevel, new Weight(kilograms));
}

TaxCategory? configuredShippingTaxCategory = null;
var configuredShippingTaxCategoryValue =
	builder.Configuration["Shipping:TaxCategory"];
if (configuredShippingTaxCategoryValue is not null)
{
	if (!Enum.TryParse<TaxCategory>(
		configuredShippingTaxCategoryValue,
		ignoreCase: true,
		out var shippingTaxCategory) ||
		!Enum.IsDefined(shippingTaxCategory))
	{
		throw new InvalidOperationException(
			"Shipping:TaxCategory must name a defined tax category.");
	}

	configuredShippingTaxCategory = shippingTaxCategory;
}

builder.Services.AddScoped<MixedBasketApplicationService>(services =>
	new MixedBasketApplicationService(
		services.GetRequiredService<IApexPostRepository>(),
		configuredTaxRates,
		configuredMaximumParcelWeights,
		configuredShippingTaxCategory));

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

var protectedPartnerPathPrefixes = builder.Configuration
	.GetSection("ApiSecurity:ProtectedPathPrefixes")
	.Get<string[]>() ?? [];
PartitionedRateLimiter<HttpContext>? partnerRateLimiter = null;
if (protectedPartnerPathPrefixes.Length > 0)
{
	var partnerPermitLimit = builder.Configuration.GetValue<int?>(
		"ApiSecurity:PartnerRateLimit:PermitLimit");
	var partnerWindowSeconds = builder.Configuration.GetValue<int?>(
		"ApiSecurity:PartnerRateLimit:WindowSeconds");
	if (partnerPermitLimit is null or <= 0 ||
		partnerWindowSeconds is null or <= 0)
	{
		throw new InvalidOperationException(
			"Configure positive ApiSecurity:PartnerRateLimit:PermitLimit and WindowSeconds when partner path prefixes are enabled.");
	}

	partnerRateLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
		context =>
		{
			if (!context.Items.TryGetValue(
				PartnerRateLimitingMiddleware.PartnerIdentityItemKey,
				out var identity) ||
				identity is not string partnerIdentity)
			{
				return RateLimitPartition.GetNoLimiter("unvalidated");
			}

			return RateLimitPartition.GetFixedWindowLimiter(
				partnerIdentity,
				_ => new FixedWindowRateLimiterOptions
				{
					PermitLimit = partnerPermitLimit.Value,
					Window = TimeSpan.FromSeconds(partnerWindowSeconds.Value),
					QueueLimit = 0,
					AutoReplenishment = true
				});
		});
	builder.Services.AddSingleton(partnerRateLimiter);
}

var app = builder.Build();

if (app.Configuration.GetValue<bool>("DemoData:Initialize"))
{
	await app.Services
		.GetRequiredService<DemoDataInitializer>()
		.InitializeAsync();
}

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
app.UseCors("ParcelTracking");

app.UseMiddleware<ApiKeyAndIpRestrictionMiddleware>();
if (partnerRateLimiter is not null)
	app.UseMiddleware<PartnerRateLimitingMiddleware>();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.MapControllerRoute(
	name: "default",
	pattern: "{controller=Home}/{action=Index}/{id?}")
	.WithStaticAssets();

app.Run();

public partial class Program { }