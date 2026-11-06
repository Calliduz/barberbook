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

public class BarberPortalTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly DateTimeZone _zone = DateTimeZoneProviders.Tzdb["Asia/Manila"];
    private const string DemoPassword = "Demo@BarberBook2026!";

    public BarberPortalTests(WebApplicationFactory<Program> factory)
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
    public async Task LogoutEndpoint_SupportsGetAndPost_NeverReturns405()
    {
        await EnsureSeededAsync();
        var barberClient = await CreateAuthenticatedClientAsync("ramon@barberbook.local", DemoPassword);

        // 1. GET /api/auth/logout as API request (application/json)
        var getReq = new HttpRequestMessage(HttpMethod.Get, "/api/auth/logout");
        getReq.Headers.Accept.ParseAdd("application/json");
        var getResponse = await barberClient.SendAsync(getReq);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 2. GET /api/auth/logout as Browser request (text/html)
        var browserReq = new HttpRequestMessage(HttpMethod.Get, "/api/auth/logout");
        browserReq.Headers.Accept.ParseAdd("text/html");
        var browserResponse = await barberClient.SendAsync(browserReq);
        browserResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        // 3. GET /logout standard web route
        var webLogoutRes = await barberClient.GetAsync("/logout");
        webLogoutRes.StatusCode.Should().Be(HttpStatusCode.Redirect);

        // 4. POST /api/auth/logout as API request
        var postReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        postReq.Headers.Accept.ParseAdd("application/json");
        var postResponse = await barberClient.SendAsync(postReq);
        postResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task BF2_EO3_T21_BarberSchedule_Returns200_DayAndWeekViews()
    {
        await EnsureSeededAsync();
        var barberClient = await CreateAuthenticatedClientAsync("ramon@barberbook.local", DemoPassword);

        // Day view
        var dayResponse = await barberClient.GetAsync("/barber/schedule?view=day");
        dayResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var dayHtml = await dayResponse.Content.ReadAsStringAsync();
        dayHtml.Should().Contain("My Work Schedule");
        dayHtml.Should().Contain("Ramon");

        // Week view
        var weekResponse = await barberClient.GetAsync("/barber/schedule?view=week");
        weekResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var weekHtml = await weekResponse.Content.ReadAsStringAsync();
        weekHtml.Should().Contain("Week View");
    }

    private static int _barberSlotCounter = 100;

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

        int slot = Interlocked.Increment(ref _barberSlotCounter);
        var appDate = new LocalDate(2028, 1, 1).PlusDays(slot / 16);
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
            ReferenceCode = $"BB-TST-{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
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
    public async Task BF2_BF4_OB5_SC7_BarberAppointments_Returns200_AndTransitionsOwnAppointment()
    {
        await EnsureSeededAsync();
        var barberClient = await CreateAuthenticatedClientAsync("ramon@barberbook.local", DemoPassword);

        // 1. View appointments
        var listResponse = await barberClient.GetAsync("/barber/appointments");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var listHtml = await listResponse.Content.ReadAsStringAsync();
        listHtml.Should().Contain("Barber Appointments");

        // 2. Create isolated pending appointment for Ramon (BarberId = 1)
        var app = await CreateIsolatedAppointmentAsync(AppointmentStatus.Pending, barberId: 1);

        // 3. Ramon confirms own appointment (pending -> confirmed)
        var confirmForm = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("to", "confirmed")
        });
        var confirmResponse = await barberClient.PostAsync($"/barber/appointments/{app.Id}/status", confirmForm);
        confirmResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        // Verify status in DB
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
            var updated = await db.Appointments.FindAsync(app.Id);
            updated!.Status.Should().Be(AppointmentStatus.Confirmed);
        }
    }

    [Fact]
    public async Task T06_BarberCannotUpdateAnotherBarbersAppointment()
    {
        await EnsureSeededAsync();
        // Login as Dennis (BarberId = 2)
        var dennisClient = await CreateAuthenticatedClientAsync("dennis@barberbook.local", DemoPassword);

        // Create pending appointment for Ramon (BarberId = 1)
        var ramonApp = await CreateIsolatedAppointmentAsync(AppointmentStatus.Pending, barberId: 1);

        // Dennis tries to confirm Ramon's appointment
        var confirmForm = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("to", "confirmed")
        });
        var response = await dennisClient.PostAsync($"/barber/appointments/{ramonApp.Id}/status", confirmForm);
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        // Status should still be Pending (rejected by guard)
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
            var notUpdated = await db.Appointments.FindAsync(ramonApp.Id);
            notUpdated!.Status.Should().Be(AppointmentStatus.Pending);
        }
    }

    [Fact]
    public async Task BF3_T08_BarberAvailability_Returns200_AndManagesDaysOff()
    {
        await EnsureSeededAsync();
        var barberClient = await CreateAuthenticatedClientAsync("ramon@barberbook.local", DemoPassword);

        // 1. GET availability view
        var getResponse = await barberClient.GetAsync("/barber/availability");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await getResponse.Content.ReadAsStringAsync();
        html.Should().Contain("Availability & Days Off");

        // 2. Add day off
        var targetDate = DateTime.UtcNow.AddDays(20).ToString("yyyy-MM-dd");
        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("date", targetDate),
            new KeyValuePair<string, string>("reason", "Personal Workshop")
        });

        var postResponse = await barberClient.PostAsync("/barber/availability/days-off", form);
        postResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        // 3. Verify in DB
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
            var dayOff = await db.DaysOff.FirstOrDefaultAsync(d => d.BarberId == 1 && d.Reason == "Personal Workshop");
            dayOff.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task BF5_RP4_T13_BarberTransactions_ScopedStrictlyToOwnCompletedAppointments()
    {
        await EnsureSeededAsync();
        var ramonClient = await CreateAuthenticatedClientAsync("ramon@barberbook.local", DemoPassword);

        // Create completed appointment for Ramon (BarberId = 1)
        // and another completed appointment for Dennis (BarberId = 2)
        var ramonApp = await CreateIsolatedAppointmentAsync(
            AppointmentStatus.Completed,
            barberId: 1,
            serviceName: "Ramon Special Cut",
            price: 350.00m);

        var dennisApp = await CreateIsolatedAppointmentAsync(
            AppointmentStatus.Completed,
            barberId: 2,
            serviceName: "Dennis Special Cut",
            price: 400.00m);

        // Ramon views transactions
        var response = await ramonClient.GetAsync("/barber/transactions");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();

        // Ramon's row must appear
        content.Should().Contain(ramonApp.ReferenceCode);
        content.Should().Contain("Ramon Special Cut");

        // Dennis's row must NEVER appear in Ramon's transactions (T-13)
        content.Should().NotContain(dennisApp.ReferenceCode);
        content.Should().NotContain("Dennis Special Cut");
    }

    [Fact]
    public async Task BF6_HW1_HW3_BarberEquipment_Returns200_AndLogsInspection()
    {
        await EnsureSeededAsync();
        var ramonClient = await CreateAuthenticatedClientAsync("ramon@barberbook.local", DemoPassword);

        // 1. GET equipment view
        var getResponse = await ramonClient.GetAsync("/barber/equipment");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await getResponse.Content.ReadAsStringAsync();
        html.Should().Contain("My Assigned Equipment");
        html.Should().Contain("Wahl Magic Clip Cordless"); // Assigned to Ramon

        // 2. Post inspection log for Ramon's tool (Id = 1)
        var inspectForm = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("condition", "Good"),
            new KeyValuePair<string, string>("notes", "Oiled, cleaned, zero-gapped for shift")
        });

        var postResponse = await ramonClient.PostAsync("/barber/equipment/1/inspect", inspectForm);
        postResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        // Re-read equipment view
        var updatedResponse = await ramonClient.GetAsync("/barber/equipment");
        var updatedHtml = await updatedResponse.Content.ReadAsStringAsync();
        updatedHtml.Should().Contain("Oiled, cleaned, zero-gapped for shift");
    }

    [Fact]
    public async Task CustomerCannotAccessBarberPortal()
    {
        await EnsureSeededAsync();
        var customerClient = await CreateAuthenticatedClientAsync("juan@barberbook.local", DemoPassword);

        var endpoints = new[]
        {
            "/barber",
            "/barber/schedule",
            "/barber/appointments",
            "/barber/availability",
            "/barber/transactions",
            "/barber/equipment"
        };

        foreach (var endpoint in endpoints)
        {
            var res = await customerClient.GetAsync(endpoint);
            // Non-barber gets 403 Forbidden or redirect to AccessDenied
            ((int)res.StatusCode).Should().BeOneOf((int)HttpStatusCode.Forbidden, (int)HttpStatusCode.Redirect);
        }
    }
}

