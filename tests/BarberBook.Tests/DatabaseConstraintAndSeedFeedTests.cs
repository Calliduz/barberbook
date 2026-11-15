using BarberBook.Data;
using BarberBook.Data.Entities;
using BarberBook.Data.Enums;
using BarberBook.Data.Identity;
using BarberBook.Data.Seeding;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDB.EntityFrameworkCore.Extensions;
using NodaTime;
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
                ["ConnectionStrings:MongoConnection"] = "mongodb+srv://cabunocjoshlisteristuris_db_user:QtjA8IW2dDSZkdVF@cluster0.ivvillv.mongodb.net/?appName=Cluster0",
                ["ConnectionStrings:DatabaseName"] = "barberbook",
                ["Admin:Email"] = "admin@barberbook.local",
                ["Admin:Password"] = "Admin@BarberBook2026!",
                ["Seed:DemoPassword"] = "Demo@BarberBook2026!"
            })
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        var mongoUri = configuration.GetConnectionString("MongoConnection")
            ?? Environment.GetEnvironmentVariable("MONGODB_URI")
            ?? "mongodb+srv://cabunocjoshlisteristuris_db_user:QtjA8IW2dDSZkdVF@cluster0.ivvillv.mongodb.net/?appName=Cluster0";
        var client = new MongoClient(mongoUri);

        services.AddDbContext<BarberBookDbContext>(options =>
        {
            options.UseMongoDB(client, "barberbook");
        });

        services.AddIdentity<ApplicationUser, IdentityRole<int>>()
            .AddUserStore<BarberBookUserStore>()
            .AddRoleStore<RoleStore<IdentityRole<int>, BarberBookDbContext, int>>()
            .AddDefaultTokenProviders();

        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<BarberBookDbContext>();
    }

    public async Task InitializeAsync()
    {
        await DatabaseSeeder.SeedAsync(_serviceProvider);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _serviceProvider.DisposeAsync();
    }

    [Fact]
    public async Task T10_SeedData_ReproducesDailySales_ForSeptember14_2026()
    {
        // Arrange: Daily sales for Sep 14, 2026 in Asia/Manila (9:00 AM - 5:00 PM)
        var startOfDayManila = new LocalDateTime(2026, 9, 14, 0, 0, 0).InZoneStrictly(ManilaZone).ToInstant();
        var endOfDayManila = new LocalDateTime(2026, 9, 14, 23, 59, 59).InZoneStrictly(ManilaZone).ToInstant();

        var ramon = await _context.Barbers.FirstAsync(b => b.DisplayName == "Ramon");
        var dennis = await _context.Barbers.FirstAsync(b => b.DisplayName == "Dennis");
        var miguel = await _context.Barbers.FirstAsync(b => b.DisplayName == "Miguel");

        // Act: Filter completed appointments on 2026-09-14
        var completedAppointments = await _context.Appointments
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
        completedAppointments.Where(a => a.BarberId == ramon.Id).Sum(a => a.Price).Should().Be(250.00m); // 150 + 100
        completedAppointments.Where(a => a.BarberId == dennis.Id).Sum(a => a.Price).Should().Be(720.00m); // 220 + 500
        completedAppointments.Where(a => a.BarberId == miguel.Id).Sum(a => a.Price).Should().Be(370.00m); // 150 + 220

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

        var sepAppointments = await _context.Appointments
            .Where(a => a.Status == AppointmentStatus.Completed && a.StartsAt >= startOfSep && a.StartsAt <= endOfSep)
            .ToListAsync();
        var sepSales = sepAppointments.Sum(a => a.Price);

        sepSales.Should().Be(1340.00m);

        // August 2026
        var startOfAug = new LocalDateTime(2026, 8, 1, 0, 0, 0).InZoneStrictly(ManilaZone).ToInstant();
        var endOfAug = new LocalDateTime(2026, 8, 31, 23, 59, 59).InZoneStrictly(ManilaZone).ToInstant();

        var augAppointments = await _context.Appointments
            .Where(a => a.Status == AppointmentStatus.Completed && a.StartsAt >= startOfAug && a.StartsAt <= endOfAug)
            .ToListAsync();
        var augSales = augAppointments.Sum(a => a.Price);

        augSales.Should().Be(370.00m); // 220 (Aug 30) + 150 (Aug 16)
    }

    [Fact]
    public async Task T12_SeedData_ReproducesCustomerTransactions_ForJuanDelaCruz()
    {
        // Act: Juan Dela Cruz completed transactions (seeded baseline)
        var juan = await _context.Users.FirstAsync(u => u.Email == "juan@barberbook.local");
        var juanTransactions = await _context.Appointments
            .Where(a => a.CustomerId == juan.Id && a.Status == AppointmentStatus.Completed && a.ReferenceCode.StartsWith("BB-"))
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

        allAppointments.Should().HaveCountGreaterThanOrEqualTo(8);

        // But completed sales strictly equals ₱1,340.00 across 6 appointments
        var completedAppointments = allAppointments.Where(a => a.Status == AppointmentStatus.Completed).ToList();
        completedAppointments.Should().HaveCount(6);
        completedAppointments.Sum(a => a.Price).Should().Be(1340.00m);
    }
}
