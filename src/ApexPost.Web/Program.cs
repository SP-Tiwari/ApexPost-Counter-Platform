using ApexPost.Application.Persistence;
using ApexPost.Application.Security;
using ApexPost.Application.Services;
using ApexPost.Infrastructure.Persistence;
using ApexPost.Web.Authentication;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddAuthentication(options =>
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
	throw new InvalidOperationException(
		"Storage:Directory must be configured for JSON persistence.");

if (!Path.IsPathRooted(storageDirectory))
	storageDirectory = Path.Combine(
		builder.Environment.ContentRootPath,
		storageDirectory);

builder.Services.AddSingleton<IApexPostRepository>(
	_ => new JsonApexPostRepository(storageDirectory));
var credentialFile = builder.Configuration["Authentication:CredentialFile"];
if (string.IsNullOrWhiteSpace(credentialFile))
	throw new InvalidOperationException(
		"Authentication:CredentialFile must be configured.");
if (!Path.IsPathRooted(credentialFile))
	credentialFile = Path.Combine(
		builder.Environment.ContentRootPath,
		credentialFile);

builder.Services.AddSingleton<IStaffCredentialRepository>(
	_ => new JsonStaffCredentialRepository(credentialFile));
builder.Services.AddScoped<TransactionApplicationService>();
builder.Services.AddScoped<StaffAuthenticationService>();
builder.Services.AddScoped<StaffCookieAuthenticationEvents>();

var signInPermitLimit = builder.Configuration.GetValue<int>(
	"Authentication:SignInRateLimit:PermitLimit", 5);
var signInWindowSeconds = builder.Configuration.GetValue<int>(
	"Authentication:SignInRateLimit:WindowSeconds", 60);
if (signInPermitLimit <= 0 || signInWindowSeconds <= 0)
	throw new InvalidOperationException(
		"Sign-in rate limit and window settings must be positive.");

builder.Services.AddRateLimiter(options =>
{
	options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
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
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

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
