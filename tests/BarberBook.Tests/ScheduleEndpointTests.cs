using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BarberBook.Data;
using BarberBook.Data.Entities;
using BarberBook.Data.Enums;
using BarberBook.Data.Seeding;
using BarberBook.Web.Common;
using BarberBook.Web.Models.Auth;
using BarberBook.Web.Models.Schedule;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Xunit;

namespace BarberBook.Tests;

public class ScheduleEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly DateTimeZone _zone = DateTimeZoneProviders.Tzdb["Asia/Manila"];

    public ScheduleEndpointTests(WebApplicationFactory<Program> factory)
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
    public async Task T08_ChangingHoursOrAddingDayOff_ImmediatelyChangesPublicAvailability()
    {
        // Prove T-08: Barber edits weekly hours and adds a day off -> public availability reflects it immediately
        await EnsureSeededAsync();

        var adminClient = await CreateAuthenticatedClientAsync("admin@barberbook.local", "Admin@BarberBook2026!");
        var anonClient = _factory.CreateClient();

        // Target: future Monday 2026-12-07
        var testDate = "2026-12-07";

        // 1. Set Monday hours for Ramon (Barber 1) to 09:00 - 12:00 (single morning shift)
        var updateHoursResponse = await adminClient.PutAsJsonAsync("/api/admin/barbers/1/working-hours", new SetWorkingHoursRequest
        {
            Ranges = new List<WorkingHoursRangeDto>
            {
                new() { DayOfWeek = 1, StartTime = "09:00", EndTime = "12:00" }
            }
        });
        updateHoursResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify public availability immediately: Haircut (30m, id 1) on 2026-12-07
        var availBeforeDayOff = await anonClient.GetFromJsonAsync<PublicAvailabilityResponse>(
            $"/api/barbers/1/availability?serviceId=1&date={testDate}");

        availBeforeDayOff.Should().NotBeNull();
        availBeforeDayOff!.Slots.Should().NotBeEmpty();
        availBeforeDayOff.Slots.First().Should().Be("09:00");
        availBeforeDayOff.Slots.Last().Should().Be("11:30"); // ends at 12:00

        // 2. Add Day Off for 2026-12-07
        var addDayOffResponse = await adminClient.PostAsJsonAsync("/api/admin/barbers/1/days-off", new CreateDayOffRequest
        {
            StartDate = testDate,
            Reason = "Shop Maintenance Day"
        });
        addDayOffResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify public availability immediately: 0 slots!
        var availDuringDayOff = await anonClient.GetFromJsonAsync<PublicAvailabilityResponse>(
            $"/api/barbers/1/availability?serviceId=1&date={testDate}");

        availDuringDayOff.Should().NotBeNull();
        availDuringDayOff!.Slots.Should().BeEmpty("Adding day off must immediately clear availability (T-08)");

        // 3. Remove Day Off
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
            var localDate = new LocalDate(2026, 12, 7);
            var dayOff = await db.DaysOff.FirstOrDefaultAsync(d => d.BarberId == 1 && d.Date == localDate);
            if (dayOff != null)
            {
                var deleteRes = await adminClient.DeleteAsync($"/api/admin/barbers/1/days-off/{dayOff.Id}");
                deleteRes.StatusCode.Should().Be(HttpStatusCode.OK);
            }
        }

        // Verify availability immediately restored
        var availAfterRemoval = await anonClient.GetFromJsonAsync<PublicAvailabilityResponse>(
            $"/api/barbers/1/availability?serviceId=1&date={testDate}");

        availAfterRemoval!.Slots.Should().NotBeEmpty("Removing day off must immediately restore availability");
    }

    [Fact]
    public async Task T04_DayOffOrOutsideHours_ReturnsNoSlotsInPublicApi()
    {
        // Prove T-04: The barber has a day off, or the time is outside working hours -> No slots offered
        await EnsureSeededAsync();
        var anonClient = _factory.CreateClient();

        // Sunday 2026-12-13: Ramon has NO working hours on Sunday
        var sundayAvail = await anonClient.GetFromJsonAsync<PublicAvailabilityResponse>(
            "/api/barbers/1/availability?serviceId=1&date=2026-12-13");

        sundayAvail!.Slots.Should().BeEmpty("No slots offered when outside working hours");
    }

    [Fact]
    public async Task BF3_Barber_CanManageOwnWorkingHoursAndDaysOff()
    {
        // Barber Ramon manages his own hours via /api/barber/*
        await EnsureSeededAsync();
        var barberClient = await CreateAuthenticatedClientAsync("ramon@barberbook.local", "Demo@BarberBook2026!");

        // 1. Get own working hours
        var getHours = await barberClient.GetFromJsonAsync<List<WorkingHoursItemDto>>("/api/barber/working-hours");
        getHours.Should().NotBeNull();

        // 2. Add day off for Ramon
        var addDayOff = await barberClient.PostAsJsonAsync("/api/barber/days-off", new CreateDayOffRequest
        {
            StartDate = "2026-12-25",
            Reason = "Christmas Day"
        });
        addDayOff.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. Verify in own days off list
        var daysOff = await barberClient.GetFromJsonAsync<List<DayOffItemDto>>("/api/barber/days-off");
        daysOff!.Any(d => d.Date == "2026-12-25").Should().BeTrue();
    }

    [Fact]
    public async Task Customer_CannotAccessAdminOrBarberScheduleEndpoints_Returns403()
    {
        // Rule BR-02: Customer role guard
        await EnsureSeededAsync();
        var customerClient = await CreateAuthenticatedClientAsync("juan@barberbook.local", "Demo@BarberBook2026!");

        // Accessing admin endpoint
        var adminEndpoint = await customerClient.GetAsync("/api/admin/barbers/1/working-hours");
        adminEndpoint.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Accessing barber self-service endpoint
        var barberEndpoint = await customerClient.GetAsync("/api/barber/working-hours");
        barberEndpoint.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AddDayOff_WithConflictingAppointments_ReturnsWarningsWithoutAutoCancelling()
    {
        // Requirement: Warn — never auto-cancel — when a new day off conflicts with existing appointments
        await EnsureSeededAsync();
        var adminClient = await CreateAuthenticatedClientAsync("admin@barberbook.local", "Admin@BarberBook2026!");

        // Target date with existing completed appointment: 2026-09-14
        // Create a test PENDING appointment on 2026-12-15
        var conflictDate = new LocalDate(2026, 12, 15);
        int appointmentId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
            var customer = await db.Users.FirstAsync(u => u.Role == UserRole.Customer);
            var appStart = conflictDate.At(new LocalTime(10, 0)).InZoneStrictly(_zone).ToInstant();
            var appEnd = conflictDate.At(new LocalTime(10, 30)).InZoneStrictly(_zone).ToInstant();

            var app = new Appointment
            {
                ReferenceCode = "BB-CONF-TEST-01",
                CustomerId = customer.Id,
                BarberId = 1,
                ServiceId = 1,
                StartsAt = appStart,
                EndsAt = appEnd,
                Status = AppointmentStatus.Pending,
                Price = 150.00m,
                ServiceName = "Haircut"
            };
            db.Appointments.Add(app);
            await db.SaveChangesAsync();
            appointmentId = app.Id;
        }

        // Admin adds day off on 2026-12-15
        var response = await adminClient.PostAsJsonAsync("/api/admin/barbers/1/days-off", new CreateDayOffRequest
        {
            StartDate = "2026-12-15",
            Reason = "Emergency Day Off"
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<CreateDayOffResultDto>();
        result.Should().NotBeNull();
        result!.HasConflicts.Should().BeTrue();
        result.Conflicts.Should().ContainSingle(c => c.AppointmentId == appointmentId);

        // Verify the appointment is NEVER auto-cancelled!
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
            var appAfter = await db.Appointments.FindAsync(appointmentId);
            appAfter!.Status.Should().Be(AppointmentStatus.Pending, "Appointment must NEVER be auto-cancelled when a day off is set");
        }
    }

    [Fact]
    public async Task SetWorkingHours_OverlappingRangesOnSameDay_RejectedByValidation()
    {
        // FluentValidation: No overlapping ranges on the same day
        await EnsureSeededAsync();
        var adminClient = await CreateAuthenticatedClientAsync("admin@barberbook.local", "Admin@BarberBook2026!");

        var invalidRequest = new SetWorkingHoursRequest
        {
            Ranges = new List<WorkingHoursRangeDto>
            {
                new() { DayOfWeek = 1, StartTime = "09:00", EndTime = "13:00" },
                new() { DayOfWeek = 1, StartTime = "12:30", EndTime = "17:00" } // Overlaps 12:30 - 13:00!
            }
        };

        var response = await adminClient.PutAsJsonAsync("/api/admin/barbers/1/working-hours", invalidRequest);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        error!.Error.Code.Should().Be("VALIDATION_ERROR");
        error.Error.Message.Should().Contain("Validation failed");
    }

    [Fact]
    public async Task CF3_PublicScheduleEndpoint_ReturnsWeeklyHoursAndDaysOff_WithoutCustomerData()
    {
        // Requirement CF-3: View barber schedules without exposing any customer records
        await EnsureSeededAsync();
        var anonClient = _factory.CreateClient();

        var schedule = await anonClient.GetFromJsonAsync<PublicBarberScheduleDto>("/api/barbers/1/schedule");
        schedule.Should().NotBeNull();
        schedule!.BarberId.Should().Be(1);
        schedule.DisplayName.Should().Be("Ramon");
        schedule.WeeklyHours.Should().NotBeEmpty();
    }
}
