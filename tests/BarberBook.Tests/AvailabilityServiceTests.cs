using BarberBook.Data;
using BarberBook.Data.Entities;
using BarberBook.Data.Enums;
using BarberBook.Data.Seeding;
using BarberBook.Web.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Xunit;

namespace BarberBook.Tests;

public class FixedTestClock : IClock
{
    public Instant CurrentInstant { get; set; }
    public FixedTestClock(Instant instant) => CurrentInstant = instant;
    public Instant GetCurrentInstant() => CurrentInstant;
}

public class AvailabilityServiceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly DateTimeZone _zone = DateTimeZoneProviders.Tzdb["Asia/Manila"];

    public AvailabilityServiceTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private async Task EnsureSeededAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await DatabaseSeeder.SeedAsync(scope.ServiceProvider);
    }

    private async Task<(Barber barber, Service s30, Service s60, Service s90)> SetupAvailabilityFixturesAsync(BarberBookDbContext db)
    {
        // Find Ramon (Barber ID 1 or first active barber)
        var barber = await db.Barbers.Include(b => b.User).FirstAsync(b => b.IsActive && b.User.IsActive);

        // Find or create standard services (30m, 60m, 90m)
        var s30 = await db.Services.FirstAsync(s => s.DurationMinutes == 30);
        var s60 = await db.Services.FirstAsync(s => s.DurationMinutes == 60);
        var s90 = await db.Services.FirstAsync(s => s.DurationMinutes == 90);

        return (barber, s30, s60, s90);
    }

    [Fact]
    public async Task GetAvailableSlots_NormalDay_Returns30MinuteGrid()
    {
        // Arrange
        await EnsureSeededAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
        var (barber, s30, _, _) = await SetupAvailabilityFixturesAsync(db);

        // Monday: 2026-11-16 (future date)
        var date = new LocalDate(2026, 11, 16);
        int dow = 1; // Monday

        // Set standard 9:00 - 18:00 hours for Monday
        var existingHours = await db.WorkingHours.Where(w => w.BarberId == barber.Id && w.DayOfWeek == dow).ToListAsync();
        db.WorkingHours.RemoveRange(existingHours);
        db.WorkingHours.Add(new WorkingHours
        {
            BarberId = barber.Id,
            DayOfWeek = dow,
            StartTime = new LocalTime(9, 0),
            EndTime = new LocalTime(18, 0)
        });

        // Ensure no appointments or day off for this date
        var existingDaysOff = await db.DaysOff.Where(d => d.BarberId == barber.Id && d.Date == date).ToListAsync();
        db.DaysOff.RemoveRange(existingDaysOff);
        await db.SaveChangesAsync();

        // Clock set before the date (e.g. 2026-11-15 00:00 UTC)
        var fixedClock = new FixedTestClock(Instant.FromUtc(2026, 11, 15, 0, 0));
        var availabilityService = new AvailabilityService(db, fixedClock, _zone);

        // Act
        var slots = await availabilityService.GetAvailableSlotsAsync(barber.Id, s30.Id, date);

        // Assert: 9:00 to 17:30 every 30 minutes = 18 slots
        slots.Should().NotBeEmpty();
        slots.Count.Should().Be(18);
        slots.First().Should().Be(new LocalTime(9, 0));
        slots.Last().Should().Be(new LocalTime(17, 30));
    }

    [Fact]
    public async Task GetAvailableSlots_SplitRanges_LunchBreak_NoSlotCrossesWindow()
    {
        // Arrange: Shift 1: 09:00 - 12:00, Shift 2: 13:00 - 18:00
        await EnsureSeededAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
        var (barber, _, s60, _) = await SetupAvailabilityFixturesAsync(db);

        var date = new LocalDate(2026, 11, 17); // Tuesday
        int dow = 2;

        var existingHours = await db.WorkingHours.Where(w => w.BarberId == barber.Id && w.DayOfWeek == dow).ToListAsync();
        db.WorkingHours.RemoveRange(existingHours);
        db.WorkingHours.Add(new WorkingHours
        {
            BarberId = barber.Id,
            DayOfWeek = dow,
            StartTime = new LocalTime(9, 0),
            EndTime = new LocalTime(12, 0)
        });
        db.WorkingHours.Add(new WorkingHours
        {
            BarberId = barber.Id,
            DayOfWeek = dow,
            StartTime = new LocalTime(13, 0),
            EndTime = new LocalTime(18, 0)
        });
        await db.SaveChangesAsync();

        var fixedClock = new FixedTestClock(Instant.FromUtc(2026, 11, 15, 0, 0));
        var availabilityService = new AvailabilityService(db, fixedClock, _zone);

        // Act: 60-minute service
        var slots = await availabilityService.GetAvailableSlotsAsync(barber.Id, s60.Id, date);

        // Assert:
        // Morning 60m slots: 09:00, 09:30, 10:00, 10:30, 11:00 (ends at 12:00).
        // 11:30 MUST NOT be present because 11:30 + 60m = 12:30 (exceeds 12:00 window, BR-03).
        // 12:00 and 12:30 MUST NOT be present.
        // Afternoon 60m slots start at 13:00, 13:30, ..., 17:00 (ends at 18:00).
        slots.Should().Contain(new LocalTime(9, 0));
        slots.Should().Contain(new LocalTime(11, 0));
        slots.Should().NotContain(new LocalTime(11, 30), "Service cannot span across the lunch break window");
        slots.Should().NotContain(new LocalTime(12, 0));
        slots.Should().NotContain(new LocalTime(12, 30));
        slots.Should().Contain(new LocalTime(13, 0));
        slots.Should().Contain(new LocalTime(17, 0));
        slots.Should().NotContain(new LocalTime(17, 30), "A 60m service starting at 17:30 would exceed closing time 18:00");
    }

    [Fact]
    public async Task GetAvailableSlots_DurationLongerThanRemainingWindow_Rejected()
    {
        // Range: 9:00 - 10:00 (60 minutes). Service: 90 minutes.
        await EnsureSeededAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
        var (barber, _, _, s90) = await SetupAvailabilityFixturesAsync(db);

        var date = new LocalDate(2026, 11, 18); // Wednesday
        int dow = 3;

        var existingHours = await db.WorkingHours.Where(w => w.BarberId == barber.Id && w.DayOfWeek == dow).ToListAsync();
        db.WorkingHours.RemoveRange(existingHours);
        db.WorkingHours.Add(new WorkingHours
        {
            BarberId = barber.Id,
            DayOfWeek = dow,
            StartTime = new LocalTime(9, 0),
            EndTime = new LocalTime(10, 0) // Only 60 min window
        });
        await db.SaveChangesAsync();

        var fixedClock = new FixedTestClock(Instant.FromUtc(2026, 11, 15, 0, 0));
        var availabilityService = new AvailabilityService(db, fixedClock, _zone);

        var slots = await availabilityService.GetAvailableSlotsAsync(barber.Id, s90.Id, date);

        slots.Should().BeEmpty("90-minute service cannot fit in a 60-minute window");
    }

    [Fact]
    public async Task GetAvailableSlots_DayOff_ReturnsNoSlots()
    {
        // Rule BR-03, T-04: If date is a day off, no slots are offered
        await EnsureSeededAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
        var (barber, s30, _, _) = await SetupAvailabilityFixturesAsync(db);

        var date = new LocalDate(2026, 11, 19);

        // Add day off
        db.DaysOff.Add(new DayOff
        {
            BarberId = barber.Id,
            Date = date,
            Reason = "Personal Holiday"
        });
        await db.SaveChangesAsync();

        var fixedClock = new FixedTestClock(Instant.FromUtc(2026, 11, 15, 0, 0));
        var availabilityService = new AvailabilityService(db, fixedClock, _zone);

        var slots = await availabilityService.GetAvailableSlotsAsync(barber.Id, s30.Id, date);

        slots.Should().BeEmpty("Day off must prevent all slot availability");
    }

    [Fact]
    public async Task GetAvailableSlots_PastTimes_ExcludesEarlierSlotsOnSameDay()
    {
        // Rule BR-03: Slot must be in the future
        await EnsureSeededAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
        var (barber, s30, _, _) = await SetupAvailabilityFixturesAsync(db);

        var date = new LocalDate(2026, 11, 20); // Friday
        int dow = 5;

        var existingHours = await db.WorkingHours.Where(w => w.BarberId == barber.Id && w.DayOfWeek == dow).ToListAsync();
        db.WorkingHours.RemoveRange(existingHours);
        db.WorkingHours.Add(new WorkingHours
        {
            BarberId = barber.Id,
            DayOfWeek = dow,
            StartTime = new LocalTime(9, 0),
            EndTime = new LocalTime(18, 0)
        });
        await db.SaveChangesAsync();

        // "Now" is set to 2026-11-20 at 14:15 Asia/Manila (06:15 UTC)
        var manilaNow = date.At(new LocalTime(14, 15)).InZoneStrictly(_zone).ToInstant();
        var fixedClock = new FixedTestClock(manilaNow);
        var availabilityService = new AvailabilityService(db, fixedClock, _zone);

        var slots = await availabilityService.GetAvailableSlotsAsync(barber.Id, s30.Id, date);

        // Slots before 14:15 must be excluded! First slot should be 14:30
        slots.Should().NotContain(new LocalTime(9, 0));
        slots.Should().NotContain(new LocalTime(14, 0));
        slots.First().Should().Be(new LocalTime(14, 30));
    }

    [Fact]
    public async Task GetAvailableSlots_BackToBackAppointments_Allowed()
    {
        // Existing appointment: 10:00 - 10:30
        // Candidate 9:30 - 10:00: allowed (ends at existing start)
        // Candidate 10:00 - 10:30: NOT allowed (overlaps)
        // Candidate 10:30 - 11:00: allowed (starts at existing end)
        await EnsureSeededAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
        var (barber, s30, _, _) = await SetupAvailabilityFixturesAsync(db);

        var date = new LocalDate(2026, 11, 21); // Saturday
        int dow = 6;

        var existingHours = await db.WorkingHours.Where(w => w.BarberId == barber.Id && w.DayOfWeek == dow).ToListAsync();
        db.WorkingHours.RemoveRange(existingHours);
        db.WorkingHours.Add(new WorkingHours
        {
            BarberId = barber.Id,
            DayOfWeek = dow,
            StartTime = new LocalTime(9, 0),
            EndTime = new LocalTime(18, 0)
        });

        // Add an appointment from 10:00 to 10:30
        var appStartUtc = date.At(new LocalTime(10, 0)).InZoneStrictly(_zone).ToInstant();
        var appEndUtc = date.At(new LocalTime(10, 30)).InZoneStrictly(_zone).ToInstant();

        var customer = await db.Users.FirstAsync(u => u.Role == UserRole.Customer);
        db.Appointments.Add(new Appointment
        {
            ReferenceCode = $"BB-TEST-{Guid.NewGuid():N}"[..15],
            CustomerId = customer.Id,
            BarberId = barber.Id,
            ServiceId = s30.Id,
            StartsAt = appStartUtc,
            EndsAt = appEndUtc,
            Status = AppointmentStatus.Confirmed,
            Price = 150.00m,
            ServiceName = "Haircut"
        });
        await db.SaveChangesAsync();

        var fixedClock = new FixedTestClock(Instant.FromUtc(2026, 11, 15, 0, 0));
        var availabilityService = new AvailabilityService(db, fixedClock, _zone);

        var slots = await availabilityService.GetAvailableSlotsAsync(barber.Id, s30.Id, date);

        slots.Should().Contain(new LocalTime(9, 30), "Back-to-back appointment before must be allowed");
        slots.Should().NotContain(new LocalTime(10, 0), "Overlapping slot must be blocked");
        slots.Should().Contain(new LocalTime(10, 30), "Back-to-back appointment after must be allowed");
    }

    [Fact]
    public async Task GetAvailableSlots_CancelledAndRejected_DoNotBlock_WhilePendingConfirmedCompletedBlock()
    {
        // Cancelled and Rejected appointments should NOT block slots
        await EnsureSeededAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
        var (barber, s30, _, _) = await SetupAvailabilityFixturesAsync(db);

        var date = new LocalDate(2026, 11, 23); // Monday
        int dow = 1;

        var existingHours = await db.WorkingHours.Where(w => w.BarberId == barber.Id && w.DayOfWeek == dow).ToListAsync();
        db.WorkingHours.RemoveRange(existingHours);
        db.WorkingHours.Add(new WorkingHours
        {
            BarberId = barber.Id,
            DayOfWeek = dow,
            StartTime = new LocalTime(9, 0),
            EndTime = new LocalTime(18, 0)
        });

        var customer = await db.Users.FirstAsync(u => u.Role == UserRole.Customer);

        // Cancelled at 11:00
        db.Appointments.Add(new Appointment
        {
            ReferenceCode = $"BB-CAN-{Guid.NewGuid():N}"[..15],
            CustomerId = customer.Id,
            BarberId = barber.Id,
            ServiceId = s30.Id,
            StartsAt = date.At(new LocalTime(11, 0)).InZoneStrictly(_zone).ToInstant(),
            EndsAt = date.At(new LocalTime(11, 30)).InZoneStrictly(_zone).ToInstant(),
            Status = AppointmentStatus.Cancelled,
            Price = 150.00m,
            ServiceName = "Haircut"
        });

        // Rejected at 11:30
        db.Appointments.Add(new Appointment
        {
            ReferenceCode = $"BB-REJ-{Guid.NewGuid():N}"[..15],
            CustomerId = customer.Id,
            BarberId = barber.Id,
            ServiceId = s30.Id,
            StartsAt = date.At(new LocalTime(11, 30)).InZoneStrictly(_zone).ToInstant(),
            EndsAt = date.At(new LocalTime(12, 0)).InZoneStrictly(_zone).ToInstant(),
            Status = AppointmentStatus.Rejected,
            Price = 150.00m,
            ServiceName = "Haircut"
        });

        // Pending at 13:00 (blocks)
        db.Appointments.Add(new Appointment
        {
            ReferenceCode = $"BB-PEN-{Guid.NewGuid():N}"[..15],
            CustomerId = customer.Id,
            BarberId = barber.Id,
            ServiceId = s30.Id,
            StartsAt = date.At(new LocalTime(13, 0)).InZoneStrictly(_zone).ToInstant(),
            EndsAt = date.At(new LocalTime(13, 30)).InZoneStrictly(_zone).ToInstant(),
            Status = AppointmentStatus.Pending,
            Price = 150.00m,
            ServiceName = "Haircut"
        });

        // Completed at 14:00 (blocks)
        db.Appointments.Add(new Appointment
        {
            ReferenceCode = $"BB-COM-{Guid.NewGuid():N}"[..15],
            CustomerId = customer.Id,
            BarberId = barber.Id,
            ServiceId = s30.Id,
            StartsAt = date.At(new LocalTime(14, 0)).InZoneStrictly(_zone).ToInstant(),
            EndsAt = date.At(new LocalTime(14, 30)).InZoneStrictly(_zone).ToInstant(),
            Status = AppointmentStatus.Completed,
            Price = 150.00m,
            ServiceName = "Haircut"
        });

        await db.SaveChangesAsync();

        var fixedClock = new FixedTestClock(Instant.FromUtc(2026, 11, 15, 0, 0));
        var availabilityService = new AvailabilityService(db, fixedClock, _zone);

        var slots = await availabilityService.GetAvailableSlotsAsync(barber.Id, s30.Id, date);

        slots.Should().Contain(new LocalTime(11, 0), "Cancelled appointment must not block slot");
        slots.Should().Contain(new LocalTime(11, 30), "Rejected appointment must not block slot");
        slots.Should().NotContain(new LocalTime(13, 0), "Pending appointment must block slot");
        slots.Should().NotContain(new LocalTime(14, 0), "Completed appointment must block slot");
    }

    [Fact]
    public async Task GetAvailableSlots_MidnightEdges_HandlesShiftUntilMidnight()
    {
        // Working hours: 22:00 - 00:00 (midnight)
        await EnsureSeededAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
        var (barber, s30, _, _) = await SetupAvailabilityFixturesAsync(db);

        var date = new LocalDate(2026, 11, 24); // Tuesday
        int dow = 2;

        var existingHours = await db.WorkingHours.Where(w => w.BarberId == barber.Id && w.DayOfWeek == dow).ToListAsync();
        db.WorkingHours.RemoveRange(existingHours);
        db.WorkingHours.Add(new WorkingHours
        {
            BarberId = barber.Id,
            DayOfWeek = dow,
            StartTime = new LocalTime(22, 0),
            EndTime = new LocalTime(0, 0) // Midnight closing
        });
        await db.SaveChangesAsync();

        var fixedClock = new FixedTestClock(Instant.FromUtc(2026, 11, 15, 0, 0));
        var availabilityService = new AvailabilityService(db, fixedClock, _zone);

        var slots = await availabilityService.GetAvailableSlotsAsync(barber.Id, s30.Id, date);

        // 22:00, 22:30, 23:00, 23:30 (ends at 24:00/00:00)
        slots.Should().Equal(new List<LocalTime>
        {
            new(22, 0),
            new(22, 30),
            new(23, 0),
            new(23, 30)
        });
    }

    [Fact]
    public async Task IsSlotAvailable_ValidatesOffGrid_PastTimes_AndOverlaps()
    {
        // Table-driven verification of IsSlotAvailableAsync
        await EnsureSeededAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
        var (barber, s30, _, _) = await SetupAvailabilityFixturesAsync(db);

        var date = new LocalDate(2026, 11, 25); // Wednesday
        int dow = 3;

        var existingHours = await db.WorkingHours.Where(w => w.BarberId == barber.Id && w.DayOfWeek == dow).ToListAsync();
        db.WorkingHours.RemoveRange(existingHours);
        db.WorkingHours.Add(new WorkingHours
        {
            BarberId = barber.Id,
            DayOfWeek = dow,
            StartTime = new LocalTime(9, 0),
            EndTime = new LocalTime(18, 0)
        });
        await db.SaveChangesAsync();

        var fixedClock = new FixedTestClock(Instant.FromUtc(2026, 11, 20, 0, 0));
        var availabilityService = new AvailabilityService(db, fixedClock, _zone);

        // 1. Off-grid time (e.g. 09:15)
        var offGridUtc = date.At(new LocalTime(9, 15)).InZoneStrictly(_zone).ToInstant();
        (await availabilityService.IsSlotAvailableAsync(barber.Id, s30.Id, offGridUtc)).Should().BeFalse();

        // 2. Past time (e.g. before fixedClock)
        var pastUtc = Instant.FromUtc(2026, 11, 19, 0, 0);
        (await availabilityService.IsSlotAvailableAsync(barber.Id, s30.Id, pastUtc)).Should().BeFalse();

        // 3. Valid on-grid future slot
        var validUtc = date.At(new LocalTime(10, 0)).InZoneStrictly(_zone).ToInstant();
        (await availabilityService.IsSlotAvailableAsync(barber.Id, s30.Id, validUtc)).Should().BeTrue();
    }
}

