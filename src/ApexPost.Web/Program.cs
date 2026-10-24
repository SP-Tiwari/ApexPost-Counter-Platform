using ApexPost.Application.Persistence;
using ApexPost.Application.Services;
using ApexPost.Infrastructure.Persistence;
using ApexPost.Web.Authentication;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddAuthentication(options =>
	{
		options.DefaultScheme = UnconfiguredAuthenticationHandler.SchemeName;
	})
	.AddScheme<AuthenticationSchemeOptions, UnconfiguredAuthenticationHandler>(
		UnconfiguredAuthenticationHandler.SchemeName,
		_ => { });
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
builder.Services.AddScoped<TransactionApplicationService>();

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

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();

public partial class Program { }
