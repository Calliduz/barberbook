using BarberBook.Data;
using BarberBook.Data.Entities;
using BarberBook.Data.Identity;
using BarberBook.Web.Middleware;
using BarberBook.Web.Services;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MongoDB.Driver;
using MongoDB.EntityFrameworkCore.Extensions;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;

// Auto-load .env if present
var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (!File.Exists(envPath))
{
    var parentEnv = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", ".env");
    if (File.Exists(parentEnv)) envPath = parentEnv;
}
if (File.Exists(envPath))
{
    foreach (var line in File.ReadAllLines(envPath))
    {
        var trimmed = line.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#")) continue;
        var parts = trimmed.Split('=', 2);
        if (parts.Length == 2)
        {
            var key = parts[0].Trim();
            var val = parts[1].Trim().Trim('"').Trim('\'');
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
            {
                Environment.SetEnvironmentVariable(key, val);
            }
        }
    }
}

var builder = WebApplication.CreateBuilder(args);

// Disable Kestrel Server header for security reconnaissance defense
builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
});

// 1. Database & EF Core (MongoDB Atlas)
var mongoUri = builder.Configuration.GetConnectionString("MongoConnection")
    ?? Environment.GetEnvironmentVariable("MONGODB_URI")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__MongoConnection")
    ?? "mongodb+srv://cabunocjoshlisteristuris_db_user:QtjA8IW2dDSZkdVF@cluster0.ivvillv.mongodb.net/?appName=Cluster0";
var databaseName = builder.Configuration["DatabaseName"]
    ?? Environment.GetEnvironmentVariable("MONGODB_DB_NAME")
    ?? "barberbook";

var mongoClient = new MongoClient(mongoUri);
builder.Services.AddSingleton<IMongoClient>(mongoClient);

builder.Services.AddDbContext<BarberBookDbContext>(options =>
{
    options.UseMongoDB(mongoClient, databaseName);
    options.ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
});

// 2. ASP.NET Core Identity with document-adapted BarberBookUserStore & BarberBookRoleStore
builder.Services.AddIdentity<ApplicationUser, IdentityRole<int>>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 8;
    options.User.RequireUniqueEmail = true;

    // Lockout protection against brute-force attacks
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
})
.AddUserStore<BarberBookUserStore>()
.AddRoleStore<BarberBookRoleStore>()
.AddDefaultTokenProviders();

// Configure Application Cookie
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(4);
    options.SlidingExpiration = true;

    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api") || context.Request.Headers.Accept.ToString().Contains("application/json"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };

    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api") || context.Request.Headers.Accept.ToString().Contains("application/json"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});

// 3. Authorization Policies (Roles: Customer, Barber, Admin)
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdminRole", policy => policy.RequireRole("Admin"));
    options.AddPolicy("RequireBarberRole", policy => policy.RequireRole("Barber"));
    options.AddPolicy("RequireCustomerRole", policy => policy.RequireRole("Customer"));
});

// 4. NodaTime Clock & Timezone
builder.Services.AddSingleton<IClock>(SystemClock.Instance);
builder.Services.AddSingleton<DateTimeZone>(DateTimeZoneProviders.Tzdb["Asia/Manila"]);

// 5. Application Services
builder.Services.AddScoped<IAvailabilityService, AvailabilityService>();
builder.Services.AddScoped<IAppointmentTransitionService, AppointmentTransitionService>();
builder.Services.AddScoped<IReportingService, ReportingService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IEquipmentService, EquipmentService>();
builder.Services.AddScoped<IPurchaseSaleService, PurchaseSaleService>();

// 6. Rate Limiter for sensitive auth endpoints
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("auth-policy", opt =>
    {
        opt.PermitLimit = builder.Environment.IsProduction() ? 15 : 2000;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });
});

// Strict CORS Policy for AJAX Endpoints
builder.Services.AddCors(options =>
{
    options.AddPolicy("StrictCorsPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:5000", "https://localhost:5001", "http://127.0.0.1:5000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// 7. MVC & Controllers
builder.Services.AddControllersWithViews();

// 8. FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

var app = builder.Build();

// CLI seed support: dotnet run -- --seed
if (args.Contains("--seed"))
{
    using var seedScope = app.Services.CreateScope();
    await BarberBook.Data.Seeding.DatabaseSeeder.SeedAsync(seedScope.ServiceProvider);
    return;
}

// Development automatic schema migration & seed hygiene
if (app.Environment.IsDevelopment())
{
    try
    {
        using var scope = app.Services.CreateScope();
        await BarberBook.Data.Seeding.DatabaseSeeder.SeedAsync(scope.ServiceProvider);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Initial database migration/seeding skipped (database unreachable).");
    }
}

// Security Headers & Centralized Error Handler Middleware
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<ApiExceptionMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/StatusCodePage", "?code={0}");

app.UseStaticFiles();
app.UseRouting();

app.UseCors("StrictCorsPolicy");
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// Route Configuration: Areas and Default
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// Required for WebApplicationFactory in integration tests
public partial class Program { }

