using System.Net;
using System.Net.Http.Json;
using BarberBook.Data;
using BarberBook.Data.Entities;
using BarberBook.Data.Enums;
using BarberBook.Data.Seeding;
using BarberBook.Web.Models.Auth;
using BarberBook.Web.Models.Reports;
using BarberBook.Web.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Xunit;

namespace BarberBook.Tests;

public class ReportingServiceAndReportsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly DateTimeZone _zone = DateTimeZoneProviders.Tzdb["Asia/Manila"];
    private const string AdminEmail = "admin@barberbook.local";
    private const string AdminPassword = "Admin@BarberBook2026!";
    private const string RamonEmail = "ramon@barberbook.local";
    private const string DennisEmail = "dennis@barberbook.local";
    private const string DemoPassword = "Demo@BarberBook2026!";

    public ReportingServiceAndReportsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private async Task EnsureSeededAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await DatabaseSeeder.SeedAsync(scope.ServiceProvider);

        var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
        var testNoise = await db.Appointments
            .Where(a => a.ReferenceCode.StartsWith("ADMIN-") ||
                        a.ReferenceCode.StartsWith("TRANS-") ||
                        a.ReferenceCode.StartsWith("BB-RAMON-") ||
                        a.ReferenceCode.StartsWith("BB-DENNIS-") ||
                        a.ReferenceCode.StartsWith("BB-TEST-"))
            .ToListAsync();
        if (testNoise.Count > 0)
        {
            db.Appointments.RemoveRange(testNoise);
            await db.SaveChangesAsync();
        }
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
    public async Task T10_RP01_GetDailySales_20260914_MatchesSampleDataExactly()
    {
        // Covers T-10, RP-1, OB-8, BR-07, BR-08, BR-11
        await EnsureSeededAsync();
        using var scope = _factory.Services.CreateScope();
        var reportingService = scope.ServiceProvider.GetRequiredService<IReportingService>();

        var date = new LocalDate(2026, 9, 14);
        var report = await reportingService.GetDailySalesAsync(date);

        // 1. Total sales & count
        report.CompletedCount.Should().Be(6);
        report.TotalSales.Should().Be(1340.00m);
        report.FormattedTotalSales.Should().Be("₱1,340.00");

        // 2. Per barber breakdown
        // Ramon: ₱250 (2), Dennis: ₱720 (2), Miguel: ₱370 (2)
        report.ByBarber.Should().HaveCount(3);
        var ramon = report.ByBarber.First(b => b.Name == "Ramon");
        ramon.Count.Should().Be(2);
        ramon.TotalSales.Should().Be(250.00m);

        var dennis = report.ByBarber.First(b => b.Name == "Dennis");
        dennis.Count.Should().Be(2);
        dennis.TotalSales.Should().Be(720.00m);

        var miguel = report.ByBarber.First(b => b.Name == "Miguel");
        miguel.Count.Should().Be(2);
        miguel.TotalSales.Should().Be(370.00m);

        // 3. Per service breakdown
        // Haircut ₱300 (2), Haircut + Shave ₱440 (2), Beard Trim ₱100 (1), Hair Color ₱500 (1)
        var haircut = report.ByService.First(s => s.Name == "Haircut");
        haircut.Count.Should().Be(2);
        haircut.TotalSales.Should().Be(300.00m);

        var haircutShave = report.ByService.First(s => s.Name == "Haircut + Shave");
        haircutShave.Count.Should().Be(2);
        haircutShave.TotalSales.Should().Be(440.00m);

        var beardTrim = report.ByService.First(s => s.Name == "Beard Trim");
        beardTrim.Count.Should().Be(1);
        beardTrim.TotalSales.Should().Be(100.00m);

        var hairColor = report.ByService.First(s => s.Name == "Hair Color");
        hairColor.Count.Should().Be(1);
        hairColor.TotalSales.Should().Be(500.00m);

        // 4. Line items matching the paper's sample layout
        report.Items.Should().HaveCount(6);
        report.Items[0].Customer.Should().Be("Juan Dela Cruz");
        report.Items[0].Barber.Should().Be("Ramon");
        report.Items[0].Service.Should().Be("Haircut");
        report.Items[0].Amount.Should().Be(150.00m);

        report.Items[1].Customer.Should().Be("Mark Santos");
        report.Items[1].Barber.Should().Be("Dennis");
        report.Items[1].Service.Should().Be("Haircut + Shave");
        report.Items[1].Amount.Should().Be(220.00m);

        report.Items[2].Customer.Should().Be("Paolo Reyes");
        report.Items[2].Barber.Should().Be("Ramon");
        report.Items[2].Service.Should().Be("Beard Trim");
        report.Items[2].Amount.Should().Be(100.00m);

        report.Items[3].Customer.Should().Be("Carlo Mendoza");
        report.Items[3].Barber.Should().Be("Miguel");
        report.Items[3].Service.Should().Be("Haircut");
        report.Items[3].Amount.Should().Be(150.00m);

        report.Items[4].Customer.Should().Be("Adrian Cruz");
        report.Items[4].Barber.Should().Be("Dennis");
        report.Items[4].Service.Should().Be("Hair Color");
        report.Items[4].Amount.Should().Be(500.00m);

        report.Items[5].Customer.Should().Be("Leo Garcia");
        report.Items[5].Barber.Should().Be("Miguel");
        report.Items[5].Service.Should().Be("Haircut + Shave");
        report.Items[5].Amount.Should().Be(220.00m);

        // Also test API endpoint
        var adminClient = await CreateAuthenticatedClientAsync(AdminEmail, AdminPassword);
        var apiResp = await adminClient.GetAsync("/api/admin/reports/daily?date=2026-09-14");
        apiResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var apiData = await apiResp.Content.ReadFromJsonAsync<DailySalesReportDto>();
        apiData.Should().NotBeNull();
        apiData!.TotalSales.Should().Be(1340.00m);
        apiData.CompletedCount.Should().Be(6);
    }

    [Fact]
    public async Task T11_RP02_GetMonthlySales_SepAndAug2026_SumMatchesDays()
    {
        // Covers T-11, RP-2, BR-11
        await EnsureSeededAsync();
        using var scope = _factory.Services.CreateScope();
        var reportingService = scope.ServiceProvider.GetRequiredService<IReportingService>();

        // September 2026: ₱1,340.00 (6 appointments)
        var sepReport = await reportingService.GetMonthlySalesAsync(2026, 9);
        sepReport.TotalSales.Should().Be(1340.00m);
        sepReport.CompletedCount.Should().Be(6);
        sepReport.DailyTotals.Should().HaveCount(30); // 30 days in Sep, zero days included
        sepReport.DailyTotals.Sum(d => d.TotalSales).Should().Be(sepReport.TotalSales);
        sepReport.DailyTotals.Sum(d => d.CompletedCount).Should().Be(sepReport.CompletedCount);

        var sep14Day = sepReport.DailyTotals.First(d => d.Day == 14);
        sep14Day.TotalSales.Should().Be(1340.00m);
        sep14Day.CompletedCount.Should().Be(6);

        // August 2026: ₱370.00 (2 appointments for Juan Dela Cruz)
        var augReport = await reportingService.GetMonthlySalesAsync(2026, 8);
        augReport.TotalSales.Should().Be(370.00m);
        augReport.CompletedCount.Should().Be(2);
        augReport.DailyTotals.Should().HaveCount(31); // 31 days in Aug
        augReport.DailyTotals.Sum(d => d.TotalSales).Should().Be(augReport.TotalSales);
        augReport.DailyTotals.Sum(d => d.CompletedCount).Should().Be(augReport.CompletedCount);
    }

    [Fact]
    public async Task T11_BR11_TimeZoneBoundary_Sep30AndOct1_FallIntoDifferentMonths()
    {
        // Covers T-11: Completed appointments at 11:30 PM on Sep 30 and 12:30 AM on Oct 1 (Asia/Manila)
        // fall into different months.
        await EnsureSeededAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
        var reportingService = scope.ServiceProvider.GetRequiredService<IReportingService>();

        var testRefA = "BB-20280930-TZ1";
        var testRefB = "BB-20281001-TZ2";

        // Remove if previously created
        var old = await db.Appointments.Where(a => a.ReferenceCode == testRefA || a.ReferenceCode == testRefB).ToListAsync();
        db.Appointments.RemoveRange(old);
        await db.SaveChangesAsync();

        var barber = await db.Barbers.FirstAsync(b => b.DisplayName == "Ramon");
        var customer = await db.Users.FirstAsync(u => u.Role == UserRole.Customer);
        var service = await db.Services.FirstAsync();

        // 11:30 PM Sep 30, 2028 (Asia/Manila)
        var ldtA = new LocalDateTime(2028, 9, 30, 23, 30);
        var startA = ldtA.InZoneStrictly(_zone).ToInstant();

        // 12:30 AM Oct 1, 2028 (Asia/Manila)
        var ldtB = new LocalDateTime(2028, 10, 1, 0, 30);
        var startB = ldtB.InZoneStrictly(_zone).ToInstant();

        db.Appointments.Add(new Appointment
        {
            ReferenceCode = testRefA,
            CustomerId = customer.Id,
            BarberId = barber.Id,
            ServiceId = service.Id,
            StartsAt = startA,
            EndsAt = startA.Plus(Duration.FromMinutes(30)),
            Status = AppointmentStatus.Completed,
            Price = 150m,
            ServiceName = "Haircut",
            CreatedAt = startA.Minus(Duration.FromDays(1)),
            UpdatedAt = startA
        });

        db.Appointments.Add(new Appointment
        {
            ReferenceCode = testRefB,
            CustomerId = customer.Id,
            BarberId = barber.Id,
            ServiceId = service.Id,
            StartsAt = startB,
            EndsAt = startB.Plus(Duration.FromMinutes(30)),
            Status = AppointmentStatus.Completed,
            Price = 200m,
            ServiceName = "Shave",
            CreatedAt = startB.Minus(Duration.FromDays(1)),
            UpdatedAt = startB
        });

        await db.SaveChangesAsync();

        // Verify Sep 2028 includes Appt A (₱150) and excludes Appt B
        var sep2028 = await reportingService.GetMonthlySalesAsync(2028, 9);
        var sepDay30 = sep2028.DailyTotals.First(d => d.Day == 30);
        sepDay30.TotalSales.Should().Be(150m);
        sep2028.TotalSales.Should().Be(150m);

        // Verify Oct 2028 includes Appt B (₱200) and excludes Appt A
        var oct2028 = await reportingService.GetMonthlySalesAsync(2028, 10);
        var octDay1 = oct2028.DailyTotals.First(d => d.Day == 1);
        octDay1.TotalSales.Should().Be(200m);
        oct2028.TotalSales.Should().Be(200m);
    }

    [Fact]
    public async Task T12_RP03_CustomerTransactions_JuanDelaCruz_Returns3RowsTotal520()
    {
        // Covers T-12, RP-3, BR-12
        await EnsureSeededAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
        var reportingService = scope.ServiceProvider.GetRequiredService<IReportingService>();

        var juan = await db.Users.FirstAsync(u => u.Email == "juan@barberbook.local");

        var report = await reportingService.GetCustomerTransactionsAsync(juan.Id);

        // 3 rows: Sep 14, Aug 30, Aug 16
        report.CompletedCount.Should().Be(3);
        report.TotalSpent.Should().Be(520.00m);
        report.FormattedTotalSpent.Should().Be("₱520.00");

        // Newest first
        report.Transactions[0].Date.Should().Be(new LocalDate(2026, 9, 14));
        report.Transactions[0].Amount.Should().Be(150.00m);

        report.Transactions[1].Date.Should().Be(new LocalDate(2026, 8, 30));
        report.Transactions[1].Amount.Should().Be(220.00m);

        report.Transactions[2].Date.Should().Be(new LocalDate(2026, 8, 16));
        report.Transactions[2].Amount.Should().Be(150.00m);

        // Date range filter test (August 2026 only)
        var augRangeReport = await reportingService.GetCustomerTransactionsAsync(
            juan.Id,
            startDate: new LocalDate(2026, 8, 1),
            endDate: new LocalDate(2026, 8, 31));

        augRangeReport.CompletedCount.Should().Be(2);
        augRangeReport.TotalSpent.Should().Be(370.00m);
    }

    [Fact]
    public async Task T13_RP04_BF05_BarberTransactions_Ramon_OwnViewAndSecurityIsolation()
    {
        // Covers T-13, RP-4, BF-5:
        // Ramon sees own completed appointments with total sales.
        // Another barber's rows never appear in Ramon's own view.
        // A barber requesting another barber's transactions is refused.
        await EnsureSeededAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
        var ramon = await db.Barbers.FirstAsync(b => b.DisplayName == "Ramon");
        var dennis = await db.Barbers.FirstAsync(b => b.DisplayName == "Dennis");

        // 1. Ramon logs in and accesses his own transactions endpoint for 2026-09-14
        var ramonClient = await CreateAuthenticatedClientAsync(RamonEmail, DemoPassword);
        var ramonTxResp = await ramonClient.GetAsync("/api/barber/transactions?startDate=2026-09-14&endDate=2026-09-14");
        ramonTxResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var ramonTxData = await ramonTxResp.Content.ReadFromJsonAsync<BarberTransactionsReportDto>();
        ramonTxData.Should().NotBeNull();
        ramonTxData!.BarberName.Should().Be("Ramon");
        ramonTxData.CompletedCount.Should().Be(2);
        ramonTxData.TotalSales.Should().Be(250.00m);
        // Verify Dennis's clients (Mark Santos, Adrian Cruz) are not in Ramon's list
        ramonTxData.Transactions.Should().OnlyContain(t => t.Service != "Hair Color"); // Hair Color was done by Dennis!

        // 2. Ramon attempts to access Dennis's transactions via /api/reports/barbers/{dennis.Id}
        var forbiddenResp = await ramonClient.GetAsync($"/api/reports/barbers/{dennis.Id}");
        forbiddenResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 3. Admin can read both Ramon and Dennis transactions
        var adminClient = await CreateAuthenticatedClientAsync(AdminEmail, AdminPassword);
        var adminRamonResp = await adminClient.GetAsync($"/api/reports/barbers/{ramon.Id}");
        adminRamonResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var adminDennisResp = await adminClient.GetAsync($"/api/reports/barbers/{dennis.Id}");
        adminDennisResp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task T14_BR07_NoiseAppointments_DoNotCountInSales()
    {
        // Covers T-14, BR-07:
        // Cancelled, pending, confirmed and rejected rows on the same day change nothing.
        await EnsureSeededAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
        var reportingService = scope.ServiceProvider.GetRequiredService<IReportingService>();

        var testDate = new LocalDate(2026, 9, 14);

        // Initial daily sales should be 6 appointments and ₱1,340.00
        var initialReport = await reportingService.GetDailySalesAsync(testDate);
        initialReport.CompletedCount.Should().Be(6);
        initialReport.TotalSales.Should().Be(1340.00m);

        // The seed already contains 2 noise appointments on 2026-09-14:
        // 1 Cancelled (BB-20260914-0007), 1 Pending (BB-20260914-0008)
        var noiseAppointments = await db.Appointments
            .Where(a => a.ReferenceCode == "BB-20260914-0007" || a.ReferenceCode == "BB-20260914-0008")
            .ToListAsync();
        noiseAppointments.Should().HaveCount(2);

        // Add an extra Confirmed and Rejected appointment on the same day
        var barber = await db.Barbers.FirstAsync(b => b.DisplayName == "Miguel");
        var customer = await db.Users.FirstAsync(u => u.Role == UserRole.Customer);
        var service = await db.Services.FirstAsync();

        var startAt = new LocalDateTime(2026, 9, 14, 18, 0).InZoneStrictly(_zone).ToInstant();

        var refConf = "BB-20260914-NOISE-CONF";
        var refRej = "BB-20260914-NOISE-REJ";

        // Clean up if existed
        var existingNoise = await db.Appointments.Where(a => a.ReferenceCode == refConf || a.ReferenceCode == refRej).ToListAsync();
        db.Appointments.RemoveRange(existingNoise);
        await db.SaveChangesAsync();

        db.Appointments.Add(new Appointment
        {
            ReferenceCode = refConf,
            CustomerId = customer.Id,
            BarberId = barber.Id,
            ServiceId = service.Id,
            StartsAt = startAt,
            EndsAt = startAt.Plus(Duration.FromMinutes(30)),
            Status = AppointmentStatus.Confirmed,
            Price = 999m,
            ServiceName = "Confirmed Noise",
            CreatedAt = startAt,
            UpdatedAt = startAt
        });

        db.Appointments.Add(new Appointment
        {
            ReferenceCode = refRej,
            CustomerId = customer.Id,
            BarberId = barber.Id,
            ServiceId = service.Id,
            StartsAt = startAt.Plus(Duration.FromMinutes(30)),
            EndsAt = startAt.Plus(Duration.FromMinutes(60)),
            Status = AppointmentStatus.Rejected,
            Price = 888m,
            ServiceName = "Rejected Noise",
            CreatedAt = startAt,
            UpdatedAt = startAt
        });

        await db.SaveChangesAsync();

        // Check report again: totals MUST NOT CHANGE (BR-07)
        var afterReport = await reportingService.GetDailySalesAsync(testDate);
        afterReport.CompletedCount.Should().Be(6);
        afterReport.TotalSales.Should().Be(1340.00m);
        afterReport.FormattedTotalSales.Should().Be("₱1,340.00");
    }

    [Fact]
    public async Task AdminReports_CustomerCannotAccessDailyOrMonthlyReports()
    {
        await EnsureSeededAsync();
        var customerClient = await CreateAuthenticatedClientAsync("juan@barberbook.local", DemoPassword);

        var respDaily = await customerClient.GetAsync("/api/reports/daily");
        respDaily.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var respMonthly = await customerClient.GetAsync("/api/reports/monthly");
        respMonthly.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var respPage = await customerClient.GetAsync("/admin/reports");
        // MVC redirects to AccessDenied or returns 403
        (respPage.StatusCode == HttpStatusCode.Redirect || respPage.StatusCode == HttpStatusCode.Forbidden).Should().BeTrue();
    }
}
