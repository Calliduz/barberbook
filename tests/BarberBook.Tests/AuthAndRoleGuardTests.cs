using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BarberBook.Data;
using BarberBook.Data.Entities;
using BarberBook.Data.Enums;
using BarberBook.Data.Seeding;
using BarberBook.Web.Common;
using BarberBook.Web.Models.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BarberBook.Tests;

public class AuthAndRoleGuardTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AuthAndRoleGuardTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private async Task EnsureSeededAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await DatabaseSeeder.SeedAsync(scope.ServiceProvider);
    }

    [Fact]
    public async Task CF1_SC1_Register_CreatesCustomerOnly_EvenIfAdminRoleSent()
    {
        // Arrange
        await EnsureSeededAsync();
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var uniqueEmail = $"attacker_{Guid.NewGuid():N}@test.com";
        var payload = new RegisterModel
        {
            FullName = "Malicious Admin Wannabe",
            Email = uniqueEmail,
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            Role = "admin" // Attempting privilege escalation
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/register", payload);

        // Assert: Created (201)
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(uniqueEmail);

        user.Should().NotBeNull();
        user!.Role.Should().Be(UserRole.Customer, "Role must be Customer regardless of request payload");
        (await userManager.IsInRoleAsync(user, "Customer")).Should().BeTrue();
        (await userManager.IsInRoleAsync(user, "Admin")).Should().BeFalse();
    }

    [Fact]
    public async Task CF1_Register_RejectsDuplicateEmail()
    {
        // Arrange: Use existing seeded customer
        await EnsureSeededAsync();
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var payload = new RegisterModel
        {
            FullName = "Juan Duplicate",
            Email = "juan@barberbook.local", // Existing email
            Password = "Password123!",
            ConfirmPassword = "Password123!"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/register", payload);

        // Assert: 409 Conflict
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var content = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        content.Should().NotBeNull();
        content!.Error.Code.Should().Be("DUPLICATE_EMAIL");
    }

    [Fact]
    public async Task CF1_BF1_Login_AuthenticatesValidUser_AndIssuesCookie()
    {
        // Arrange
        await EnsureSeededAsync();
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var loginPayload = new LoginModel
        {
            Email = "ramon@barberbook.local",
            Password = "Demo@BarberBook2026!"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", loginPayload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        cookies.Should().Contain(c => c.Contains(".AspNetCore.Cookies") || c.Contains(".AspNetCore.Identity.Application"));

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("redirect_url").GetString().Should().Be("/barber");
        body.GetProperty("user").GetProperty("role").GetString().Should().Be("barber");
    }

    [Fact]
    public async Task BR13_Login_RejectsInactiveUser()
    {
        // Arrange: Create or deactivate a user
        await EnsureSeededAsync();
        var email = $"inactive_{Guid.NewGuid():N}@test.com";

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = "Inactive Test User",
                Role = UserRole.Customer,
                IsActive = false // Deactivated
            };
            await userManager.CreateAsync(user, "Password123!");
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginModel
        {
            Email = email,
            Password = "Password123!"
        });

        // Assert: 401 Unauthorized with ACCOUNT_INACTIVE code
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var content = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        content!.Error.Code.Should().Be("ACCOUNT_INACTIVE");
    }

    [Fact]
    public async Task Login_RejectsInvalidCredentials_WithGenericMessage()
    {
        // Arrange
        await EnsureSeededAsync();
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginModel
        {
            Email = "nonexistent@barberbook.local",
            Password = "WrongPassword123!"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var content = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        content!.Error.Code.Should().Be("INVALID_CREDENTIALS");
        content.Error.Message.Should().Be("Invalid email or password.");
    }

    [Fact]
    public async Task Me_ReturnsCurrentAuthenticatedUser_WhenLoggedIn()
    {
        // Arrange
        await EnsureSeededAsync();
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

        // Login as admin
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginModel
        {
            Email = "admin@barberbook.local",
            Password = "Admin@BarberBook2026!"
        });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act: Call Me
        var meResponse = await client.GetAsync("/api/auth/me");

        // Assert
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var user = await meResponse.Content.ReadFromJsonAsync<CurrentUserDto>();
        user.Should().NotBeNull();
        user!.Email.Should().Be("admin@barberbook.local");
        user.Role.Should().Be("admin");
        user.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Logout_ClearsSession_AndPreventsSubsequentAccess()
    {
        // Arrange
        await EnsureSeededAsync();
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

        await client.PostAsJsonAsync("/api/auth/login", new LoginModel
        {
            Email = "admin@barberbook.local",
            Password = "Admin@BarberBook2026!"
        });

        // Verify authenticated
        (await client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);

        // Act: Logout
        var logoutResponse = await client.PostAsync("/api/auth/logout", null);
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert: /api/auth/me is now unauthorized
        var afterLogoutResponse = await client.GetAsync("/api/auth/me");
        afterLogoutResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task T20_RouteGuard_EnforcesAuthorizationAcrossAllPortals()
    {
        // Proves T-20: Role guards reject unauthorized users across roles
        await EnsureSeededAsync();

        // 1. Anonymous access to protected areas redirects to login (or 401/403)
        var anonClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var adminResponse = await anonClient.GetAsync("/admin");
        adminResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        adminResponse.Headers.Location!.ToString().Should().Contain("/Account/Login");

        var barberResponse = await anonClient.GetAsync("/barber");
        barberResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        barberResponse.Headers.Location!.ToString().Should().Contain("/Account/Login");

        // 2. Customer access to /barber or /admin returns 403 Forbidden / AccessDenied
        var customerClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });
        await customerClient.PostAsJsonAsync("/api/auth/login", new LoginModel
        {
            Email = "juan@barberbook.local",
            Password = "Demo@BarberBook2026!"
        });

        var customerToBarber = await customerClient.GetAsync("/barber");
        // Cookie auth redirects to AccessDenied on forbidden (302 -> /Account/AccessDenied) or returns 403
        if (customerToBarber.StatusCode == HttpStatusCode.Redirect)
        {
            customerToBarber.Headers.Location!.ToString().Should().Contain("AccessDenied");
        }
        else
        {
            customerToBarber.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        var customerToAdmin = await customerClient.GetAsync("/admin");
        if (customerToAdmin.StatusCode == HttpStatusCode.Redirect)
        {
            customerToAdmin.Headers.Location!.ToString().Should().Contain("AccessDenied");
        }
        else
        {
            customerToAdmin.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        // 3. Barber access to /admin returns 403 Forbidden / AccessDenied
        var barberClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });
        await barberClient.PostAsJsonAsync("/api/auth/login", new LoginModel
        {
            Email = "ramon@barberbook.local",
            Password = "Demo@BarberBook2026!"
        });

        var barberToAdmin = await barberClient.GetAsync("/admin");
        if (barberToAdmin.StatusCode == HttpStatusCode.Redirect)
        {
            barberToAdmin.Headers.Location!.ToString().Should().Contain("AccessDenied");
        }
        else
        {
            barberToAdmin.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        // Barber CAN access /barber
        var barberToBarber = await barberClient.GetAsync("/barber");
        barberToBarber.StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. Admin CAN access /admin
        var adminClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });
        await adminClient.PostAsJsonAsync("/api/auth/login", new LoginModel
        {
            Email = "admin@barberbook.local",
            Password = "Admin@BarberBook2026!"
        });

        var adminToAdmin = await adminClient.GetAsync("/admin");
        adminToAdmin.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public void T20_RouteTable_WalksRegisteredEndpoints_AndAssertsGuards()
    {
        // Prove T-20: inspect endpoint data source to assert guards on all registered endpoints
        var endpointDataSources = _factory.Services.GetServices<EndpointDataSource>();
        var endpoints = endpointDataSources.SelectMany(ds => ds.Endpoints).OfType<RouteEndpoint>().ToList();

        endpoints.Should().NotBeEmpty();

        foreach (var endpoint in endpoints)
        {
            var routePattern = endpoint.RoutePattern.RawText ?? string.Empty;
            var actionDescriptor = endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor>();

            // Admin portal routes or controllers in Admin area must have Admin role policy or authorize attribute
            var area = actionDescriptor?.RouteValues.TryGetValue("area", out var a) == true ? a : null;
            var isAreaAdmin = string.Equals(area, "Admin", StringComparison.OrdinalIgnoreCase);
            if (isAreaAdmin ||
                routePattern.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
                routePattern.StartsWith("admin/", StringComparison.OrdinalIgnoreCase) ||
                routePattern.StartsWith("api/admin", StringComparison.OrdinalIgnoreCase))
            {
                var authorizeData = endpoint.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>();
                authorizeData.Should().NotBeEmpty($"Route '{routePattern}' in admin portal must have an [Authorize] guard");
                authorizeData.Any(a => a.Roles?.Contains("Admin") == true).Should().BeTrue($"Route '{routePattern}' must require 'Admin' role");
            }

            // Barber portal routes or controllers in Barber area must have Barber role policy or authorize attribute
            var isAreaBarber = string.Equals(area, "Barber", StringComparison.OrdinalIgnoreCase);
            if (isAreaBarber ||
                routePattern.Equals("barber", StringComparison.OrdinalIgnoreCase) ||
                routePattern.StartsWith("barber/", StringComparison.OrdinalIgnoreCase) ||
                routePattern.Equals("api/barber", StringComparison.OrdinalIgnoreCase) ||
                routePattern.StartsWith("api/barber/", StringComparison.OrdinalIgnoreCase))
            {
                var authorizeData = endpoint.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>();
                authorizeData.Should().NotBeEmpty($"Route '{routePattern}' in barber portal must have an [Authorize] guard");
                authorizeData.Any(a => a.Roles?.Contains("Barber") == true).Should().BeTrue($"Route '{routePattern}' must require 'Barber' role");
            }

            // Customer-only endpoints
            if (routePattern.Equals("book", StringComparison.OrdinalIgnoreCase) ||
                routePattern.Equals("my-appointments", StringComparison.OrdinalIgnoreCase) ||
                routePattern.Equals("profile", StringComparison.OrdinalIgnoreCase) ||
                routePattern.Equals("api/me/profile", StringComparison.OrdinalIgnoreCase) ||
                routePattern.Equals("api/me/appointments", StringComparison.OrdinalIgnoreCase))
            {
                var authorizeData = endpoint.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>();
                authorizeData.Should().NotBeEmpty($"Route '{routePattern}' must have an [Authorize] guard");
                authorizeData.Any(a => a.Roles?.Contains("Customer") == true).Should().BeTrue($"Route '{routePattern}' must require 'Customer' role");
            }
        }
    }

    [Fact]
    public async Task P20_SecurityHeaders_EmittedOnEveryResponse()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/");

        // Assert
        response.Headers.Contains("X-Content-Type-Options").Should().BeTrue();
        response.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");

        response.Headers.Contains("X-Frame-Options").Should().BeTrue();
        response.Headers.GetValues("X-Frame-Options").Should().Contain("DENY");

        response.Headers.Contains("Referrer-Policy").Should().BeTrue();
        response.Headers.GetValues("Referrer-Policy").Should().Contain("strict-origin-when-cross-origin");

        response.Headers.Contains("Content-Security-Policy").Should().BeTrue();
    }
}
