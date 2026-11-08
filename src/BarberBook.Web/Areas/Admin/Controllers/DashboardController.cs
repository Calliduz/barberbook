using System.Globalization;
using BarberBook.Data;
using BarberBook.Data.Enums;
using BarberBook.Web.Common;
using BarberBook.Web.Models.Admin;
using BarberBook.Web.Services;
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
    private readonly IReportingService _reportingService;
    private readonly IAppointmentTransitionService _transitionService;
    private readonly IEquipmentService _equipmentService;
    private readonly IInventoryService _inventoryService;
    private readonly IClock _clock;
    private readonly DateTimeZone _zone;

    public DashboardController(
        BarberBookDbContext dbContext,
        IReportingService reportingService,
        IAppointmentTransitionService transitionService,
        IEquipmentService equipmentService,
        IInventoryService inventoryService,
        IClock clock,
        DateTimeZone? zone = null)
    {
        _dbContext = dbContext;
        _reportingService = reportingService;
        _transitionService = transitionService;
        _equipmentService = equipmentService;
        _inventoryService = inventoryService;
        _clock = clock;
        _zone = zone ?? DateTimeZoneProviders.Tzdb["Asia/Manila"];
    }

    /// <summary>
    /// Admin landing page /admin:
    /// Shows summary cards, "Needs action" pending list with inline Confirm/Reject,
    /// today's schedule grouped by barber, quick links, and Alerts area placeholder (P13, AF-1, OB-4).
    /// </summary>
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var localNow = _clock.GetCurrentInstant().InZone(_zone);
        var today = localNow.Date;

        var todayStartUtc = today.AtStartOfDayInZone(_zone).ToInstant();
        var tomorrowStartUtc = today.PlusDays(1).AtStartOfDayInZone(_zone).ToInstant();

        // 1. Today's appointments query
        var todayAppointments = await _dbContext.Appointments
            .AsNoTracking()
            .Include(a => a.Customer)
            .Include(a => a.Barber)
            .Where(a => a.StartsAt >= todayStartUtc && a.StartsAt < tomorrowStartUtc)
            .OrderBy(a => a.StartsAt)
            .ToListAsync();

        var todayCounts = new TodayCountsDto
        {
            Pending = todayAppointments.Count(a => a.Status == AppointmentStatus.Pending),
            Confirmed = todayAppointments.Count(a => a.Status == AppointmentStatus.Confirmed),
            Completed = todayAppointments.Count(a => a.Status == AppointmentStatus.Completed),
            Cancelled = todayAppointments.Count(a => a.Status == AppointmentStatus.Cancelled),
            Rejected = todayAppointments.Count(a => a.Status == AppointmentStatus.Rejected),
            Total = todayAppointments.Count
        };

        // 2. Pending bookings needing action (oldest first, up to 10) across all dates
        var pendingRaw = await _dbContext.Appointments
            .AsNoTracking()
            .Include(a => a.Customer)
            .Include(a => a.Barber)
            .Where(a => a.Status == AppointmentStatus.Pending)
            .OrderBy(a => a.CreatedAt)
            .Take(10)
            .ToListAsync();

        var pendingBookings = pendingRaw.Select(a =>
        {
            var startsAtLocal = a.StartsAt.InZone(_zone);
            var createdAtLocal = a.CreatedAt.InZone(_zone);
            return new AdminDashboardPendingItemDto
            {
                Id = a.Id,
                ReferenceCode = a.ReferenceCode,
                CustomerName = a.Customer.FullName,
                CustomerPhone = a.Customer.PhoneNumber,
                BarberName = a.Barber.DisplayName,
                ServiceName = a.ServiceName,
                Price = a.Price,
                FormattedPrice = FormattingHelpers.FormatPeso(a.Price),
                ScheduledDateFormatted = startsAtLocal.ToString("MMM d, yyyy", CultureInfo.InvariantCulture),
                ScheduledTime = startsAtLocal.ToString("h:mm tt", CultureInfo.InvariantCulture),
                CreatedAtFormatted = createdAtLocal.ToString("MMM d, yyyy · h:mm tt", CultureInfo.InvariantCulture),
                Notes = a.Notes
            };
        }).ToList();

        // 3. Financial figures strictly from IReportingService (BR-07, BR-11, RP-1, RP-2)
        var dailyReport = await _reportingService.GetDailySalesAsync(today);
        var monthlyReport = await _reportingService.GetMonthlySalesAsync(today.Year, today.Month);

        // 4. Entity counts
        var activeBarbersCount = await _dbContext.Barbers
            .CountAsync(b => b.IsActive && b.User.IsActive);

        var totalCustomersCount = await _dbContext.Users
            .CountAsync(u => u.Role == UserRole.Customer);

        // 5. Today's appointments grouped by barber
        var barbersRaw = await _dbContext.Barbers
            .AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.DisplayName)
            .ToListAsync();

        var todayByBarber = barbersRaw.Select(b =>
        {
            var bAppts = todayAppointments
                .Where(a => a.BarberId == b.Id)
                .Select(a =>
                {
                    var startsAtLocal = a.StartsAt.InZone(_zone);
                    return new AdminDashboardScheduleItemDto
                    {
                        Id = a.Id,
                        ReferenceCode = a.ReferenceCode,
                        TimeFormatted = startsAtLocal.ToString("h:mm tt", CultureInfo.InvariantCulture),
                        CustomerName = a.Customer.FullName,
                        ServiceName = a.ServiceName,
                        Status = a.Status,
                        StatusString = a.Status.ToString(),
                        Price = a.Price,
                        FormattedPrice = FormattingHelpers.FormatPeso(a.Price)
                    };
                }).ToList();

            return new AdminDashboardBarberScheduleDto
            {
                BarberId = b.Id,
                BarberName = b.DisplayName,
                Specialty = b.Specialties ?? "Master Craftsman",
                Appointments = bAppts
            };
        }).ToList();

        // 6. Recent appointments live feed
        var recentApptsRaw = await _dbContext.Appointments
            .AsNoTracking()
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
                StartsAtFormatted = startsAtLocal.ToString("MMM d, yyyy · h:mm tt", CultureInfo.InvariantCulture),
                Status = a.Status
            };
        }).ToList();

        // 7. Equipment monitoring alerts (P16, HW-3, INV-02)
        var monitoring = await _equipmentService.GetEquipmentMonitoringAsync();
        var flaggedAlerts = monitoring.PoorCondition
            .Concat(monitoring.UnderMaintenanceOrOutOfService)
            .Concat(monitoring.OverdueForInspection)
            .GroupBy(i => i.Id)
            .Select(g => g.First())
            .ToList();

        // 8. Low-stock products alert (HW-1, INV-05)
        var products = await _inventoryService.GetProductsAsync(includeInactive: false);
        var lowStock = products.Where(p => p.IsLowStock).ToList();

        // 9. Chart.js Analytics: 7-day revenue trend & 30-day booking breakdown
        var revDates = new List<string>();
        var revServices = new List<decimal>();
        var revProducts = new List<decimal>();
        for (int i = 6; i >= 0; i--)
        {
            var day = today.PlusDays(-i);
            revDates.Add(day.ToString("MMM d", CultureInfo.InvariantCulture));
            var dayReport = await _reportingService.GetDailySalesAsync(day);
            revServices.Add(dayReport.ServiceSales);
            revProducts.Add(dayReport.ProductSales.Total);
        }

        var thirtyDaysAgoUtc = today.PlusDays(-30).AtStartOfDayInZone(_zone).ToInstant();
        var recent30Appts = await _dbContext.Appointments
            .AsNoTracking()
            .Where(a => a.StartsAt >= thirtyDaysAgoUtc)
            .Select(a => a.Status)
            .ToListAsync();

        var chartConfirmed = recent30Appts.Count(s => s == AppointmentStatus.Confirmed);
        var chartCompleted = recent30Appts.Count(s => s == AppointmentStatus.Completed);
        var chartPending = recent30Appts.Count(s => s == AppointmentStatus.Pending);
        var chartCancelled = recent30Appts.Count(s => s == AppointmentStatus.Cancelled);
        var chartNoShow = recent30Appts.Count(s => s == AppointmentStatus.NoShow || s == AppointmentStatus.Rejected);

        var model = new AdminDashboardViewModel
        {
            TodayCounts = todayCounts,
            ActiveBarbersCount = activeBarbersCount,
            TotalCustomersCount = totalCustomersCount,
            FlaggedEquipmentCount = monitoring.TotalFlaggedCount,
            FlaggedEquipmentAlerts = flaggedAlerts,
            TodayCompletedSales = dailyReport.TotalSales,
            FormattedTodayCompletedSales = dailyReport.FormattedTotalSales,
            MonthToDateCompletedSales = monthlyReport.TotalSales,
            FormattedMonthToDateCompletedSales = monthlyReport.FormattedTotalSales,
            TodayProductSales = dailyReport.ProductSales.Total,
            FormattedTodayProductSales = dailyReport.ProductSales.FormattedTotal,
            MonthToDateProductSales = monthlyReport.ProductSales,
            FormattedMonthToDateProductSales = monthlyReport.FormattedProductSales,
            TodayGrandTotal = dailyReport.GrandTotal,
            FormattedTodayGrandTotal = dailyReport.FormattedGrandTotal,
            MonthToDateGrandTotal = monthlyReport.GrandTotal,
            FormattedMonthToDateGrandTotal = monthlyReport.FormattedGrandTotal,
            LowStockProductsCount = lowStock.Count,
            LowStockProducts = lowStock,
            PendingBookings = pendingBookings,
            TodayAppointmentsByBarber = todayByBarber,
            RecentAppointments = recentAppts,
            RevenueChartDates = revDates,
            RevenueChartServices = revServices,
            RevenueChartProducts = revProducts,
            ChartConfirmedCount = chartConfirmed,
            ChartCompletedCount = chartCompleted,
            ChartPendingCount = chartPending,
            ChartCancelledCount = chartCancelled,
            ChartNoShowCount = chartNoShow
        };

        return View(model);
    }

    /// <summary>
    /// GET /api/admin/dashboard:
    /// Returns in one call: today's appointment counts by status, pending bookings (oldest first, up to 10),
    /// today's service sales, this month's service sales, and today's appointments grouped by barber (P13).
    /// </summary>
    [HttpGet("/api/admin/dashboard")]
    public async Task<IActionResult> ApiDashboard()
    {
        var localNow = _clock.GetCurrentInstant().InZone(_zone);
        var today = localNow.Date;

        var todayStartUtc = today.AtStartOfDayInZone(_zone).ToInstant();
        var tomorrowStartUtc = today.PlusDays(1).AtStartOfDayInZone(_zone).ToInstant();

        // 1. Today's appointments query
        var todayAppointments = await _dbContext.Appointments
            .AsNoTracking()
            .Include(a => a.Customer)
            .Include(a => a.Barber)
            .Where(a => a.StartsAt >= todayStartUtc && a.StartsAt < tomorrowStartUtc)
            .OrderBy(a => a.StartsAt)
            .ToListAsync();

        var todayCounts = new TodayCountsDto
        {
            Pending = todayAppointments.Count(a => a.Status == AppointmentStatus.Pending),
            Confirmed = todayAppointments.Count(a => a.Status == AppointmentStatus.Confirmed),
            Completed = todayAppointments.Count(a => a.Status == AppointmentStatus.Completed),
            Cancelled = todayAppointments.Count(a => a.Status == AppointmentStatus.Cancelled),
            Rejected = todayAppointments.Count(a => a.Status == AppointmentStatus.Rejected),
            Total = todayAppointments.Count
        };

        // 2. Pending bookings (oldest first, up to 10)
        var pendingRaw = await _dbContext.Appointments
            .AsNoTracking()
            .Include(a => a.Customer)
            .Include(a => a.Barber)
            .Where(a => a.Status == AppointmentStatus.Pending)
            .OrderBy(a => a.CreatedAt)
            .Take(10)
            .ToListAsync();

        var pendingBookings = pendingRaw.Select(a =>
        {
            var startsAtLocal = a.StartsAt.InZone(_zone);
            var createdAtLocal = a.CreatedAt.InZone(_zone);
            return new AdminDashboardPendingItemDto
            {
                Id = a.Id,
                ReferenceCode = a.ReferenceCode,
                CustomerName = a.Customer.FullName,
                CustomerPhone = a.Customer.PhoneNumber,
                BarberName = a.Barber.DisplayName,
                ServiceName = a.ServiceName,
                Price = a.Price,
                FormattedPrice = FormattingHelpers.FormatPeso(a.Price),
                ScheduledDateFormatted = startsAtLocal.ToString("MMM d, yyyy", CultureInfo.InvariantCulture),
                ScheduledTime = startsAtLocal.ToString("h:mm tt", CultureInfo.InvariantCulture),
                CreatedAtFormatted = createdAtLocal.ToString("MMM d, yyyy · h:mm tt", CultureInfo.InvariantCulture),
                Notes = a.Notes
            };
        }).ToList();

        // 3. Financial figures via IReportingService
        var dailyReport = await _reportingService.GetDailySalesAsync(today);
        var monthlyReport = await _reportingService.GetMonthlySalesAsync(today.Year, today.Month);

        // 4. Entity counts
        var activeBarbersCount = await _dbContext.Barbers
            .CountAsync(b => b.IsActive && b.User.IsActive);

        var totalCustomersCount = await _dbContext.Users
            .CountAsync(u => u.Role == UserRole.Customer);

        // 5. Today's appointments grouped by barber
        var barbersRaw = await _dbContext.Barbers
            .AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.DisplayName)
            .ToListAsync();

        var todayByBarber = barbersRaw.Select(b =>
        {
            var bAppts = todayAppointments
                .Where(a => a.BarberId == b.Id)
                .Select(a =>
                {
                    var startsAtLocal = a.StartsAt.InZone(_zone);
                    return new AdminDashboardScheduleItemDto
                    {
                        Id = a.Id,
                        ReferenceCode = a.ReferenceCode,
                        TimeFormatted = startsAtLocal.ToString("h:mm tt", CultureInfo.InvariantCulture),
                        CustomerName = a.Customer.FullName,
                        ServiceName = a.ServiceName,
                        Status = a.Status,
                        StatusString = a.Status.ToString(),
                        Price = a.Price,
                        FormattedPrice = FormattingHelpers.FormatPeso(a.Price)
                    };
                }).ToList();

            return new AdminDashboardBarberScheduleDto
            {
                BarberId = b.Id,
                BarberName = b.DisplayName,
                Specialty = b.Specialties ?? "Master Craftsman",
                Appointments = bAppts
            };
        }).ToList();

        // Equipment monitoring alerts (P16, HW-3, INV-02)
        var monitoring = await _equipmentService.GetEquipmentMonitoringAsync();
        var flaggedAlerts = monitoring.PoorCondition
            .Concat(monitoring.UnderMaintenanceOrOutOfService)
            .Concat(monitoring.OverdueForInspection)
            .GroupBy(i => i.Id)
            .Select(g => g.First())
            .ToList();

        // Low-stock products alert (HW-1, INV-05)
        var products = await _inventoryService.GetProductsAsync(includeInactive: false);
        var lowStock = products.Where(p => p.IsLowStock).ToList();

        var dto = new AdminDashboardDto
        {
            TodayCounts = todayCounts,
            PendingBookings = pendingBookings,
            TodayServiceSales = dailyReport.TotalSales,
            FormattedTodayServiceSales = dailyReport.FormattedTotalSales,
            ThisMonthServiceSales = monthlyReport.TotalSales,
            FormattedThisMonthServiceSales = monthlyReport.FormattedTotalSales,
            TodayProductSales = dailyReport.ProductSales.Total,
            FormattedTodayProductSales = dailyReport.ProductSales.FormattedTotal,
            ThisMonthProductSales = monthlyReport.ProductSales,
            FormattedThisMonthProductSales = monthlyReport.FormattedProductSales,
            TodayGrandTotal = dailyReport.GrandTotal,
            FormattedTodayGrandTotal = dailyReport.FormattedGrandTotal,
            ThisMonthGrandTotal = monthlyReport.GrandTotal,
            FormattedThisMonthGrandTotal = monthlyReport.FormattedGrandTotal,
            TodayAppointmentsByBarber = todayByBarber,
            ActiveBarbersCount = activeBarbersCount,
            TotalCustomersCount = totalCustomersCount,
            FlaggedEquipmentCount = monitoring.TotalFlaggedCount,
            FlaggedEquipmentAlerts = flaggedAlerts,
            LowStockProductsCount = lowStock.Count,
            LowStockProducts = lowStock
        };

        return Ok(dto);
    }

    /// <summary>
    /// Inline Confirm and Reject action from Admin Dashboard
    /// </summary>
    [HttpPost("appointments/{id:int}/status")]
    public async Task<IActionResult> InlineStatusAction(
        int id,
        [FromForm] string to,
        [FromForm] string? note = null)
    {
        if (!Enum.TryParse<AppointmentStatus>(to, true, out var targetStatus))
        {
            TempData["ErrorMessage"] = $"Invalid status '{to}'.";
            return RedirectToAction(nameof(Index));
        }

        var adminUserId = User.GetUserId() ?? 1;
        var actor = new TransitionActor(adminUserId, "Admin", CustomerId: null, BarberId: null);

        var result = await _transitionService.TransitionAsync(id, targetStatus, actor, note);

        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage ?? "Status update failed.";
        }
        else
        {
            TempData["SuccessMessage"] = $"Appointment #{id} marked as {targetStatus.ToString().ToLowerInvariant()} successfully.";
        }

        if (Request.Headers.Accept.ToString().Contains("application/json"))
        {
            if (!result.Success)
            {
                return StatusCode(result.StatusCode, new ApiErrorResponse(result.ErrorCode!, result.ErrorMessage!));
            }
            return Ok(new { success = true, appointmentId = id, status = targetStatus.ToString().ToLowerInvariant() });
        }

        return RedirectToAction(nameof(Index));
    }
}
