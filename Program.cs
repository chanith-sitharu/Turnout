using System;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TurnoutStore.Data;
using TurnoutStore.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Forwarded Headers for Cloud Reverse Proxies (Render, Cloudflare, etc.)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// 1. Add MVC Controllers and Views
builder.Services.AddControllersWithViews();

// 2. Register Antiforgery with Custom Header support for AJAX/fetch calls
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
});

// 3. Register AppDbContext with SQL Server or PostgreSQL (Render/Cloud)
// Read cloud database environment variables FIRST (DATABASE_URL takes priority over local appsettings.json)
var rawConnection = Environment.GetEnvironmentVariable("DATABASE_URL")
                    ?? builder.Configuration["DATABASE_URL"]
                    ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                    ?? builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    var parsedConnection = ConvertPostgresUrlToConnectionString(rawConnection);
    if (!string.IsNullOrWhiteSpace(parsedConnection) && 
        (parsedConnection.StartsWith("Host=", StringComparison.OrdinalIgnoreCase) || 
         parsedConnection.Contains("Port=5432", StringComparison.OrdinalIgnoreCase) ||
         parsedConnection.Contains("Username=", StringComparison.OrdinalIgnoreCase)))
    {
        Console.WriteLine("[TurnoutStore] Configured PostgreSQL provider from DATABASE_URL.");
        options.UseNpgsql(parsedConnection);
    }
    else
    {
        Console.WriteLine("[TurnoutStore] Configured SQL Server provider.");
        options.UseSqlServer(rawConnection ?? "Server=(localdb)\\mssqllocaldb;Database=TurnoutStoreDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True");
    }
});

// 4. Register Application Services
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<IImageStorageService, ImageStorageService>();
builder.Services.AddSingleton<ICurrencyService, CurrencyService>();

// 5. Configure Cookie Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
    });

// 6. Configure Session State
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

var app = builder.Build();

// Forward headers from reverse proxy (Render / Cloudflare)
app.UseForwardedHeaders();

// 7. Apply Database Migrations and Seed Initial Data at Startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        var passwordService = services.GetRequiredService<IPasswordService>();
        DbInitializer.Initialize(context, builder.Configuration, logger, passwordService);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Database initialization could not complete at startup. Continuing web application startup.");
    }
}

// 8. Configure HTTP Request Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// UseSession is placed before UseAuthentication so session state is established
// and available to authentication events, custom claims transforms, and controllers.
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// Helper method to convert PostgreSQL URI (e.g. Render postgres:// URLs) into ADO.NET Npgsql format
static string? ConvertPostgresUrlToConnectionString(string? url)
{
    if (string.IsNullOrWhiteSpace(url)) return url;
    if (!url.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) && 
        !url.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        return url;
    }

    try
    {
        var uri = new Uri(url);
        var userInfo = uri.UserInfo.Split(':', 2);
        var username = Uri.UnescapeDataString(userInfo[0]);
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
        var host = uri.Host;
        var port = uri.Port > 0 ? uri.Port : 5432;
        var database = uri.AbsolutePath.TrimStart('/');

        return $"Host={host};Port={port};Database={database};Username={username};Password={password};SSL Mode=Prefer;Trust Server Certificate=true;";
    }
    catch
    {
        return url;
    }
}
