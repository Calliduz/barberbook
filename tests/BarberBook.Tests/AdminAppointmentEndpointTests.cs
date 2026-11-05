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
using BarberBook.Web.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Xunit;

namespace BarberBook.Tests;

public class AdminAppointmentEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly DateTimeZone _zone = DateTimeZoneProviders.Tzdb["Asia/Manila"];
    private static int _adminSlotCounter = 0;

    public AdminAppointmentEndpointTests(WebApplicationFactory<Program> factory)
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

    private async Task<Appointment> CreateIsolatedAppointmentAsync(
        AppointmentStatus status,
        int barberId = 1,
        int customerId = 5,
        LocalDate? date = null,
        LocalTime? time = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var now = clock.GetCurrentInstant();

        int slot = Interlocked.Increment(ref _adminSlotCounter);
        var appDate = date ?? new LocalDate(2027, 4, 1).PlusDays(slot / 16);
        var appTime = time ?? new LocalTime(9, 0).PlusMinutes((slot % 16) * 30);
        var startsAt = appDate.At(appTime).InZoneStrictly(_zone).ToInstant();
        var endsAt = startsAt.Plus(Duration.FromMinutes(30));

        // Clean up any overlapping appointments from prior test runs
        var existing = await db.Appointments
            .Where(a => a.BarberId == barberId && a.StartsAt < endsAt && a.EndsAt > startsAt)
            .ToListAsync();
        if (existing.Count > 0)
        {
            db.Appointments.RemoveRange(existing);
            await db.SaveChangesAsync();
        }

        var uniqueRef = $"ADMIN-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

        var app = new Appointment
        {
            ReferenceCode = uniqueRef,
            CustomerId = customerId,
            BarberId = barberId,
            ServiceId = 1,
            ServiceName = "Haircut",
            Price = 250.00m,
            StartsAt = startsAt,
            EndsAt = endsAt,
            Status = status,
            Notes = "Admin endpoint test note",
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Appointments.Add(app);
        await db.SaveChangesAsync();

        db.StatusHistories.Add(new StatusHistory
        {
            AppointmentId = app.Id,
            FromStatus = null,
            ToStatus = status,
            ChangedById = customerId,
            ChangedByRole = "Customer",
            Note = "Initial booking",
            ChangedAt = now
        });
        await db.SaveChangesAsync();

        return app;
    }

    [Fact]
    public async Task T06_AF6_OB5_Admin_CanConfirm_And_Reject_PendingBookings()
    {
        // Proves T-06: Admin confirms and rejects bookings.
        // Status changes are logged to status_history.
        await EnsureSeededAsync();

        var adminClient = await CreateAuthenticatedClientAsync("admin@barberbook.local", "Admin@BarberBook2026!");

        // 1. Confirm Pending Booking
        var appToConfirm = await CreateIsolatedAppointmentAsync(AppointmentStatus.Pending);

        var confirmResponse = await adminClient.PostAsJsonAsync($"/api/admin/appointments/{appToConfirm.Id}/status", new UpdateAppointmentStatusRequest
        {
            To = "confirmed",
            Note = "Booking confirmed by admin operator"
        });

        confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var confirmDto = await confirmResponse.Content.ReadFromJsonAsync<AppointmentDetailDto>();
        confirmDto.Should().NotBeNull();
        confirmDto!.Status.Should().Be("confirmed");

        // 2. Reject Pending Booking
        var appToReject = await CreateIsolatedAppointmentAsync(AppointmentStatus.Pending);

        var rejectResponse = await adminClient.PostAsJsonAsync($"/api/admin/appointments/{appToReject.Id}/status", new UpdateAppointmentStatusRequest
        {
            To = "rejected",
            Note = "Barber unavailable due to emergency maintenance"
        });

        rejectResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var rejectDto = await rejectResponse.Content.ReadFromJsonAsync<AppointmentDetailDto>();
        rejectDto.Should().NotBeNull();
        rejectDto!.Status.Should().Be("rejected");

        // Verify status histories in DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();

        var confirmHistory = await db.StatusHistories.Where(h => h.AppointmentId == appToConfirm.Id).ToListAsync();
        confirmHistory.Should().Contain(h => h.ToStatus == AppointmentStatus.Confirmed && h.ChangedByRole == "Admin");

        var rejectHistory = await db.StatusHistories.Where(h => h.AppointmentId == appToReject.Id).ToListAsync();
        rejectHistory.Should().Contain(h => h.ToStatus == AppointmentStatus.Rejected && h.ChangedByRole == "Admin");
    }

    [Fact]
    public async Task T06_BF4_BarberUpdatesOwn_BarberCannotTouchAnotherBarbers()
    {
        // Proves T-06 (Barber half): A barber updates their own; a barber tries another barber's (returns 403).
        await EnsureSeededAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
        var transitionService = scope.ServiceProvider.GetRequiredService<IAppointmentTransitionService>();

        var ramon = await db.Barbers.Include(b => b.User).FirstAsync(b => b.DisplayName == "Ramon");
        var dennis = await db.Barbers.Include(b => b.User).FirstAsync(b => b.DisplayName == "Dennis");

        // Ramon's appointment
        var ramonApp = await CreateIsolatedAppointmentAsync(AppointmentStatus.Pending, barberId: ramon.Id);

        // Dennis tries to confirm Ramon's appointment -> 403 Forbidden
        var dennisActor = new TransitionActor(dennis.User.Id, "Barber", BarberId: dennis.Id);
        var dennisAttempt = await transitionService.TransitionAsync(ramonApp.Id, AppointmentStatus.Confirmed, dennisActor, "Dennis trying Ramon app");
        dennisAttempt.StatusCode.Should().Be(403);
        dennisAttempt.ErrorCode.Should().Be("FORBIDDEN");

        // Ramon confirms his own appointment -> 200 OK
        var ramonActor = new TransitionActor(ramon.User.Id, "Barber", BarberId: ramon.Id);
        var ramonAttempt = await transitionService.TransitionAsync(ramonApp.Id, AppointmentStatus.Confirmed, ramonActor, "Ramon confirmed own app");
        ramonAttempt.StatusCode.Should().Be(200);
        ramonAttempt.Success.Should().BeTrue();
    }

    [Fact]
    public async Task T09_AF7_SC8_AdminFiltersHistory_ByStatus_Date_Barber_Customer_WithTimeline()
    {
        // Proves T-09: Admin filters history by status, date, barber and customer; admin sees all, with status timeline.
        await EnsureSeededAsync();

        var adminClient = await CreateAuthenticatedClientAsync("admin@barberbook.local", "Admin@BarberBook2026!");

        var testDate = new LocalDate(2027, 5, 10);
        var app = await CreateIsolatedAppointmentAsync(
            AppointmentStatus.Completed,
            barberId: 1,
            customerId: 5,
            date: testDate);

        var url = $"/api/admin/appointments?status=completed&barberId=1&customerId=5&startDate=2027-05-10&endDate=2027-05-10";
        var response = await adminClient.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var pagedResult = await response.Content.ReadFromJsonAsync<AdminAppointmentPagedResultDto>();

        pagedResult.Should().NotBeNull();
        pagedResult!.Items.Should().NotBeEmpty();

        var matching = pagedResult.Items.FirstOrDefault(a => a.Id == app.Id);
        matching.Should().NotBeNull();
        matching!.Status.Should().Be("completed");
        matching.BarberId.Should().Be(1);
        matching.CustomerId.Should().Be(5);
        matching.Timeline.Should().NotBeEmpty();
    }

    [Fact]
    public async Task T20_BR02_RoleGuards_CustomerCannotAccess_AdminAppointmentsApi()
    {
        // Proves BR-02 & T-20: Customer cannot access admin appointments endpoints
        await EnsureSeededAsync();

        var customerClient = await CreateAuthenticatedClientAsync("juan@barberbook.local", "Demo@BarberBook2026!");

        var response = await customerClient.GetAsync("/api/admin/appointments");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var updateResponse = await customerClient.PostAsJsonAsync("/api/admin/appointments/1/status", new UpdateAppointmentStatusRequest
        {
            To = "confirmed"
        });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task T21_EO3_AdminScheduleAndBookings_ViewsRenderSuccessfully()
    {
        // Proves T-21 (admin): Day and week layouts available on admin bookings view
        await EnsureSeededAsync();

        var adminClient = await CreateAuthenticatedClientAsync("admin@barberbook.local", "Admin@BarberBook2026!");

        // 1. List view
        var listResponse = await adminClient.GetAsync("/admin/appointments?view=list");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var listHtml = await listResponse.Content.ReadAsStringAsync();
        listHtml.Should().Contain("listViewContainer");

        // 2. Day view
        var dayResponse = await adminClient.GetAsync("/admin/appointments?view=day");
        dayResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var dayHtml = await dayResponse.Content.ReadAsStringAsync();
        dayHtml.Should().Contain("dayViewContainer");

        // 3. Week view
        var weekResponse = await adminClient.GetAsync("/admin/appointments?view=week");
        weekResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var weekHtml = await weekResponse.Content.ReadAsStringAsync();
        weekHtml.Should().Contain("weekViewContainer");

        // 4. History page
        var historyResponse = await adminClient.GetAsync("/admin/history");
        historyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var historyHtml = await historyResponse.Content.ReadAsStringAsync();
        historyHtml.Should().Contain("Appointment History");
    }
}
