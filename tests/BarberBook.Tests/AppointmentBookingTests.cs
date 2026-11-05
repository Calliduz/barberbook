using System.Net;
using System.Net.Http.Json;
using BarberBook.Data;
using BarberBook.Data.Entities;
using BarberBook.Data.Enums;
using BarberBook.Data.Seeding;
using BarberBook.Web.Models.Appointment;
using BarberBook.Web.Models.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Xunit;

namespace BarberBook.Tests;

public class AppointmentBookingTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly DateTimeZone _zone = DateTimeZoneProviders.Tzdb["Asia/Manila"];

    public AppointmentBookingTests(WebApplicationFactory<Program> factory)
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
    public async Task T03_ConcurrentBooking_SameSlot_ExactlyOne201_AndOne409()
    {
        // Mandatory Acceptance Test T-03:
        // Two customers book the same barber and slot at the exact same moment.
        // Exactly one succeeds (201); the other gets a clear "SLOT_TAKEN" error (409).
        await EnsureSeededAsync();
        await EnsureWorkingHoursAsync(1, 3); // Wednesday

        var clientJuan = await CreateAuthenticatedClientAsync("juan@barberbook.local", "Demo@BarberBook2026!");
        var clientMark = await CreateAuthenticatedClientAsync("mark@barberbook.local", "Demo@BarberBook2026!");

        var testDate = new LocalDate(2026, 12, 9); // Wednesday
        var slotManila = testDate.At(new LocalTime(10, 0));
        var slotInstant = slotManila.InZoneStrictly(_zone).ToInstant();

        // Clean up any existing appointments or days off for barber 1 on that date
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

        var payload = new CreateAppointmentRequest
        {
            ServiceId = 1, // Haircut (30 min)
            BarberId = 1,  // Ramon
            StartsAt = slotInstant.ToString(),
            Notes = "Concurrent booking test"
        };

        // Fire both simultaneous POST requests
        var task1 = clientJuan.PostAsJsonAsync("/api/appointments", payload);
        var task2 = clientMark.PostAsJsonAsync("/api/appointments", payload);

        var responses = await Task.WhenAll(task1, task2);

        var statusCodes = responses.Select(r => r.StatusCode).ToList();
        statusCodes.Should().ContainSingle(s => s == HttpStatusCode.Created, "Exactly one booking must succeed with 201 Created");
        statusCodes.Should().ContainSingle(s => s == HttpStatusCode.Conflict, "The other booking must fail with 409 Conflict");

        var conflictResponse = responses.First(r => r.StatusCode == HttpStatusCode.Conflict);
        var errorContent = await conflictResponse.Content.ReadAsStringAsync();
        errorContent.Should().Contain("SLOT_TAKEN", "The conflict response must specify error code SLOT_TAKEN");

        // Verify database state: exactly one appointment recorded for that slot
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
            var count = await db.Appointments.CountAsync(a => a.BarberId == 1 && a.StartsAt == slotInstant);
            count.Should().Be(1, "Exactly one appointment must exist in the database for the contested slot");
        }
    }

    [Fact]
    public async Task T02_CF4_CF5_CustomerBooks_CreatesPending_GeneratesReferenceCode_AndSnapshot()
    {
        // Acceptance Test T-02:
        // A customer books Haircut with Ramon at an open slot.
        // Appointment is pending; confirmation shows reference code, service, barber, 12-hour time, and ₱ price.
        await EnsureSeededAsync();
        await EnsureWorkingHoursAsync(1, 4); // Thursday

        var client = await CreateAuthenticatedClientAsync("juan@barberbook.local", "Demo@BarberBook2026!");

        var testDate = new LocalDate(2026, 12, 10); // Thursday
        var slotManila = testDate.At(new LocalTime(11, 0));
        var slotInstant = slotManila.InZoneStrictly(_zone).ToInstant();

        // Clean up prior test data on that date
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

        var payload = new CreateAppointmentRequest
        {
            ServiceId = 1, // Haircut (₱150, 30 min)
            BarberId = 1,  // Ramon
            StartsAt = slotInstant.ToString(),
            Notes = "Please keep the top medium length"
        };

        var postResponse = await client.PostAsJsonAsync("/api/appointments", payload);
        postResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await postResponse.Content.ReadFromJsonAsync<AppointmentDetailDto>();
        created.Should().NotBeNull();
        created!.ReferenceCode.Should().MatchRegex(@"^BB-20261210-\d{4}$", "Reference code format must be BB-YYYYMMDD-####");
        created.Status.Should().Be("pending");
        created.StatusDisplay.Should().Be("Pending — awaiting confirmation by the shop");
        created.ServiceName.Should().Be("Haircut");
        created.Price.Should().Be(150.00m);
        created.FormattedPrice.Should().Be("₱150.00");
        created.FormattedTime.Should().Be("11:00 AM – 11:30 AM");
        created.BarberName.Should().Be("Ramon");
        created.CustomerName.Should().Be("Juan Dela Cruz");
        created.Notes.Should().Be("Please keep the top medium length");

        // Verify status history row exists in database
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
            var history = await db.StatusHistories
                .Where(h => h.AppointmentId == created.Id)
                .ToListAsync();

            history.Should().HaveCount(1);
            history[0].FromStatus.Should().BeNull();
            history[0].ToStatus.Should().Be(AppointmentStatus.Pending);
            history[0].ChangedByRole.Should().Be("Customer");
        }

        // Verify GET /api/appointments/:id returns the same confirmation details
        var getResponse = await client.GetAsync($"/api/appointments/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var fetched = await getResponse.Content.ReadFromJsonAsync<AppointmentDetailDto>();
        fetched.Should().NotBeNull();
        fetched!.ReferenceCode.Should().Be(created.ReferenceCode);
    }

    [Fact]
    public async Task BR03_BookingInPast_ReturnsBadRequest()
    {
        await EnsureSeededAsync();
        var client = await CreateAuthenticatedClientAsync("juan@barberbook.local", "Demo@BarberBook2026!");

        var pastInstant = Instant.FromUtc(2025, 1, 1, 0, 0);
        var payload = new CreateAppointmentRequest
        {
            ServiceId = 1,
            BarberId = 1,
            StartsAt = pastInstant.ToString()
        };

        var response = await client.PostAsJsonAsync("/api/appointments", payload);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("PAST_TIME");
    }

    [Fact]
    public async Task BR05_BookingOffGrid_ReturnsBadRequest()
    {
        await EnsureSeededAsync();
        var client = await CreateAuthenticatedClientAsync("juan@barberbook.local", "Demo@BarberBook2026!");

        // 10:15 is not on the 30-minute grid
        var testDate = new LocalDate(2026, 12, 11);
        var offGridInstant = testDate.At(new LocalTime(10, 15)).InZoneStrictly(_zone).ToInstant();

        var payload = new CreateAppointmentRequest
        {
            ServiceId = 1,
            BarberId = 1,
            StartsAt = offGridInstant.ToString()
        };

        var response = await client.PostAsJsonAsync("/api/appointments", payload);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("OFF_GRID_TIME");
    }

    [Fact]
    public async Task BR03_BookingOutsideWorkingHours_ReturnsBadRequest()
    {
        await EnsureSeededAsync();
        var client = await CreateAuthenticatedClientAsync("juan@barberbook.local", "Demo@BarberBook2026!");

        // Ramon works 9:00 AM - 6:00 PM. 8:00 PM (20:00) is outside working hours
        var testDate = new LocalDate(2026, 12, 11);
        var outsideInstant = testDate.At(new LocalTime(20, 0)).InZoneStrictly(_zone).ToInstant();

        var payload = new CreateAppointmentRequest
        {
            ServiceId = 1,
            BarberId = 1,
            StartsAt = outsideInstant.ToString()
        };

        var response = await client.PostAsJsonAsync("/api/appointments", payload);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("OUTSIDE_WORKING_HOURS");
    }

    [Fact]
    public async Task BR03_BookingOnDayOff_ReturnsBadRequest()
    {
        await EnsureSeededAsync();
        var client = await CreateAuthenticatedClientAsync("juan@barberbook.local", "Demo@BarberBook2026!");

        var testDate = new LocalDate(2026, 12, 12); // Saturday

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
            var existing = await db.DaysOff.Where(d => d.BarberId == 1 && d.Date == testDate).ToListAsync();
            db.DaysOff.RemoveRange(existing);

            db.DaysOff.Add(new DayOff
            {
                BarberId = 1,
                Date = testDate,
                Reason = "Personal vacation"
            });
            await db.SaveChangesAsync();
        }

        var slotInstant = testDate.At(new LocalTime(10, 0)).InZoneStrictly(_zone).ToInstant();
        var payload = new CreateAppointmentRequest
        {
            ServiceId = 1,
            BarberId = 1,
            StartsAt = slotInstant.ToString()
        };

        var response = await client.PostAsJsonAsync("/api/appointments", payload);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("DAY_OFF");
    }

    [Fact]
    public async Task BR08_BR14_PriceSnapshot_IsImmutable_WhenServicePriceChanges()
    {
        // Rule BR-08 / BR-14:
        // An appointment stores price and service_name at booking time.
        // A service price change never alters existing appointments.
        await EnsureSeededAsync();
        await EnsureWorkingHoursAsync(1, 1); // Monday
        var client = await CreateAuthenticatedClientAsync("juan@barberbook.local", "Demo@BarberBook2026!");

        var testDate = new LocalDate(2026, 12, 14); // Monday
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

        var payload = new CreateAppointmentRequest
        {
            ServiceId = 1, // Haircut initial price: ₱150
            BarberId = 1,
            StartsAt = slotInstant.ToString()
        };

        var createResponse = await client.PostAsJsonAsync("/api/appointments", payload);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<AppointmentDetailDto>();
        created!.Price.Should().Be(150.00m);

        // Admin modifies service price from ₱150 to ₱300
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
            var service = await db.Services.FindAsync(1);
            service!.Price = 300.00m;
            await db.SaveChangesAsync();
        }

        // Fetch appointment again and verify snapshot is still ₱150
        var getResponse = await client.GetAsync($"/api/appointments/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await getResponse.Content.ReadFromJsonAsync<AppointmentDetailDto>();

        fetched!.Price.Should().Be(150.00m, "Appointment snapshot price must not change when service price changes");
        fetched.FormattedPrice.Should().Be("₱150.00");

        // Revert service price back to ₱150 for test fixture cleanliness
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
            var service = await db.Services.FindAsync(1);
            service!.Price = 150.00m;
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task BR02_GetAppointment_EnforcesRoleAndOwnership()
    {
        // Rule BR-02: Scoped authorization.
        // Customer B cannot view Customer A's appointment (403 Forbidden).
        // Assigned Barber and Admin can view it (200 OK).
        await EnsureSeededAsync();
        await EnsureWorkingHoursAsync(1, 2); // Tuesday

        var clientJuan = await CreateAuthenticatedClientAsync("juan@barberbook.local", "Demo@BarberBook2026!");
        var clientMark = await CreateAuthenticatedClientAsync("mark@barberbook.local", "Demo@BarberBook2026!");
        var clientRamon = await CreateAuthenticatedClientAsync("ramon@barberbook.local", "Demo@BarberBook2026!");
        var clientAdmin = await CreateAuthenticatedClientAsync("admin@barberbook.local", "Admin@BarberBook2026!");

        var testDate = new LocalDate(2026, 12, 15); // Tuesday
        var slotInstant = testDate.At(new LocalTime(15, 0)).InZoneStrictly(_zone).ToInstant();

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

        var payload = new CreateAppointmentRequest
        {
            ServiceId = 1,
            BarberId = 1, // Ramon
            StartsAt = slotInstant.ToString()
        };

        var postResp = await clientJuan.PostAsJsonAsync("/api/appointments", payload);
        postResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await postResp.Content.ReadFromJsonAsync<AppointmentDetailDto>();

        // 1. Mark (another customer) tries to view Juan's appointment -> 403 Forbidden
        var foreignCustomerResp = await clientMark.GetAsync($"/api/appointments/{created!.Id}");
        foreignCustomerResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 2. Ramon (the assigned barber) views the appointment -> 200 OK
        var barberResp = await clientRamon.GetAsync($"/api/appointments/{created.Id}");
        barberResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. Admin views the appointment -> 200 OK
        var adminResp = await clientAdmin.GetAsync($"/api/appointments/{created.Id}");
        adminResp.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
