using System.Net;
using System.Net.Http.Json;
using BarberBook.Data;
using BarberBook.Data.Entities;
using BarberBook.Data.Enums;
using BarberBook.Data.Seeding;
using BarberBook.Web.Common;
using BarberBook.Web.Models.Appointment;
using BarberBook.Web.Models.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Xunit;

namespace BarberBook.Tests;

public class MyAppointmentsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly DateTimeZone _zone = DateTimeZoneProviders.Tzdb["Asia/Manila"];

    public MyAppointmentsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private async Task EnsureSeededAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await DatabaseSeeder.SeedAsync(scope.ServiceProvider);
    }

    private async Task EnsureWorkingHoursAsync(int barberId, int dayOfWeek)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
        var existing = await db.WorkingHours.Where(w => w.BarberId == barberId && w.DayOfWeek == dayOfWeek).ToListAsync();
        db.WorkingHours.RemoveRange(existing);
        db.WorkingHours.Add(new WorkingHours
        {
            BarberId = barberId,
            DayOfWeek = dayOfWeek,
            StartTime = new LocalTime(9, 0),
            EndTime = new LocalTime(18, 0)
        });
        await db.SaveChangesAsync();
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
    public async Task T05_CF6_OB3_BR09_CustomerCancelsAppointment_SlotUnblocksForOthers()
    {
        // Proves T-05 (Cancel): A customer cancels one appointment; cancellation is logged;
        // the cancelled slot can immediately be booked by someone else.
        await EnsureSeededAsync();
        await EnsureWorkingHoursAsync(1, 3); // Wednesday

        var clientJuan = await CreateAuthenticatedClientAsync("juan@barberbook.local", "Demo@BarberBook2026!");
        var clientMark = await CreateAuthenticatedClientAsync("mark@barberbook.local", "Demo@BarberBook2026!");

        var testDate = new LocalDate(2026, 12, 16); // Wednesday
        var slotInstant = testDate.At(new LocalTime(10, 0)).InZoneStrictly(_zone).ToInstant();

        // Clean up prior test data on test date
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
            var dayStartUtc = testDate.AtStartOfDayInZone(_zone).ToInstant();
            var dayEndUtc = testDate.PlusDays(1).AtStartOfDayInZone(_zone).ToInstant();

            var existingApps = await db.Appointments
                .Where(a => a.BarberId == 1 && a.StartsAt < dayEndUtc && a.EndsAt > dayStartUtc)
                .ToListAsync();
            db.Appointments.RemoveRange(existingApps);

            var existingDaysOff = await db.DaysOff
                .Where(d => d.BarberId == 1 && d.Date == testDate)
                .ToListAsync();
            db.DaysOff.RemoveRange(existingDaysOff);

            await db.SaveChangesAsync();
        }

        // 1. Juan books slot 10:00
        var bookPayload = new CreateAppointmentRequest
        {
            ServiceId = 1,
            BarberId = 1,
            StartsAt = slotInstant.ToString()
        };

        var bookResp = await clientJuan.PostAsJsonAsync("/api/appointments", bookPayload);
        bookResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await bookResp.Content.ReadFromJsonAsync<AppointmentDetailDto>();

        // 2. Mark tries to book the same slot -> 409 SLOT_TAKEN
        var collideResp = await clientMark.PostAsJsonAsync("/api/appointments", bookPayload);
        collideResp.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // 3. Juan cancels his appointment
        var cancelPayload = new CancelAppointmentRequest
        {
            Reason = "Unexpected meeting at work"
        };
        var cancelResp = await clientJuan.PostAsJsonAsync($"/api/appointments/{created!.Id}/cancel", cancelPayload);
        cancelResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify status in DB is Cancelled and history is written
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
            var appInDb = await db.Appointments
                .Include(a => a.StatusHistories)
                .FirstOrDefaultAsync(a => a.Id == created.Id);

            appInDb!.Status.Should().Be(AppointmentStatus.Cancelled);
            appInDb.StatusHistories.Should().Contain(h => h.ToStatus == AppointmentStatus.Cancelled && h.Note == "Unexpected meeting at work");
        }

        // 4. Mark now books the exact same slot -> Succeeds with 201 Created!
        var markBookResp = await clientMark.PostAsJsonAsync("/api/appointments", bookPayload);
        markBookResp.StatusCode.Should().Be(HttpStatusCode.Created, "Cancelled slot must immediately unblock for other customers");
    }

    [Fact]
    public async Task T05_CF6_OB3_BR09_CustomerReschedulesAppointment_ReturnsToPending_AndRevalidates()
    {
        // Proves T-05 (Reschedule): A customer reschedules an appointment;
        // validates with isSlotAvailable; keeps service, barber and price snapshot; returns status to pending; logs history.
        await EnsureSeededAsync();
        await EnsureWorkingHoursAsync(1, 4); // Thursday

        var clientJuan = await CreateAuthenticatedClientAsync("juan@barberbook.local", "Demo@BarberBook2026!");

        var testDate = new LocalDate(2026, 12, 17); // Thursday
        var initialSlot = testDate.At(new LocalTime(10, 0)).InZoneStrictly(_zone).ToInstant();
        var newSlot = testDate.At(new LocalTime(11, 0)).InZoneStrictly(_zone).ToInstant();

        // Clean up prior test data
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
            var dayStartUtc = testDate.AtStartOfDayInZone(_zone).ToInstant();
            var dayEndUtc = testDate.PlusDays(1).AtStartOfDayInZone(_zone).ToInstant();

            var existingApps = await db.Appointments
                .Where(a => a.BarberId == 1 && a.StartsAt < dayEndUtc && a.EndsAt > dayStartUtc)
                .ToListAsync();
            db.Appointments.RemoveRange(existingApps);

            var existingDaysOff = await db.DaysOff
                .Where(d => d.BarberId == 1 && d.Date == testDate)
                .ToListAsync();
            db.DaysOff.RemoveRange(existingDaysOff);

            await db.SaveChangesAsync();
        }

        // 1. Book initial appointment at 10:00
        var bookResp = await clientJuan.PostAsJsonAsync("/api/appointments", new CreateAppointmentRequest
        {
            ServiceId = 1, // Haircut (₱150)
            BarberId = 1,  // Ramon
            StartsAt = initialSlot.ToString()
        });
        bookResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await bookResp.Content.ReadFromJsonAsync<AppointmentDetailDto>();

        // 2. Reschedule to 11:00
        var reschedPayload = new RescheduleAppointmentRequest
        {
            StartsAt = newSlot.ToString()
        };

        var reschedResp = await clientJuan.PostAsJsonAsync($"/api/appointments/{created!.Id}/reschedule", reschedPayload);
        reschedResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await reschedResp.Content.ReadFromJsonAsync<AppointmentDetailDto>();
        updated.Should().NotBeNull();
        updated!.Status.Should().Be("pending", "Rescheduling must return appointment to pending (BR-09)");
        updated.StartsAt.Should().Be(newSlot.ToString());
        updated.Price.Should().Be(150.00m, "Price snapshot must remain immutable");
        updated.ServiceName.Should().Be("Haircut", "Service snapshot must remain unchanged");
        updated.BarberId.Should().Be(1);

        // 3. Verify status history in DB
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
            var history = await db.StatusHistories
                .Where(h => h.AppointmentId == created.Id)
                .OrderBy(h => h.ChangedAt)
                .ToListAsync();

            history.Should().HaveCount(2);
            history[1].FromStatus.Should().Be(AppointmentStatus.Pending);
            history[1].ToStatus.Should().Be(AppointmentStatus.Pending);
            history[1].Note.Should().Contain("Rescheduled to");
        }
    }

    [Fact]
    public async Task Reschedule_IntoOccupiedSlot_FailsWith409SlotTaken()
    {
        await EnsureSeededAsync();
        await EnsureWorkingHoursAsync(1, 5); // Friday

        var clientJuan = await CreateAuthenticatedClientAsync("juan@barberbook.local", "Demo@BarberBook2026!");
        var clientMark = await CreateAuthenticatedClientAsync("mark@barberbook.local", "Demo@BarberBook2026!");

        var testDate = new LocalDate(2026, 12, 18); // Friday
        var slotA = testDate.At(new LocalTime(10, 0)).InZoneStrictly(_zone).ToInstant();
        var slotB = testDate.At(new LocalTime(11, 0)).InZoneStrictly(_zone).ToInstant();

        // Clean up prior test data
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
            var dayStartUtc = testDate.AtStartOfDayInZone(_zone).ToInstant();
            var dayEndUtc = testDate.PlusDays(1).AtStartOfDayInZone(_zone).ToInstant();

            var existingApps = await db.Appointments
                .Where(a => a.BarberId == 1 && a.StartsAt < dayEndUtc && a.EndsAt > dayStartUtc)
                .ToListAsync();
            db.Appointments.RemoveRange(existingApps);

            var existingDaysOff = await db.DaysOff
                .Where(d => d.BarberId == 1 && d.Date == testDate)
                .ToListAsync();
            db.DaysOff.RemoveRange(existingDaysOff);

            await db.SaveChangesAsync();
        }

        // Juan books slot A (10:00)
        var respA = await clientJuan.PostAsJsonAsync("/api/appointments", new CreateAppointmentRequest
        {
            ServiceId = 1,
            BarberId = 1,
            StartsAt = slotA.ToString()
        });
        respA.StatusCode.Should().Be(HttpStatusCode.Created);
        var juanApp = await respA.Content.ReadFromJsonAsync<AppointmentDetailDto>();

        // Mark books slot B (11:00)
        var respB = await clientMark.PostAsJsonAsync("/api/appointments", new CreateAppointmentRequest
        {
            ServiceId = 1,
            BarberId = 1,
            StartsAt = slotB.ToString()
        });
        respB.StatusCode.Should().Be(HttpStatusCode.Created);

        // Juan tries to reschedule into slot B (11:00) which Mark holds -> 409 Conflict
        var reschedResp = await clientJuan.PostAsJsonAsync($"/api/appointments/{juanApp!.Id}/reschedule", new RescheduleAppointmentRequest
        {
            StartsAt = slotB.ToString()
        });

        reschedResp.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var content = await reschedResp.Content.ReadAsStringAsync();
        content.Should().Contain("SLOT_TAKEN");
    }

    [Fact]
    public async Task Customer_CannotTouchAnotherCustomersAppointment_Returns403()
    {
        // Rule BR-02: Scoped authorization
        await EnsureSeededAsync();
        await EnsureWorkingHoursAsync(1, 6); // Saturday

        var clientJuan = await CreateAuthenticatedClientAsync("juan@barberbook.local", "Demo@BarberBook2026!");
        var clientMark = await CreateAuthenticatedClientAsync("mark@barberbook.local", "Demo@BarberBook2026!");

        var testDate = new LocalDate(2026, 12, 19); // Saturday
        var slotInstant = testDate.At(new LocalTime(14, 0)).InZoneStrictly(_zone).ToInstant();

        // Clean up prior test data
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
            var dayStartUtc = testDate.AtStartOfDayInZone(_zone).ToInstant();
            var dayEndUtc = testDate.PlusDays(1).AtStartOfDayInZone(_zone).ToInstant();

            var existingApps = await db.Appointments
                .Where(a => a.BarberId == 1 && a.StartsAt < dayEndUtc && a.EndsAt > dayStartUtc)
                .ToListAsync();
            db.Appointments.RemoveRange(existingApps);

            var existingDaysOff = await db.DaysOff
                .Where(d => d.BarberId == 1 && d.Date == testDate)
                .ToListAsync();
            db.DaysOff.RemoveRange(existingDaysOff);

            await db.SaveChangesAsync();
        }

        var postResp = await clientJuan.PostAsJsonAsync("/api/appointments", new CreateAppointmentRequest
        {
            ServiceId = 1,
            BarberId = 1,
            StartsAt = slotInstant.ToString()
        });
        postResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var juanApp = await postResp.Content.ReadFromJsonAsync<AppointmentDetailDto>();

        // Mark tries to cancel Juan's appointment -> 403 Forbidden
        var cancelResp = await clientMark.PostAsJsonAsync($"/api/appointments/{juanApp!.Id}/cancel", new CancelAppointmentRequest());
        cancelResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Mark tries to reschedule Juan's appointment -> 403 Forbidden
        var reschedResp = await clientMark.PostAsJsonAsync($"/api/appointments/{juanApp.Id}/reschedule", new RescheduleAppointmentRequest
        {
            StartsAt = testDate.At(new LocalTime(15, 0)).InZoneStrictly(_zone).ToInstant().ToString()
        });
        reschedResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TerminalStatuses_CannotBeCancelledOrRescheduled_Returns409()
    {
        // Transition table §9: Completed, rejected and cancelled appointments cannot be cancelled or rescheduled (409 INVALID_TRANSITION).
        await EnsureSeededAsync();
        var clientJuan = await CreateAuthenticatedClientAsync("juan@barberbook.local", "Demo@BarberBook2026!");

        int completedAppId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
            // Juan's seed appointment is completed
            var completedApp = await db.Appointments
                .FirstAsync(a => a.Customer.Email == "juan@barberbook.local" && a.Status == AppointmentStatus.Completed);
            completedAppId = completedApp.Id;
        }

        // 1. Try to cancel completed appointment -> 409 Conflict (INVALID_TRANSITION)
        var cancelResp = await clientJuan.PostAsJsonAsync($"/api/appointments/{completedAppId}/cancel", new CancelAppointmentRequest());
        cancelResp.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var cancelContent = await cancelResp.Content.ReadAsStringAsync();
        cancelContent.Should().Contain("INVALID_TRANSITION");

        // 2. Try to reschedule completed appointment -> 409 Conflict (INVALID_TRANSITION)
        var futureSlot = new LocalDate(2026, 12, 21).At(new LocalTime(10, 0)).InZoneStrictly(_zone).ToInstant();
        var reschedResp = await clientJuan.PostAsJsonAsync($"/api/appointments/{completedAppId}/reschedule", new RescheduleAppointmentRequest
        {
            StartsAt = futureSlot.ToString()
        });
        reschedResp.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var reschedContent = await reschedResp.Content.ReadAsStringAsync();
        reschedContent.Should().Contain("INVALID_TRANSITION");
    }

    [Fact]
    public async Task T09_GetMyAppointments_UpcomingAndHistory_ReturnsTimeline()
    {
        // Proves T-09 (customer side): A customer opens history; customer sees only their own,
        // with the status timeline.
        await EnsureSeededAsync();
        var clientJuan = await CreateAuthenticatedClientAsync("juan@barberbook.local", "Demo@BarberBook2026!");

        // 1. History scope returns Juan's completed seed appointments (newest first)
        var historyResp = await clientJuan.GetAsync("/api/me/appointments?scope=history");
        historyResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var historyList = await historyResp.Content.ReadFromJsonAsync<List<AppointmentSummaryDto>>();
        historyList.Should().NotBeNull();
        historyList!.Should().NotBeEmpty();
        historyList!.All(a => a.Status == "completed" || a.Status == "cancelled" || a.Status == "rejected" || !a.CanCancelOrReschedule).Should().BeTrue();

        // 2. GET /api/appointments/:id includes status timeline
        var firstAppId = historyList!.First().Id;
        var detailResp = await clientJuan.GetAsync($"/api/appointments/{firstAppId}");
        detailResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await detailResp.Content.ReadFromJsonAsync<AppointmentDetailDto>();
        detail.Should().NotBeNull();
        detail!.Timeline.Should().NotBeNull();
        detail.Timeline.Should().NotBeEmpty("Status timeline must be present in appointment details");
    }
}

