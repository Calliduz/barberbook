using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BarberBook.Data;
using BarberBook.Data.Entities;
using BarberBook.Data.Enums;
using BarberBook.Data.Seeding;
using BarberBook.Web.Common;
using BarberBook.Web.Models.Admin;
using BarberBook.Web.Models.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BarberBook.Tests;

public class AdminBarberAndServiceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AdminBarberAndServiceTests(WebApplicationFactory<Program> factory)
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
    public async Task AF2_BF1_LM4_T07_AdminCreatesBarber_AndBarberCanLogin_AndAccessBarberDashboard()
    {
        // Requirement: Admin creates a barber account (role: barber, profile created).
        // The barber logs in with temporary credentials and accesses /barber, but cannot access /admin.
        await EnsureSeededAsync();
        var adminClient = await CreateAuthenticatedClientAsync("admin@barberbook.local", "Admin@BarberBook2026!");

        var uniqueEmail = $"barber_antonio_{Guid.NewGuid():N}@barberbook.local";
        var createRequest = new CreateBarberRequest
        {
            FullName = "Antonio Luna",
            DisplayName = "Antonio",
            Email = uniqueEmail,
            Password = "TempPassword123!",
            Specialties = "Scissor Cut, Beard Shaping",
            Bio = "Master barber with 10 years experience."
        };

        // 1. Admin creates barber
        var createResponse = await adminClient.PostAsJsonAsync("/api/admin/barbers", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdBarber = await createResponse.Content.ReadFromJsonAsync<AdminBarberItemDto>();
        createdBarber.Should().NotBeNull();
        createdBarber!.DisplayName.Should().Be("Antonio");
        createdBarber.Email.Should().Be(uniqueEmail);
        createdBarber.IsActive.Should().BeTrue();

        // Verify in database
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
            var barberInDb = await db.Barbers.Include(b => b.User).FirstOrDefaultAsync(b => b.User.Email == uniqueEmail);
            barberInDb.Should().NotBeNull();
            barberInDb!.User.Role.Should().Be(UserRole.Barber);
            barberInDb.User.IsActive.Should().BeTrue();
            barberInDb.Specialties.Should().Be("Scissor Cut, Beard Shaping");
        }

        // 2. The new barber logs in with temporary credentials
        var barberClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });

        var loginResponse = await barberClient.PostAsJsonAsync("/api/auth/login", new LoginModel
        {
            Email = uniqueEmail,
            Password = "TempPassword123!"
        });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. Barber accesses /barber (allowed)
        var barberDashboard = await barberClient.GetAsync("/barber");
        barberDashboard.StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. Barber accesses /admin (forbidden)
        var adminForbidden = await barberClient.GetAsync("/admin");
        if (adminForbidden.StatusCode == HttpStatusCode.Redirect)
        {
            adminForbidden.Headers.Location!.ToString().Should().Contain("AccessDenied");
        }
        else
        {
            adminForbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }

    [Fact]
    public async Task AF2_AF3_BR02_CustomerAndBarber_CannotAccessAdminBarbersOrServices_Returns403()
    {
        // Rule BR-02: Role guards prevent unauthorized access across portals
        await EnsureSeededAsync();

        var customerClient = await CreateAuthenticatedClientAsync("juan@barberbook.local", "Demo@BarberBook2026!");
        var barberClient = await CreateAuthenticatedClientAsync("ramon@barberbook.local", "Demo@BarberBook2026!");

        // Customer calling admin barber/service endpoints
        var customerBarbers = await customerClient.GetAsync("/api/admin/barbers");
        customerBarbers.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var customerServices = await customerClient.GetAsync("/api/admin/services");
        customerServices.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var customerPostBarber = await customerClient.PostAsJsonAsync("/api/admin/barbers", new CreateBarberRequest());
        customerPostBarber.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Barber calling admin barber/service endpoints
        var barberBarbers = await barberClient.GetAsync("/api/admin/barbers");
        barberBarbers.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var barberServices = await barberClient.GetAsync("/api/admin/services");
        barberServices.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var barberPostService = await barberClient.PostAsJsonAsync("/api/admin/services", new CreateServiceRequest());
        barberPostService.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AF2_BR13_Admin_DeactivatesBarber_BarberCannotLogin_AndDisappearsFromPublicList()
    {
        // Requirement: A deactivated barber cannot log in and is hidden from public booking
        await EnsureSeededAsync();
        var adminClient = await CreateAuthenticatedClientAsync("admin@barberbook.local", "Admin@BarberBook2026!");

        // 1. Create a test barber to deactivate
        var email = $"deactivate_test_{Guid.NewGuid():N}@barberbook.local";
        var createResponse = await adminClient.PostAsJsonAsync("/api/admin/barbers", new CreateBarberRequest
        {
            FullName = "Deactivate Target",
            DisplayName = "Target",
            Email = email,
            Password = "Password123!",
            Specialties = "Styling"
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<AdminBarberItemDto>();

        // 2. Verify barber appears in public list
        var anonClient = _factory.CreateClient();
        var publicListBefore = await anonClient.GetFromJsonAsync<List<JsonElement>>("/api/barbers");
        publicListBefore!.Any(b => b.GetProperty("id").GetInt32() == created!.Id).Should().BeTrue();

        // 3. Admin deactivates the barber
        var toggleResponse = await adminClient.PostAsync($"/api/admin/barbers/{created!.Id}/toggle-status", null);
        toggleResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. Barber attempts to log in -> rejected (account inactive)
        var barberClient = _factory.CreateClient();
        var loginResponse = await barberClient.PostAsJsonAsync("/api/auth/login", new LoginModel
        {
            Email = email,
            Password = "Password123!"
        });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 5. Public list no longer includes deactivated barber
        var publicListAfter = await anonClient.GetFromJsonAsync<List<JsonElement>>("/api/barbers");
        publicListAfter!.Any(b => b.GetProperty("id").GetInt32() == created.Id).Should().BeFalse();

        // 6. Admin reactivates the barber
        var reactivateResponse = await adminClient.PostAsync($"/api/admin/barbers/{created.Id}/toggle-status", null);
        reactivateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 7. Barber can log in again
        var loginAfterReactivate = await barberClient.PostAsJsonAsync("/api/auth/login", new LoginModel
        {
            Email = email,
            Password = "Password123!"
        });
        loginAfterReactivate.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AF2_AS21_Admin_CanResetBarberPassword_AndBarberLogsInWithNewPassword()
    {
        // Requirement: Admin can reset a barber's password without email recovery (AS-21)
        await EnsureSeededAsync();
        var adminClient = await CreateAuthenticatedClientAsync("admin@barberbook.local", "Admin@BarberBook2026!");

        var email = $"pwd_reset_{Guid.NewGuid():N}@barberbook.local";
        var createResponse = await adminClient.PostAsJsonAsync("/api/admin/barbers", new CreateBarberRequest
        {
            FullName = "Password Reset Barber",
            DisplayName = "ResetGuy",
            Email = email,
            Password = "InitialPassword123!"
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<AdminBarberItemDto>();

        // Admin resets password
        var resetResponse = await adminClient.PostAsJsonAsync($"/api/admin/barbers/{created!.Id}/reset-password", new ResetBarberPasswordRequest
        {
            NewPassword = "BrandNewSecretPassword456!"
        });
        resetResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Login with old password fails (invalid credentials)
        var client = _factory.CreateClient();
        var oldLogin = await client.PostAsJsonAsync("/api/auth/login", new LoginModel
        {
            Email = email,
            Password = "InitialPassword123!"
        });
        oldLogin.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Login with new password succeeds
        var newLogin = await client.PostAsJsonAsync("/api/auth/login", new LoginModel
        {
            Email = email,
            Password = "BrandNewSecretPassword456!"
        });
        newLogin.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AF2_Rule6_CreateBarber_RejectsDuplicateEmail_AndInvalidFields()
    {
        // Rule 6: Validate every request and return { error: { code, message, details? } }
        await EnsureSeededAsync();
        var adminClient = await CreateAuthenticatedClientAsync("admin@barberbook.local", "Admin@BarberBook2026!");

        // Duplicate email
        var duplicateResponse = await adminClient.PostAsJsonAsync("/api/admin/barbers", new CreateBarberRequest
        {
            FullName = "Duplicate Ramon",
            DisplayName = "Ramon 2",
            Email = "ramon@barberbook.local", // already exists
            Password = "Password123!"
        });
        duplicateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var dupError = await duplicateResponse.Content.ReadFromJsonAsync<ApiErrorResponse>();
        dupError!.Error.Code.Should().Be("DUPLICATE_EMAIL");

        // Invalid validation (missing required fields)
        var invalidResponse = await adminClient.PostAsJsonAsync("/api/admin/barbers", new CreateBarberRequest
        {
            FullName = "",
            Email = "not-an-email",
            Password = "123" // too short
        });
        invalidResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var valError = await invalidResponse.Content.ReadFromJsonAsync<ApiErrorResponse>();
        valError!.Error.Code.Should().Be("VALIDATION_ERROR");
        valError.Error.Details.Should().NotBeNull();
    }

    [Fact]
    public async Task AF3_BR05_CreateService_EnforcesMultiplesOf30_AndNonNegativePrice()
    {
        // Rule BR-05: Service durations must be positive multiples of 30 minutes
        await EnsureSeededAsync();
        var adminClient = await CreateAuthenticatedClientAsync("admin@barberbook.local", "Admin@BarberBook2026!");

        // Reject non-multiple of 30 (e.g. 45 mins)
        var invalidDurationResponse = await adminClient.PostAsJsonAsync("/api/admin/services", new CreateServiceRequest
        {
            Name = "Express Trim",
            Price = 120.00m,
            DurationMinutes = 45 // Not multiple of 30!
        });
        invalidDurationResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var durationError = await invalidDurationResponse.Content.ReadFromJsonAsync<ApiErrorResponse>();
        durationError!.Error.Code.Should().Be("VALIDATION_ERROR");

        // Reject negative price
        var negativePriceResponse = await adminClient.PostAsJsonAsync("/api/admin/services", new CreateServiceRequest
        {
            Name = "Negative Service",
            Price = -50.00m,
            DurationMinutes = 30
        });
        negativePriceResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Accept valid service with multiple of 30
        var validResponse = await adminClient.PostAsJsonAsync("/api/admin/services", new CreateServiceRequest
        {
            Name = "Deluxe Spa Cut",
            Description = "Premium haircut with scalp massage",
            Price = 350.00m,
            DurationMinutes = 60
        });
        validResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await validResponse.Content.ReadFromJsonAsync<AdminServiceItemDto>();
        created!.Name.Should().Be("Deluxe Spa Cut");
        created.Price.Should().Be(350.00m);
        created.DurationMinutes.Should().Be(60);
    }

    [Fact]
    public async Task AF3_BR13_Admin_DeactivatesService_DisappearsFromPublicList_ButResolvesInHistoricalAppointments()
    {
        // Rule BR-13: Deactivated services disappear from public booking but resolve in history
        await EnsureSeededAsync();
        var adminClient = await CreateAuthenticatedClientAsync("admin@barberbook.local", "Admin@BarberBook2026!");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
            // Haircut service ID is 1
            var haircut = await db.Services.FindAsync(1);
            haircut.Should().NotBeNull();
            if (!haircut!.IsActive)
            {
                haircut.IsActive = true;
                await db.SaveChangesAsync();
            }
        }

        try
        {
            // 1. Deactivate Haircut service
            var toggleResponse = await adminClient.PostAsync("/api/admin/services/1/toggle-status", null);
            toggleResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // 2. Verify Haircut is gone from public GET /api/services
            var anonClient = _factory.CreateClient();
            var publicServices = await anonClient.GetFromJsonAsync<List<JsonElement>>("/api/services");
            publicServices!.Any(s => s.GetProperty("id").GetInt32() == 1).Should().BeFalse();

            // 3. Historical completed appointment from seed still resolves Service 1
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
                var appointment = await db.Appointments
                    .Include(a => a.Service)
                    .FirstOrDefaultAsync(a => a.ServiceId == 1 && a.Status == AppointmentStatus.Completed);

                appointment.Should().NotBeNull();
                appointment!.Service.Should().NotBeNull();
                appointment.Service.Id.Should().Be(1);
                appointment.Price.Should().Be(150.00m);
                appointment.ServiceName.Should().Be("Haircut");
            }

            // 4. Reactivate Haircut service
            var reactivateResponse = await adminClient.PostAsync("/api/admin/services/1/toggle-status", null);
            reactivateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // 5. Reappears in public list
            var publicServicesAfter = await anonClient.GetFromJsonAsync<List<JsonElement>>("/api/services");
            publicServicesAfter!.Any(s => s.GetProperty("id").GetInt32() == 1).Should().BeTrue();
        }
        finally
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
            var haircut = await db.Services.FindAsync(1);
            if (haircut != null && !haircut.IsActive)
            {
                haircut.IsActive = true;
                await db.SaveChangesAsync();
            }
        }
    }

    [Fact]
    public async Task AF3_BR14_BR08_UpdateServicePrice_DoesNotAlterHistoricalAppointments()
    {
        // Rule BR-14: Price changes never alter existing appointment snapshots
        await EnsureSeededAsync();
        var adminClient = await CreateAuthenticatedClientAsync("admin@barberbook.local", "Admin@BarberBook2026!");

        // Service 1 is Haircut, currently ₱150.00
        // Historical appointments have price = 150.00
        var updateResponse = await adminClient.PutAsJsonAsync("/api/admin/services/1", new UpdateServiceRequest
        {
            Name = "Haircut (Updated)",
            Description = "Updated description",
            Price = 180.00m, // Price increased by 30 pesos
            DurationMinutes = 30
        });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();

        // Service entity price is updated
        var updatedService = await db.Services.FindAsync(1);
        updatedService!.Price.Should().Be(180.00m);

        // Existing historical appointments must retain the original snapshot price of 150.00!
        var historicalAppointment = await db.Appointments
            .FirstOrDefaultAsync(a => a.ServiceId == 1 && a.ReferenceCode == "BB-20260914-0001");

        historicalAppointment.Should().NotBeNull();
        historicalAppointment!.Price.Should().Be(150.00m, "Snapshot price on appointment must not be modified by service price changes");
        historicalAppointment.ServiceName.Should().Be("Haircut");

        // Restore original service properties
        updatedService.Name = "Haircut";
        updatedService.Price = 150.00m;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task AF2_AF3_PaginationAndSearch_WorksOnBarbersAndServices()
    {
        // Search and pagination testing
        await EnsureSeededAsync();
        var adminClient = await CreateAuthenticatedClientAsync("admin@barberbook.local", "Admin@BarberBook2026!");

        // Barbers search
        var barberSearch = await adminClient.GetFromJsonAsync<BarberListViewModel>("/api/admin/barbers?q=Ramon");
        barberSearch.Should().NotBeNull();
        barberSearch!.Items.Should().ContainSingle(b => b.DisplayName == "Ramon");

        // Barbers pagination
        var barberPage = await adminClient.GetFromJsonAsync<BarberListViewModel>("/api/admin/barbers?pageSize=2&page=1");
        barberPage.Should().NotBeNull();
        barberPage!.Items.Count.Should().Be(2);
        barberPage.TotalCount.Should().BeGreaterThanOrEqualTo(3);
        barberPage.TotalPages.Should().BeGreaterThanOrEqualTo(2);

        // Services search
        var serviceSearch = await adminClient.GetFromJsonAsync<ServiceListViewModel>("/api/admin/services?q=Beard");
        serviceSearch.Should().NotBeNull();
        serviceSearch!.Items.Should().ContainSingle(s => s.Name == "Beard Trim");

        // Services pagination
        var servicePage = await adminClient.GetFromJsonAsync<ServiceListViewModel>("/api/admin/services?pageSize=2&page=1");
        servicePage.Should().NotBeNull();
        servicePage!.Items.Count.Should().Be(2);
        servicePage.TotalCount.Should().BeGreaterThanOrEqualTo(4);
    }
}
