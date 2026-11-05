using BarberBook.Data;
using BarberBook.Data.Entities;
using BarberBook.Data.Enums;
using BarberBook.Data.Seeding;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NodaTime;
using Npgsql;
using Xunit;

namespace BarberBook.Tests;

public class DatabaseConstraintAndSeedFeedTests : IAsyncLifetime
{
    private readonly ServiceProvider _serviceProvider;
    private readonly BarberBookDbContext _context;
    private static readonly DateTimeZone ManilaZone = DateTimeZoneProviders.Tzdb["Asia/Manila"];

    public DatabaseConstraintAndSeedFeedTests()
    {
        var services = new ServiceCollection();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=127.0.0.1;Port=5432;Database=barberbook_test;Username=barberbook;Password=barberbook_dev_password",
                ["Admin:Email"] = "admin@barberbook.local",
                ["Admin:Password"] = "Admin@BarberBook2026!",
                ["Seed:DemoPassword"] = "Demo@BarberBook2026!"
            })
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        services.AddDbContext<BarberBookDbContext>(options =>
        {
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"), o => o.UseNodaTime());
        });

        services.AddIdentity<ApplicationUser, IdentityRole<int>>()
            .AddEntityFrameworkStores<BarberBookDbContext>()
            .AddDefaultTokenProviders();

        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<BarberBookDbContext>();
    }

    public async Task InitializeAsync()
    {
        // Ensure test database is migrated and seeded
        await DatabaseSeeder.SeedAsync(_serviceProvider);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _serviceProvider.DisposeAsync();
    }

    [Fact]
    public async Task PS2_BR03_RejectsOverlappingBooking_ForSameBarber_WhenPendingConfirmedOrCompleted()
    {
        // Arrange: Find Ramon and an existing completed appointment on 2026-09-14 9:00 AM (01:00 UTC)
        var ramon = await _context.Barbers.FirstAsync(b => b.DisplayName == "Ramon");
        var customer = await _context.Users.FirstAsync(u => u.Role == UserRole.Customer && u.Email == "mark@barberbook.local");
        var service = await _context.Services.FirstAsync(s => s.Name == "Haircut");

        var startsAt = Instant.FromUtc(2026, 9, 14, 1, 15, 0); // 9:15 AM Manila (overlaps Ramon's 9:00 - 9:30 AM)
        var endsAt = startsAt.Plus(Duration.FromMinutes(30));

        var overlappingAppointment = new Appointment
        {
            ReferenceCode = "TEST-OVERLAP-001",
            BarberId = ramon.Id,
            CustomerId = customer.Id,
            ServiceId = service.Id,
            StartsAt = startsAt,
            EndsAt = endsAt,
            Status = AppointmentStatus.Pending,
            Price = service.Price,
            ServiceName = service.Name,
            CreatedAt = SystemClock.Instance.GetCurrentInstant(),
            UpdatedAt = SystemClock.Instance.GetCurrentInstant()
        };

        _context.Appointments.Add(overlappingAppointment);

        // Act & Assert: Postgres GiST exclusion constraint must throw DbUpdateException with 23P01 (exclusion_violation)
        var act = async () => await _context.SaveChangesAsync();
        var ex = await act.Should().ThrowAsync<DbUpdateException>();
        ex.Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be("23P01"); // Postgres exclusion_violation
    }

    [Fact]
    public async Task BR03_AllowsOverlappingBooking_WhenExistingIsCancelledOrRejected()
    {
        // Arrange: Find Ramon and create a Cancelled appointment
        var ramon = await _context.Barbers.FirstAsync(b => b.DisplayName == "Ramon");
        var customer = await _context.Users.FirstAsync(u => u.Role == UserRole.Customer && u.Email == "mark@barberbook.local");
        var service = await _context.Services.FirstAsync(s => s.Name == "Haircut");

        var startsAt = Instant.FromUtc(2026, 9, 14, 10, 0, 0); // 6:00 PM Manila
        var endsAt = startsAt.Plus(Duration.FromMinutes(30));

        var cancelledAppointment = new Appointment
        {
            ReferenceCode = "TEST-CANCELLED-001",
            BarberId = ramon.Id,
            CustomerId = customer.Id,
            ServiceId = service.Id,
            StartsAt = startsAt,
            EndsAt = endsAt,
            Status = AppointmentStatus.Cancelled,
            Price = service.Price,
            ServiceName = service.Name,
            CreatedAt = SystemClock.Instance.GetCurrentInstant(),
            UpdatedAt = SystemClock.Instance.GetCurrentInstant()
        };

        _context.Appointments.Add(cancelledAppointment);
        await _context.SaveChangesAsync();

        // Act: Book overlapping confirmed appointment at the exact same slot
        var newAppointment = new Appointment
        {
            ReferenceCode = "TEST-OVERLAP-ALLOWED-001",
            BarberId = ramon.Id,
            CustomerId = customer.Id,
            ServiceId = service.Id,
            StartsAt = startsAt,
            EndsAt = endsAt,
            Status = AppointmentStatus.Confirmed,
            Price = service.Price,
            ServiceName = service.Name,
            CreatedAt = SystemClock.Instance.GetCurrentInstant(),
            UpdatedAt = SystemClock.Instance.GetCurrentInstant()
        };

        _context.Appointments.Add(newAppointment);

        // Assert: Succeeds because cancelled status is excluded from the GiST constraint WHERE clause
        var saveResult = await _context.SaveChangesAsync();
        saveResult.Should().BeGreaterThan(0);

        // Cleanup
        _context.Appointments.Remove(newAppointment);
        _context.Appointments.Remove(cancelledAppointment);
        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task BR03_AllowsDifferentBarbers_ToShareSameTimeSlot()
    {
        // Arrange: Ramon and Dennis can serve different customers at the same time
        var ramon = await _context.Barbers.FirstAsync(b => b.DisplayName == "Ramon");
        var dennis = await _context.Barbers.FirstAsync(b => b.DisplayName == "Dennis");
        var customer1 = await _context.Users.FirstAsync(u => u.Role == UserRole.Customer && u.Email == "mark@barberbook.local");
        var customer2 = await _context.Users.FirstAsync(u => u.Role == UserRole.Customer && u.Email == "paolo@barberbook.local");
        var service = await _context.Services.FirstAsync(s => s.Name == "Haircut");

        var startsAt = Instant.FromUtc(2026, 9, 14, 11, 0, 0); // 7:00 PM Manila
        var endsAt = startsAt.Plus(Duration.FromMinutes(30));

        var apptRamon = new Appointment
        {
            ReferenceCode = "TEST-SAME-TIME-RAMON",
            BarberId = ramon.Id,
            CustomerId = customer1.Id,
            ServiceId = service.Id,
            StartsAt = startsAt,
            EndsAt = endsAt,
            Status = AppointmentStatus.Confirmed,
            Price = service.Price,
            ServiceName = service.Name,
            CreatedAt = SystemClock.Instance.GetCurrentInstant(),
            UpdatedAt = SystemClock.Instance.GetCurrentInstant()
        };

        var apptDennis = new Appointment
        {
            ReferenceCode = "TEST-SAME-TIME-DENNIS",
            BarberId = dennis.Id,
            CustomerId = customer2.Id,
            ServiceId = service.Id,
            StartsAt = startsAt,
            EndsAt = endsAt,
            Status = AppointmentStatus.Confirmed,
            Price = service.Price,
            ServiceName = service.Name,
            CreatedAt = SystemClock.Instance.GetCurrentInstant(),
            UpdatedAt = SystemClock.Instance.GetCurrentInstant()
        };

        _context.Appointments.AddRange(apptRamon, apptDennis);

        // Act & Assert: Must succeed without constraint violation
        var saveResult = await _context.SaveChangesAsync();
        saveResult.Should().Be(2);

        // Cleanup
        _context.Appointments.Remove(apptRamon);
        _context.Appointments.Remove(apptDennis);
        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task BR03_EnforcesCheckConstraint_EndsAtMustBeAfterStartsAt()
    {
        // Arrange: EndsAt is before StartsAt
        var ramon = await _context.Barbers.FirstAsync(b => b.DisplayName == "Ramon");
        var customer = await _context.Users.FirstAsync(u => u.Role == UserRole.Customer && u.Email == "mark@barberbook.local");
        var service = await _context.Services.FirstAsync(s => s.Name == "Haircut");

        var startsAt = Instant.FromUtc(2026, 9, 14, 12, 0, 0);
        var invalidEndsAt = startsAt.Minus(Duration.FromMinutes(30)); // 30 minutes before startsAt

        var invalidAppointment = new Appointment
        {
            ReferenceCode = "TEST-INVALID-CHECK-001",
            BarberId = ramon.Id,
            CustomerId = customer.Id,
            ServiceId = service.Id,
            StartsAt = startsAt,
            EndsAt = invalidEndsAt,
            Status = AppointmentStatus.Pending,
            Price = service.Price,
            ServiceName = service.Name,
            CreatedAt = SystemClock.Instance.GetCurrentInstant(),
            UpdatedAt = SystemClock.Instance.GetCurrentInstant()
        };

        _context.Appointments.Add(invalidAppointment);

        // Act & Assert: Check constraint ck_appointments_ends_at_after_starts_at must throw 23514
        var act = async () => await _context.SaveChangesAsync();
        var ex = await act.Should().ThrowAsync<DbUpdateException>();
        ex.Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be("23514"); // Postgres check_violation
    }

    [Fact]
    public async Task T10_SeedData_ReproducesDailySales_ForSeptember14_2026()
    {
        // Arrange: Daily sales for Sep 14, 2026 in Asia/Manila (9:00 AM - 5:00 PM)
        var startOfDayManila = new LocalDateTime(2026, 9, 14, 0, 0, 0).InZoneStrictly(ManilaZone).ToInstant();
        var endOfDayManila = new LocalDateTime(2026, 9, 14, 23, 59, 59).InZoneStrictly(ManilaZone).ToInstant();

        // Act: Filter completed appointments on 2026-09-14
        var completedAppointments = await _context.Appointments
            .Include(a => a.Barber)
            .Include(a => a.Service)
            .Where(a => a.Status == AppointmentStatus.Completed &&
                        a.StartsAt >= startOfDayManila &&
                        a.StartsAt <= endOfDayManila)
            .ToListAsync();

        var totalSales = completedAppointments.Sum(a => a.Price);
        var count = completedAppointments.Count;

        // Assert: Reference §7.3 - 6 completed appointments = ₱1,340.00
        count.Should().Be(6);
        totalSales.Should().Be(1340.00m);

        // Per-barber totals
        completedAppointments.Where(a => a.Barber.DisplayName == "Ramon").Sum(a => a.Price).Should().Be(250.00m); // 150 + 100
        completedAppointments.Where(a => a.Barber.DisplayName == "Dennis").Sum(a => a.Price).Should().Be(720.00m); // 220 + 500
        completedAppointments.Where(a => a.Barber.DisplayName == "Miguel").Sum(a => a.Price).Should().Be(370.00m); // 150 + 220

        // Per-service totals
        completedAppointments.Where(a => a.ServiceName == "Haircut").Sum(a => a.Price).Should().Be(300.00m);
        completedAppointments.Where(a => a.ServiceName == "Haircut + Shave").Sum(a => a.Price).Should().Be(440.00m);
        completedAppointments.Where(a => a.ServiceName == "Beard Trim").Sum(a => a.Price).Should().Be(100.00m);
        completedAppointments.Where(a => a.ServiceName == "Hair Color").Sum(a => a.Price).Should().Be(500.00m);
    }

    [Fact]
    public async Task T11_SeedData_ReproducesMonthlySales_ForAugustAndSeptember()
    {
        // September 2026
        var startOfSep = new LocalDateTime(2026, 9, 1, 0, 0, 0).InZoneStrictly(ManilaZone).ToInstant();
        var endOfSep = new LocalDateTime(2026, 9, 30, 23, 59, 59).InZoneStrictly(ManilaZone).ToInstant();

        var sepSales = await _context.Appointments
            .Where(a => a.Status == AppointmentStatus.Completed && a.StartsAt >= startOfSep && a.StartsAt <= endOfSep)
            .SumAsync(a => a.Price);

        sepSales.Should().Be(1340.00m);

        // August 2026
        var startOfAug = new LocalDateTime(2026, 8, 1, 0, 0, 0).InZoneStrictly(ManilaZone).ToInstant();
        var endOfAug = new LocalDateTime(2026, 8, 31, 23, 59, 59).InZoneStrictly(ManilaZone).ToInstant();

        var augSales = await _context.Appointments
            .Where(a => a.Status == AppointmentStatus.Completed && a.StartsAt >= startOfAug && a.StartsAt <= endOfAug)
            .SumAsync(a => a.Price);

        augSales.Should().Be(370.00m); // 220 (Aug 30) + 150 (Aug 16)
    }

    [Fact]
    public async Task T12_SeedData_ReproducesCustomerTransactions_ForJuanDelaCruz()
    {
        // Act: Juan Dela Cruz completed transactions
        var juan = await _context.Users.FirstAsync(u => u.Email == "juan@barberbook.local");
        var juanTransactions = await _context.Appointments
            .Where(a => a.CustomerId == juan.Id && a.Status == AppointmentStatus.Completed)
            .OrderByDescending(a => a.StartsAt)
            .ToListAsync();

        var totalSpent = juanTransactions.Sum(a => a.Price);

        // Assert: 3 transactions totaling ₱520.00 (Sep 14: 150, Aug 30: 220, Aug 16: 150)
        juanTransactions.Should().HaveCount(3);
        totalSpent.Should().Be(520.00m);
    }

    [Fact]
    public async Task T14_SeedData_NoiseAppointmentsDoNotAffectCompletedSales()
    {
        // Total appointments on Sep 14 = 8 (6 completed + 1 cancelled + 1 pending)
        var startOfDayManila = new LocalDateTime(2026, 9, 14, 0, 0, 0).InZoneStrictly(ManilaZone).ToInstant();
        var endOfDayManila = new LocalDateTime(2026, 9, 14, 23, 59, 59).InZoneStrictly(ManilaZone).ToInstant();

        var allAppointments = await _context.Appointments
            .Where(a => a.StartsAt >= startOfDayManila && a.StartsAt <= endOfDayManila)
            .ToListAsync();

        allAppointments.Should().HaveCount(8);

        // But completed sales strictly equals ₱1,340.00 across 6 appointments
        var completedAppointments = allAppointments.Where(a => a.Status == AppointmentStatus.Completed).ToList();
        completedAppointments.Should().HaveCount(6);
        completedAppointments.Sum(a => a.Price).Should().Be(1340.00m);
    }
}

