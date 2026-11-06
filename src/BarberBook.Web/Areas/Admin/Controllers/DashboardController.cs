using BarberBook.Data;
using BarberBook.Data.Enums;
using BarberBook.Web.Common;
using BarberBook.Web.Models.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace BarberBook.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
[Route("admin")]
public class DashboardController : Controller
{
    private readonly BarberBookDbContext _dbContext;
    private readonly IClock _clock;
    private readonly DateTimeZone _zone;

    public DashboardController(
        BarberBookDbContext dbContext,
        IClock clock,
        DateTimeZone? zone = null)
    {
        _dbContext = dbContext;
        _clock = clock;
        _zone = zone ?? DateTimeZoneProviders.Tzdb["Asia/Manila"];
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var now = _clock.GetCurrentInstant();
        var localNow = now.InZone(_zone);
        var today = localNow.Date;

        // Today range in UTC
        var todayStartUtc = today.AtStartOfDayInZone(_zone).ToInstant();
        var tomorrowStartUtc = today.PlusDays(1).AtStartOfDayInZone(_zone).ToInstant();

        // Month range in UTC
        var monthStart = new LocalDate(today.Year, today.Month, 1);
        var nextMonthStart = monthStart.PlusMonths(1);
        var monthStartUtc = monthStart.AtStartOfDayInZone(_zone).ToInstant();
        var nextMonthStartUtc = nextMonthStart.AtStartOfDayInZone(_zone).ToInstant();

        // 1. Appointments query
        var todayAppointments = await _dbContext.Appointments
            .Where(a => a.StartsAt >= todayStartUtc && a.StartsAt < tomorrowStartUtc)
            .ToListAsync();

        var pendingApprovalsCount = await _dbContext.Appointments
            .CountAsync(a => a.Status == AppointmentStatus.Pending);

        // 2. Sales calculations (Rule BR-07: Only completed appointments count in sales)
        var todayCompletedSales = todayAppointments
            .Where(a => a.Status == AppointmentStatus.Completed)
            .Sum(a => a.Price);

        var monthCompletedSales = await _dbContext.Appointments
            .Where(a => a.Status == AppointmentStatus.Completed &&
                        a.StartsAt >= monthStartUtc && a.StartsAt < nextMonthStartUtc)
            .SumAsync(a => a.Price);

        // 3. Entity counts
        var activeBarbersCount = await _dbContext.Barbers
            .CountAsync(b => b.IsActive && b.User.IsActive);

        var totalCustomersCount = await _dbContext.Users
            .CountAsync(u => u.Role == UserRole.Customer);

        // 4. Recent appointments feed (latest 8)
        var recentApptsRaw = await _dbContext.Appointments
            .Include(a => a.Customer)
            .Include(a => a.Barber)
            .OrderByDescending(a => a.StartsAt)
            .Take(8)
            .ToListAsync();

        var recentAppts = recentApptsRaw.Select(a =>
        {
            var startsAtLocal = a.StartsAt.InZone(_zone);
            return new AdminDashboardAppointmentItem
            {
                Id = a.Id,
                ReferenceCode = a.ReferenceCode,
                CustomerName = a.Customer.FullName,
                BarberName = a.Barber.DisplayName,
                ServiceName = a.ServiceName,
                Price = a.Price,
                FormattedPrice = FormattingHelpers.FormatPeso(a.Price),
                StartsAtFormatted = startsAtLocal.ToString("MMM d, yyyy · h:mm tt", null),
                Status = a.Status
            };
        }).ToList();

        // 5. Barber performance breakdown
        var barbersRaw = await _dbContext.Barbers
            .Include(b => b.Appointments)
            .Where(b => b.IsActive)
            .OrderBy(b => b.DisplayName)
            .ToListAsync();

        var barberStats = barbersRaw.Select(b =>
        {
            var completed = b.Appointments.Where(a => a.Status == AppointmentStatus.Completed).ToList();
            var rev = completed.Sum(a => a.Price);
            return new AdminDashboardBarberItem
            {
                Id = b.Id,
                DisplayName = b.DisplayName,
                Specialties = b.Specialties ?? "Master Craftsman",
                CompletedAppointmentsCount = completed.Count,
                TotalRevenueGenerated = rev,
                FormattedRevenue = FormattingHelpers.FormatPeso(rev),
                IsActive = b.IsActive
            };
        }).ToList();

        var model = new AdminDashboardViewModel
        {
            TodayAppointmentsCount = todayAppointments.Count,
            PendingApprovalsCount = pendingApprovalsCount,
            ActiveBarbersCount = activeBarbersCount,
            TotalCustomersCount = totalCustomersCount,
            TodayCompletedSales = todayCompletedSales,
            MonthToDateCompletedSales = monthCompletedSales,
            FormattedTodayCompletedSales = FormattingHelpers.FormatPeso(todayCompletedSales),
            FormattedMonthToDateCompletedSales = FormattingHelpers.FormatPeso(monthCompletedSales),
            RecentAppointments = recentAppts,
            Barbers = barberStats
        };

        return View(model);
    }
}
