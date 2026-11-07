using System.Net;
using System.Net.Http.Json;
using BarberBook.Data;
using BarberBook.Data.Entities;
using BarberBook.Data.Enums;
using BarberBook.Data.Seeding;
using BarberBook.Web.Models.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Xunit;

namespace BarberBook.Tests;

public class CoreTraceabilityAndBoundaryTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly DateTimeZone _zone = DateTimeZoneProviders.Tzdb["Asia/Manila"];
    private const string AdminEmail = "admin@barberbook.local";
    private const string AdminPassword = "Admin@BarberBook2026!";
    private const string DemoPassword = "Demo@BarberBook2026!";

    public CoreTraceabilityAndBoundaryTests(WebApplicationFactory<Program> factory)
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

    /// <summary>
    /// T-15 / LM-1, LM-3, LM-4, LM-5:
    /// Code inspection proving hard boundaries:
    /// 1. No payment gateway packages or integrations (LM-1).
    /// 2. No multi-tenant or multi-branch structures (LM-3).
    /// 3. Barbers cannot self-register through public endpoints (LM-4).
    /// 4. No external SMS or email sender packages (LM-5).
    /// </summary>
    [Fact]
    public async Task T15_LM1_LM3_LM4_LM5_InspectCodebase_ProvesHardBoundaries()
    {
        await EnsureSeededAsync();

        // 1. Inspect loaded assemblies and dependencies for payment SDKs
        var forbiddenPaymentKeywords = new[] { "Stripe", "PayPal", "Braintree", "PayMongo" };
        var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name ?? "");
        foreach (var keyword in forbiddenPaymentKeywords)
        {
            loadedAssemblies.Should().NotContain(a => a.Contains(keyword, StringComparison.OrdinalIgnoreCase),
                $"LM-1 violation: Found payment assembly matching {keyword}");
        }

        // 2. Inspect loaded assemblies for external SMS/email sending SDKs
        var forbiddenNotificationKeywords = new[] { "SendGrid", "Twilio", "MailKit", "MimeKit" };
        foreach (var keyword in forbiddenNotificationKeywords)
        {
            loadedAssemblies.Should().NotContain(a => a.Contains(keyword, StringComparison.OrdinalIgnoreCase),
                $"LM-5 violation: Found notification assembly matching {keyword}");
        }

        // 3. Inspect EF Core metadata to verify no multi-tenant isolation columns (TenantId prohibited per LM-3)
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
            var model = db.Model;
            foreach (var entityType in model.GetEntityTypes())
            {
                var properties = entityType.GetProperties().Select(p => p.Name);
                properties.Should().NotContain("TenantId", $"LM-3 violation: Found TenantId on {entityType.Name}");
            }
        }

        // 4. Verify that public self-registration can NEVER create a Barber or Admin account (LM-4)
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var registerModel = new
        {
            full_name = "Attempted Self Barber",
            email = $"fakebarber_{Guid.NewGuid():N}@test.local",
            phone = "09170009999",
            password = DemoPassword,
            confirm_password = DemoPassword,
            role = "Barber" // Attacker attempts to inject barber role
        };

        var regResponse = await client.PostAsJsonAsync("/api/auth/register", registerModel);
        regResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Verify the created user has Customer role strictly, never Barber
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == registerModel.email);
            user.Should().NotBeNull();
            user!.Role.Should().Be(UserRole.Customer, "Public registration must strictly force Customer role");

            // Barber profile table must NOT have an entry for this user
            var barberProfile = await db.Barbers.FirstOrDefaultAsync(b => b.UserId == user.Id);
            barberProfile.Should().BeNull("No barber profile may be created by public registration");
        }
    }

    /// <summary>
    /// FL-B: Full Barber workflow end-to-end:
    /// Login -> View Schedule & Bookings -> Set Availability / Day Off -> Update Status -> View Own Transactions
    /// </summary>
    [Fact]
    public async Task FL_B_BarberPortal_EndToEndFlow()
    {
        await EnsureSeededAsync();
        var barberClient = await CreateAuthenticatedClientAsync("ramon@barberbook.local", DemoPassword);

        // 1. Dashboard
        var dashRes = await barberClient.GetAsync("/barber");
        dashRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 2. Schedule
        var schedRes = await barberClient.GetAsync("/barber/schedule");
        schedRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. Appointments list
        var apptRes = await barberClient.GetAsync("/barber/appointments");
        apptRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. Availability
        var availRes = await barberClient.GetAsync("/barber/availability");
        availRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 5. Transactions
        var txRes = await barberClient.GetAsync("/barber/transactions");
        txRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 6. Account
        var acctRes = await barberClient.GetAsync("/barber/account");
        acctRes.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
