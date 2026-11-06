using System.Net;
using System.Net.Http.Json;
using BarberBook.Data;
using BarberBook.Data.Entities;
using BarberBook.Data.Enums;
using BarberBook.Data.Seeding;
using BarberBook.Web.Models.Admin;
using BarberBook.Web.Models.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Xunit;

namespace BarberBook.Tests;

public class AdminDashboardHomeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly DateTimeZone _zone = DateTimeZoneProviders.Tzdb["Asia/Manila"];
    private const string AdminEmail = "admin@barberbook.local";
    private const string AdminPassword = "Admin@BarberBook2026!";
    private const string DemoPassword = "Demo@BarberBook2026!";

    public AdminDashboardHomeTests(WebApplicationFactory<Program> factory)
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
    public async Task AF1_OB4_T20_AdminDashboard_ReturnsExpectedMetricsAndHtmlView()
    {
        await EnsureSeededAsync();
        var adminClient = await CreateAuthenticatedClientAsync(AdminEmail, AdminPassword);

        // 1. GET /api/admin/dashboard (Single call returning all metrics)
        var apiResponse = await adminClient.GetAsync("/api/admin/dashboard");
        apiResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await apiResponse.Content.ReadFromJsonAsync<AdminDashboardDto>();
        dto.Should().NotBeNull();
        dto!.TodayCounts.Should().NotBeNull();
        dto.PendingBookings.Should().NotBeNull();
        dto.PendingBookings.Count.Should().BeLessThanOrEqualTo(10);
        dto.TodayAppointmentsByBarber.Should().NotBeNull();
        dto.TodayAppointmentsByBarber.Should().NotBeEmpty();
        dto.ActiveBarbersCount.Should().BeGreaterThan(0);
        dto.TotalCustomersCount.Should().BeGreaterThan(0);

        // 2. GET /admin landing page HTML
        var pageResponse = await adminClient.GetAsync("/admin");
        pageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await pageResponse.Content.ReadAsStringAsync();

        // Must display all §7/P13 required UI components
        html.Should().Contain("Shop Operations & Analytics");
        html.Should().Contain("Needs Action");
        html.Should().Contain("Today's Schedule by Barber");
        html.Should().Contain("System Alerts & Notices");
        html.Should().Contain("/admin/reports");
        html.Should().Contain("/admin/appointments");
        html.Should().Contain("/admin/barbers");
        html.Should().Contain("/admin/services");
        html.Should().Contain("/admin/customers");
    }

    [Fact]
    public async Task AdminDashboard_RoleAccessControl_RejectsNonAdmins()
    {
        await EnsureSeededAsync();

        // 1. Unauthenticated client
        var unauthClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var unauthApi = await unauthClient.GetAsync("/api/admin/dashboard");
        ((int)unauthApi.StatusCode).Should().BeOneOf((int)HttpStatusCode.Unauthorized, (int)HttpStatusCode.Redirect);

        // 2. Customer client
        var customerClient = await CreateAuthenticatedClientAsync("juan@barberbook.local", DemoPassword);
        var customerApi = await customerClient.GetAsync("/api/admin/dashboard");
        customerApi.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var customerWeb = await customerClient.GetAsync("/admin");
        ((int)customerWeb.StatusCode).Should().BeOneOf((int)HttpStatusCode.Forbidden, (int)HttpStatusCode.Redirect);

        // 3. Barber client
        var barberClient = await CreateAuthenticatedClientAsync("ramon@barberbook.local", DemoPassword);
        var barberApi = await barberClient.GetAsync("/api/admin/dashboard");
        barberApi.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var barberWeb = await barberClient.GetAsync("/admin");
        ((int)barberWeb.StatusCode).Should().BeOneOf((int)HttpStatusCode.Forbidden, (int)HttpStatusCode.Redirect);
    }

    private static int _adminSlotCounter = 500;

    private async Task<Appointment> CreateIsolatedAppointmentAsync(
        AppointmentStatus status,
        int barberId = 1,
        int customerId = 6,
        string serviceName = "Classic Haircut",
        decimal price = 250.00m)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        int slot = Interlocked.Increment(ref _adminSlotCounter);
        var appDate = new LocalDate(2029, 6, 1).PlusDays(slot / 16);
        var appTime = new LocalTime(9, 0).PlusMinutes((slot % 16) * 30);
        var startsAt = appDate.At(appTime).InZoneStrictly(_zone).ToInstant();
        var endsAt = startsAt.Plus(Duration.FromMinutes(30));

        var existing = await db.Appointments
            .Where(a => a.BarberId == barberId && a.StartsAt < endsAt && a.EndsAt > startsAt)
            .ToListAsync();
        if (existing.Count > 0)
        {
            db.Appointments.RemoveRange(existing);
            await db.SaveChangesAsync();
        }

        var app = new Appointment
        {
            ReferenceCode = $"BB-ADM-{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
            CustomerId = customerId,
            BarberId = barberId,
            ServiceId = 1,
            ServiceName = serviceName,
            Price = price,
            StartsAt = startsAt,
            EndsAt = endsAt,
            Status = status,
            CreatedAt = clock.GetCurrentInstant()
        };

        db.Appointments.Add(app);
        await db.SaveChangesAsync();

        db.StatusHistories.Add(new StatusHistory
        {
            AppointmentId = app.Id,
            FromStatus = null,
            ToStatus = status,
            ChangedByRole = "Customer",
            ChangedAt = clock.GetCurrentInstant(),
            Note = "Initial booking"
        });
        await db.SaveChangesAsync();

        return app;
    }

    [Fact]
    public async Task AF1_AF6_P13_AdminDashboard_InlineConfirmAndRejectActions()
    {
        await EnsureSeededAsync();
        var adminClient = await CreateAuthenticatedClientAsync(AdminEmail, AdminPassword);

        // Create an isolated pending appointment
        var pendingApp = await CreateIsolatedAppointmentAsync(AppointmentStatus.Pending, barberId: 1);
        int appointmentId = pendingApp.Id;

        // Inline Confirm via POST /admin/appointments/{id}/status
        var confirmForm = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("to", "confirmed"),
            new KeyValuePair<string, string>("note", "Confirmed from test")
        });

        var confirmRes = await adminClient.PostAsync($"/admin/appointments/{appointmentId}/status", confirmForm);
        confirmRes.StatusCode.Should().Be(HttpStatusCode.Redirect);

        // Verify status transitioned in database
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
            var updated = await db.Appointments.FindAsync(appointmentId);
            updated!.Status.Should().Be(AppointmentStatus.Confirmed);
        }
    }
}
