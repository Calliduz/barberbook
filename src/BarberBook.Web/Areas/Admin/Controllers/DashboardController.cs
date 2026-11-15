using System.Globalization;
using BarberBook.Data;
using BarberBook.Data.Entities;
using BarberBook.Data.Enums;
using BarberBook.Data.Extensions;
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
            .Where(a => a.StartsAt >= todayStartUtc && a.StartsAt < tomorrowStartUtc)
            .OrderBy(a => a.StartsAt)
            .ToListAsync();
        await _dbContext.HydrateAppointmentsAsync(todayAppointments, includeCustomer: true, includeBarber: true, includeService: false);

        var todayCounts = new TodayCountsDto
        {
            Pending = todayAppointments.Count(a => a.Status == AppointmentStatus.Pending),
            Confirmed = todayAppointments.Count(a => a.Status == AppointmentStatus.Confirmed),
            Completed = todayAppointments.Count(a => a.Status == AppointmentStatus.Completed),
            Cancelled = todayAppointments.Count(a => a.Status == AppointmentStatus.Cancelled),
            Rejected = todayAppointments.Count(a => a.Status == AppointmentStatus.Rejected),
            Total = todayAppointments.Count
        };

        // 2. Pending bookings needing action (latest first) across all dates
        var totalPendingCount = await _dbContext.Appointments
            .CountAsync(a => a.Status == AppointmentStatus.Pending);

        var pendingRaw = await _dbContext.Appointments
            .AsNoTracking()
            .Where(a => a.Status == AppointmentStatus.Pending)
            .OrderByDescending(a => a.CreatedAt)
            .Take(15)
            .ToListAsync();
        await _dbContext.HydrateAppointmentsAsync(pendingRaw, includeCustomer: true, includeBarber: true, includeService: false);

        var pendingBookings = pendingRaw.Select(a =>
        {
            var startsAtLocal = a.StartsAt.InZone(_zone);
            var createdAtLocal = a.CreatedAt.InZone(_zone);
            return new AdminDashboardPendingItemDto
            {
                Id = a.Id,
                ReferenceCode = a.ReferenceCode,
                CustomerName = a.Customer?.FullName ?? "Walk-in Guest",
                CustomerPhone = a.Customer?.PhoneNumber,
                BarberName = a.Barber?.DisplayName ?? "Staff",
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
        var activeUserIds = await _dbContext.Users.AsNoTracking().Where(u => u.IsActive).Select(u => u.Id).ToListAsync();
        var activeBarbersCount = await _dbContext.Barbers
            .CountAsync(b => b.IsActive && activeUserIds.Contains(b.UserId));

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
                        CustomerName = a.Customer?.FullName ?? "Walk-in Guest",
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
            .OrderByDescending(a => a.StartsAt)
            .Take(8)
            .ToListAsync();
        await _dbContext.HydrateAppointmentsAsync(recentApptsRaw, includeCustomer: true, includeBarber: true, includeService: false);

        var recentAppts = recentApptsRaw.Select(a =>
        {
            var startsAtLocal = a.StartsAt.InZone(_zone);
            return new AdminDashboardAppointmentItem
            {
                Id = a.Id,
                ReferenceCode = a.ReferenceCode,
                CustomerName = a.Customer?.FullName ?? "Walk-in Guest",
                BarberName = a.Barber?.DisplayName ?? "Staff",
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

        // 9. Visual Analytics & Trends: Past 7 Days Cuts vs Products Sales
        var recentDailySales = new List<AdminDashboardDailySalesPointDto>();
        for (int i = 6; i >= 0; i--)
        {
            var dayDate = today.PlusDays(-i);
            var dayReport = await _reportingService.GetDailySalesAsync(dayDate);
            recentDailySales.Add(new AdminDashboardDailySalesPointDto
            {
                DateLabel = dayDate.ToString("MMM d", CultureInfo.InvariantCulture),
                DayName = dayDate.ToString("ddd", CultureInfo.InvariantCulture),
                ServiceSales = dayReport.TotalSales,
                ProductSales = dayReport.ProductSales.Total,
                GrandTotal = dayReport.GrandTotal,
                FormattedServiceSales = dayReport.FormattedTotalSales,
                FormattedProductSales = dayReport.ProductSales.FormattedTotal,
                FormattedGrandTotal = dayReport.FormattedGrandTotal,
                CompletedCutsCount = dayReport.CompletedCount,
                ProductItemsSold = dayReport.ProductSales.PerProduct.Sum(x => x.Count)
            });
        }

        // 10. Multi-Branch Operations Overview
        var branchesRaw = await _dbContext.Branches.AsNoTracking().ToListAsync();
        var allBarbers = await _dbContext.Barbers.AsNoTracking().ToListAsync();
        var allCompletedAppts = await _dbContext.Appointments.AsNoTracking()
            .Where(a => a.Status == AppointmentStatus.Completed)
            .Select(a => new { a.Id, a.BarberId, a.BranchId })
            .ToListAsync();

        var branchesOverview = branchesRaw.Select(br =>
        {
            var bBarberIds = allBarbers.Where(b => b.BranchId == br.Id).Select(b => b.Id).ToList();
            var cutsCount = allCompletedAppts.Count(a => a.BranchId == br.Id || (a.BranchId == null && bBarberIds.Contains(a.BarberId)));
            var openTimeStr = br.OpeningTime.ToString("h:mm tt", CultureInfo.InvariantCulture);
            var closeTimeStr = br.ClosingTime.ToString("h:mm tt", CultureInfo.InvariantCulture);

            return new AdminDashboardBranchSummaryDto
            {
                Id = br.Id,
                Name = br.Name,
                Address = br.Address,
                PhoneNumber = br.PhoneNumber ?? "0917-000-0000",
                OpeningHours = $"{openTimeStr} – {closeTimeStr}",
                ActiveBarbersCount = allBarbers.Count(b => b.BranchId == br.Id && b.IsActive),
                CompletedCutsCount = cutsCount,
                IsActive = br.IsActive
            };
        }).OrderByDescending(b => b.IsActive).ThenBy(b => b.Name).ToList();

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
            TotalPendingBookingsCount = totalPendingCount,
            PendingBookings = pendingBookings,
            TodayAppointmentsByBarber = todayByBarber,
            RecentAppointments = recentAppts,
            RecentDailySales = recentDailySales,
            BranchesOverview = branchesOverview
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
            .Where(a => a.StartsAt >= todayStartUtc && a.StartsAt < tomorrowStartUtc)
            .OrderBy(a => a.StartsAt)
            .ToListAsync();
        await _dbContext.HydrateAppointmentsAsync(todayAppointments, includeCustomer: true, includeBarber: true, includeService: false);

        var todayCounts = new TodayCountsDto
        {
            Pending = todayAppointments.Count(a => a.Status == AppointmentStatus.Pending),
            Confirmed = todayAppointments.Count(a => a.Status == AppointmentStatus.Confirmed),
            Completed = todayAppointments.Count(a => a.Status == AppointmentStatus.Completed),
            Cancelled = todayAppointments.Count(a => a.Status == AppointmentStatus.Cancelled),
            Rejected = todayAppointments.Count(a => a.Status == AppointmentStatus.Rejected),
            Total = todayAppointments.Count
        };

        // 2. Pending bookings (latest first, up to 10)
        var totalPendingCount = await _dbContext.Appointments
            .CountAsync(a => a.Status == AppointmentStatus.Pending);

        var pendingRaw = await _dbContext.Appointments
            .AsNoTracking()
            .Where(a => a.Status == AppointmentStatus.Pending)
            .OrderByDescending(a => a.CreatedAt)
            .Take(10)
            .ToListAsync();
        await _dbContext.HydrateAppointmentsAsync(pendingRaw, includeCustomer: true, includeBarber: true, includeService: false);

        var pendingBookings = pendingRaw.Select(a =>
        {
            var startsAtLocal = a.StartsAt.InZone(_zone);
            var createdAtLocal = a.CreatedAt.InZone(_zone);
            return new AdminDashboardPendingItemDto
            {
                Id = a.Id,
                ReferenceCode = a.ReferenceCode,
                CustomerName = a.Customer?.FullName ?? "Walk-in Guest",
                CustomerPhone = a.Customer?.PhoneNumber,
                BarberName = a.Barber?.DisplayName ?? "Staff",
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
        var activeUserIds = await _dbContext.Users.AsNoTracking().Where(u => u.IsActive).Select(u => u.Id).ToListAsync();
        var activeBarbersCount = await _dbContext.Barbers
            .CountAsync(b => b.IsActive && activeUserIds.Contains(b.UserId));

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
                        CustomerName = a.Customer?.FullName ?? "Walk-in Guest",
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
            TotalPendingBookingsCount = totalPendingCount,
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
