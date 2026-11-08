using System.Net;
using System.Net.Http.Json;
using BarberBook.Data;
using BarberBook.Data.Entities;
using BarberBook.Data.Enums;
using BarberBook.Data.Seeding;
using BarberBook.Web.Common;
using BarberBook.Web.Models.Admin;
using BarberBook.Web.Models.Appointment;
using BarberBook.Web.Models.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Xunit;

namespace BarberBook.Tests;

public class AdminCustomerInformationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private const string AdminEmail = "admin@barberbook.local";
    private const string AdminPassword = "Admin@BarberBook2026!";
    private const string DemoPassword = "Demo@BarberBook2026!";

    public AdminCustomerInformationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private async Task EnsureSeededAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await DatabaseSeeder.SeedAsync(scope.ServiceProvider);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string email, string password)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginModel
        {
            Email = email,
            Password = password
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return client;
    }

    [Fact]
    public async Task AF5_SC5_AdminCanListAndSearchCustomers_WithPagination()
    {
        await EnsureSeededAsync();
        var adminClient = await CreateAuthenticatedClientAsync(AdminEmail, AdminPassword);

        // 1. Search by name (Juan)
        var searchResp = await adminClient.GetAsync("/api/admin/customers?q=Juan");
        searchResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var searchResult = await searchResp.Content.ReadFromJsonAsync<AdminCustomerPagedResultDto>();
        searchResult.Should().NotBeNull();
        searchResult!.Items.Should().Contain(c => c.FullName.Contains("Juan"));

        // 2. Pagination test: pageSize = 2
        var pagedResp = await adminClient.GetAsync("/api/admin/customers?page=1&pageSize=2");
        pagedResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var pagedResult = await pagedResp.Content.ReadFromJsonAsync<AdminCustomerPagedResultDto>();
        pagedResult!.Items.Count.Should().BeLessOrEqualTo(2);
        pagedResult.TotalCount.Should().BeGreaterThan(2);

        // 3. MVC HTML View test
        var htmlResp = await adminClient.GetAsync("/admin/customers?q=Juan");
        htmlResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await htmlResp.Content.ReadAsStringAsync();
        html.Should().Contain("Customer Management");
        html.Should().Contain("Juan");
    }

    [Fact]
    public async Task AF5_T09_RP3_AdminCanViewCustomerDetail_WithSummaryAndHistory()
    {
        await EnsureSeededAsync();
        var adminClient = await CreateAuthenticatedClientAsync(AdminEmail, AdminPassword);

        int juanId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
            juanId = (await db.Users.FirstAsync(u => u.Email == "juan@barberbook.local")).Id;
        }

        var detailResp = await adminClient.GetAsync($"/api/admin/customers/{juanId}");
        detailResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var doc = await detailResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        doc.GetProperty("fullName").GetString().Should().Be("Juan Dela Cruz");
        doc.GetProperty("email").GetString().Should().Be("juan@barberbook.local");
        doc.GetProperty("totalAppointments").GetInt32().Should().BeGreaterThan(0);
        doc.GetProperty("totalSpent").GetDecimal().Should().BeGreaterThan(0m);

        // Appointments array with status timeline
        var apps = doc.GetProperty("appointments");
        apps.GetArrayLength().Should().BeGreaterThan(0);

        // MVC View test
        var htmlResp = await adminClient.GetAsync($"/admin/customers/{juanId}");
        htmlResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await htmlResp.Content.ReadAsStringAsync();
        html.Should().Contain("Juan Dela Cruz");
        html.Should().Contain("Client Appointment History");
        html.Should().Contain("Transactions");
    }

    [Fact]
    public async Task AF5_AdminCanUpdateCustomerProfile_NameEmailPhone()
    {
        await EnsureSeededAsync();
        var adminClient = await CreateAuthenticatedClientAsync(AdminEmail, AdminPassword);

        // Create an isolated customer to update
        int isolatedCustomerId;
        string origEmail = $"edit_test_{Guid.NewGuid():N}@barberbook.local";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();

            var u = new ApplicationUser
            {
                FullName = "Initial Name",
                Email = origEmail,
                UserName = origEmail,
                PhoneNumber = "09111111111",
                Role = UserRole.Customer,
                IsActive = true,
                CreatedAt = clock.GetCurrentInstant()
            };
            db.Users.Add(u);
            await db.SaveChangesAsync();
            isolatedCustomerId = u.Id;
        }

        // 1. Update customer profile
        string updatedEmail = $"updated_{Guid.NewGuid():N}@barberbook.local";
        var editPayload = new AdminCustomerEditProfileModel
        {
            FullName = "Updated Brand New Name",
            Email = updatedEmail,
            Phone = "09998887777"
        };

        var editResp = await adminClient.PutAsJsonAsync($"/api/admin/customers/{isolatedCustomerId}", editPayload);
        editResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify in DB
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
            var customer = await db.Users.FindAsync(isolatedCustomerId);
            customer!.FullName.Should().Be("Updated Brand New Name");
            customer.Email.Should().Be(updatedEmail);
            customer.PhoneNumber.Should().Be("09998887777");
        }

        // 2. Duplicate email should return 409 Conflict
        var dupPayload = new AdminCustomerEditProfileModel
        {
            FullName = "Another Person",
            Email = "juan@barberbook.local", // Already taken by Juan
            Phone = "09990001111"
        };
        var dupResp = await adminClient.PutAsJsonAsync($"/api/admin/customers/{isolatedCustomerId}", dupPayload);
        dupResp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AF5_BR13_AdminCanDeactivateAndReactivateCustomer_DeactivationBlocksLoginAndBooking()
    {
        await EnsureSeededAsync();
        var adminClient = await CreateAuthenticatedClientAsync(AdminEmail, AdminPassword);

        // Create a dedicated customer for deactivation testing
        int testCustId;
        string testEmail = $"deact_cust_{Guid.NewGuid():N}@barberbook.local";
        using (var scope = _factory.Services.CreateScope())
        {
            var userMgr = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();

            var u = new ApplicationUser
            {
                FullName = "Deactivation Subject",
                Email = testEmail,
                UserName = testEmail,
                Role = UserRole.Customer,
                IsActive = true,
                CreatedAt = clock.GetCurrentInstant()
            };
            await userMgr.CreateAsync(u, DemoPassword);
            await userMgr.AddToRoleAsync(u, "Customer");
            testCustId = u.Id;
        }

        // 1. Verify user can log in when active
        var activeLogin = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginModel
        {
            Email = testEmail,
            Password = DemoPassword
        });
        activeLogin.StatusCode.Should().Be(HttpStatusCode.OK);

        // 2. Admin deactivates the customer
        var deactResp = await adminClient.PostAsync($"/api/admin/customers/{testCustId}/status", null);
        deactResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. Deactivated customer cannot log in (BR-13)
        var blockedLogin = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginModel
        {
            Email = testEmail,
            Password = DemoPassword
        });
        blockedLogin.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var errBody = await blockedLogin.Content.ReadAsStringAsync();
        errBody.Should().Contain("ACCOUNT_INACTIVE");

        // 4. Admin reactivates the customer
        var reactResp = await adminClient.PostAsync($"/api/admin/customers/{testCustId}/status", null);
        reactResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // 5. Customer can log in again
        var reLogin = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginModel
        {
            Email = testEmail,
            Password = DemoPassword
        });
        reLogin.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AF5_AS21_AdminCanResetCustomerPassword_WithoutEmailTokens()
    {
        await EnsureSeededAsync();
        var adminClient = await CreateAuthenticatedClientAsync(AdminEmail, AdminPassword);

        // Create a customer account
        int targetCustId;
        string targetEmail = $"pwd_cust_{Guid.NewGuid():N}@barberbook.local";
        using (var scope = _factory.Services.CreateScope())
        {
            var userMgr = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();

            var u = new ApplicationUser
            {
                FullName = "Password Reset Client",
                Email = targetEmail,
                UserName = targetEmail,
                Role = UserRole.Customer,
                IsActive = true,
                CreatedAt = clock.GetCurrentInstant()
            };
            await userMgr.CreateAsync(u, DemoPassword);
            await userMgr.AddToRoleAsync(u, "Customer");
            targetCustId = u.Id;
        }

        // Admin resets password directly (AS-21)
        string brandNewPassword = "BrandNewCustomerPass999!";
        var resetResp = await adminClient.PostAsJsonAsync($"/api/admin/customers/{targetCustId}/reset-password", new AdminCustomerResetPasswordModel
        {
            NewPassword = brandNewPassword,
            ConfirmPassword = brandNewPassword
        });
        resetResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Old password fails
        var oldLogin = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginModel
        {
            Email = targetEmail,
            Password = DemoPassword
        });
        oldLogin.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // New password succeeds
        var newLogin = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginModel
        {
            Email = targetEmail,
            Password = brandNewPassword
        });
        newLogin.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task T20_BR02_CustomerAndBarber_CannotAccessAdminCustomerEndpoints()
    {
        await EnsureSeededAsync();
        var customerClient = await CreateAuthenticatedClientAsync("juan@barberbook.local", DemoPassword);
        var barberClient = await CreateAuthenticatedClientAsync("ramon@barberbook.local", DemoPassword);

        // Customer attempts
        var cList = await customerClient.GetAsync("/api/admin/customers");
        cList.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var cDetail = await customerClient.GetAsync("/api/admin/customers/5");
        cDetail.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Barber attempts
        var bList = await barberClient.GetAsync("/api/admin/customers");
        bList.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var bDetail = await barberClient.GetAsync("/api/admin/customers/5");
        bDetail.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

