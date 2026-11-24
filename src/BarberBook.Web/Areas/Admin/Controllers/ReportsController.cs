using System.Globalization;
using BarberBook.Data;
using BarberBook.Data.Enums;
using BarberBook.Web.Common;
using BarberBook.Web.Models.Reports;
using BarberBook.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using NodaTime.Text;

namespace BarberBook.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,Barber")]
public class ReportsController : Controller
{
    private readonly IReportingService _reportingService;
    private readonly BarberBookDbContext _dbContext;
    private readonly IClock _clock;
    private readonly DateTimeZone _zone;

    public ReportsController(
        IReportingService reportingService,
        BarberBookDbContext dbContext,
        IClock clock,
        DateTimeZone? zone = null)
    {
        _reportingService = reportingService;
        _dbContext = dbContext;
        _clock = clock;
        _zone = zone ?? DateTimeZoneProviders.Tzdb["Asia/Manila"];
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("admin/reports")]
    public async Task<IActionResult> Index(
        [FromQuery] string tab = "daily",
        [FromQuery] string? date = null,
        [FromQuery] string? weekDate = null,
        [FromQuery] int? year = null,
        [FromQuery] int? month = null,
        [FromQuery] int? barberId = null,
        [FromQuery] int? customerId = null,
        [FromQuery] string? startDate = null,
        [FromQuery] string? endDate = null)
    {
        var now = _clock.GetCurrentInstant();
        var localNow = now.InZone(_zone);
        var today = localNow.Date;

        // Parse date for daily view (fallback to today, or if seed day available fallback cleanly)
        LocalDate selectedDate = today;
        if (!string.IsNullOrEmpty(date) && LocalDatePattern.Iso.Parse(date).TryGetValue(today, out var parsedDate))
        {
            selectedDate = parsedDate;
        }

        // Parse week date for weekly view (fallback to selectedDate or today)
        LocalDate selectedWeekDate = selectedDate;
        if (!string.IsNullOrEmpty(weekDate) && LocalDatePattern.Iso.Parse(weekDate).TryGetValue(today, out var parsedWDate))
        {
            selectedWeekDate = parsedWDate;
        }

        // Parse year & month for monthly and yearly views
        int selectedYear = year ?? today.Year;
        int selectedMonth = month ?? today.Month;
        if (selectedMonth < 1 || selectedMonth > 12) selectedMonth = today.Month;

        // Parse optional date range filters
        LocalDate? parsedStart = null;
        if (!string.IsNullOrEmpty(startDate) && LocalDatePattern.Iso.Parse(startDate).TryGetValue(today, out var sDate))
        {
            parsedStart = sDate;
        }

        LocalDate? parsedEnd = null;
        if (!string.IsNullOrEmpty(endDate) && LocalDatePattern.Iso.Parse(endDate).TryGetValue(today, out var eDate))
        {
            parsedEnd = eDate;
        }

        // Dropdowns for Barbers and Customers
        var barbers = await _dbContext.Barbers
            .AsNoTracking()
            .OrderBy(b => b.DisplayName)
            .ToListAsync();

        var customers = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Role == UserRole.Customer)
            .OrderBy(u => u.FullName)
            .ToListAsync();

        var effectiveCustomerId = customerId ?? customers.FirstOrDefault()?.Id ?? 0;
        var effectiveBarberId = barberId ?? barbers.FirstOrDefault()?.Id ?? 0;

        // 1. Daily Sales
        var dailyReport = await _reportingService.GetDailySalesAsync(selectedDate);

        // 2. Weekly Sales
        var weeklyReport = await _reportingService.GetWeeklySalesAsync(selectedWeekDate);

        // 3. Monthly Sales
        var monthlyReport = await _reportingService.GetMonthlySalesAsync(selectedYear, selectedMonth);

        // 4. Yearly Sales
        var yearlyReport = await _reportingService.GetYearlySalesAsync(selectedYear);

        // 5. Customer Transactions
        var customerReport = effectiveCustomerId > 0
            ? await _reportingService.GetCustomerTransactionsAsync(effectiveCustomerId, parsedStart, parsedEnd)
            : new CustomerTransactionsReportDto();

        // 6. Barber Transactions
        var barberReport = effectiveBarberId > 0
            ? await _reportingService.GetBarberTransactionsAsync(effectiveBarberId, parsedStart, parsedEnd)
            : new BarberTransactionsReportDto();

        var model = new AdminReportsViewModel
        {
            ActiveTab = tab.ToLowerInvariant(),
            SelectedDateString = selectedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            SelectedDateFormatted = selectedDate.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture),
            SelectedWeekDateString = selectedWeekDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            SelectedWeekRangeFormatted = weeklyReport.WeekRangeFormatted,
            SelectedYear = selectedYear,
            SelectedMonth = selectedMonth,
            SelectedBarberId = effectiveBarberId,
            SelectedCustomerId = effectiveCustomerId,
            StartDateString = parsedStart?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            EndDateString = parsedEnd?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Barbers = barbers,
            Customers = customers,
            DailyReport = dailyReport,
            WeeklyReport = weeklyReport,
            MonthlyReport = monthlyReport,
            YearlyReport = yearlyReport,
            CustomerReport = customerReport,
            BarberReport = barberReport
        };

        return View(model);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("admin/reports/export")]
    public async Task<IActionResult> Export(
        [FromQuery] string tab = "daily",
        [FromQuery] string? date = null,
        [FromQuery] string? weekDate = null,
        [FromQuery] int? year = null,
        [FromQuery] int? month = null,
        [FromQuery] int? barberId = null,
        [FromQuery] int? customerId = null)
    {
        var localNow = _clock.GetCurrentInstant().InZone(_zone).Date;
        var sb = new System.Text.StringBuilder();

        if (tab == "weekly")
        {
            LocalDate selectedWeekDate = localNow;
            if (!string.IsNullOrEmpty(weekDate) && LocalDatePattern.Iso.Parse(weekDate).TryGetValue(localNow, out var parsedW))
            {
                selectedWeekDate = parsedW;
            }
            else if (!string.IsNullOrEmpty(date) && LocalDatePattern.Iso.Parse(date).TryGetValue(localNow, out var parsedD))
            {
                selectedWeekDate = parsedD;
            }

            var weekly = await _reportingService.GetWeeklySalesAsync(selectedWeekDate);
            sb.AppendLine("DayIndex,Date,DayOfWeek,CompletedCount,ServiceSales,ProductSales,GrandTotal");
            foreach (var item in weekly.DailyTotals)
            {
                sb.AppendLine($"{item.DayIndex},\"{item.DateFormatted}\",\"{item.DayOfWeek}\",{item.CompletedCount},{item.ServiceSales:F2},{item.ProductSales:F2},{item.GrandTotal:F2}");
            }
            sb.AppendLine($"\"TOTAL\",\"{weekly.WeekRangeFormatted}\",\"Week\",{weekly.CompletedCount},{weekly.TotalSales:F2},{weekly.ProductSales:F2},{weekly.GrandTotal:F2}");

            var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", $"weekly-sales-{weekly.WeekStartDate:yyyy-MM-dd}-to-{weekly.WeekEndDate:yyyy-MM-dd}.csv");
        }
        else if (tab == "yearly")
        {
            int selectedYear = year ?? localNow.Year;
            var yearly = await _reportingService.GetYearlySalesAsync(selectedYear);

            sb.AppendLine("Month,MonthName,CompletedCount,ServiceSales,ProductSales,GrandTotal");
            foreach (var item in yearly.MonthlyTotals)
            {
                sb.AppendLine($"{item.Month},\"{item.MonthName}\",{item.CompletedCount},{item.ServiceSales:F2},{item.ProductSales:F2},{item.GrandTotal:F2}");
            }
            sb.AppendLine($"\"TOTAL\",\"{yearly.Year}\",{yearly.CompletedCount},{yearly.TotalSales:F2},{yearly.ProductSales:F2},{yearly.GrandTotal:F2}");

            var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", $"yearly-sales-{selectedYear}.csv");
        }
        else if (tab == "monthly")
        {
            int selectedYear = year ?? localNow.Year;
            int selectedMonth = month ?? localNow.Month;
            var monthly = await _reportingService.GetMonthlySalesAsync(selectedYear, selectedMonth);

            sb.AppendLine("Date,CompletedCount,ServiceSales,ProductSales,GrandTotal");
            foreach (var item in monthly.DailyTotals)
            {
                sb.AppendLine($"\"{item.DateFormatted}\",{item.CompletedCount},{item.ServiceSales:F2},{item.ProductSales:F2},{item.GrandTotal:F2}");
            }
            sb.AppendLine($"\"TOTAL\",{monthly.CompletedCount},{monthly.TotalSales:F2},{monthly.ProductSales:F2},{monthly.GrandTotal:F2}");

            var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", $"monthly-sales-{selectedYear}-{selectedMonth:D2}.csv");
        }
        else
        {
            LocalDate selectedDate = localNow;
            if (!string.IsNullOrEmpty(date) && LocalDatePattern.Iso.Parse(date).TryGetValue(localNow, out var parsedDate))
            {
                selectedDate = parsedDate;
            }

            var daily = await _reportingService.GetDailySalesAsync(selectedDate);
            sb.AppendLine("No,Time,Customer,Barber,Service,Amount");
            foreach (var item in daily.Items)
            {
                sb.AppendLine($"{item.No},\"{item.Time}\",\"{item.Customer.Replace("\"", "\"\"")}\",\"{item.Barber.Replace("\"", "\"\"")}\",\"{item.Service.Replace("\"", "\"\"")}\",{item.Amount:F2}");
            }
            sb.AppendLine($",,,,\"Service Total\",{daily.TotalSales:F2}");
            sb.AppendLine($",,,,\"Product Total\",{daily.ProductSales.Total:F2}");
            sb.AppendLine($",,,,\"Grand Total\",{daily.GrandTotal:F2}");

            var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", $"daily-sales-{selectedDate:yyyy-MM-dd}.csv");
        }
    }

    #region REST API Endpoints

    /// <summary>
    /// RP-1: REST API endpoint for Daily Sales report
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpGet("api/admin/reports/daily")]
    [HttpGet("api/reports/daily")]
    public async Task<IActionResult> ApiDailySales([FromQuery] string? date = null)
    {
        var localNow = _clock.GetCurrentInstant().InZone(_zone).Date;
        var targetDate = localNow;
        if (!string.IsNullOrEmpty(date) && LocalDatePattern.Iso.Parse(date).TryGetValue(localNow, out var parsed))
        {
            targetDate = parsed;
        }

        var report = await _reportingService.GetDailySalesAsync(targetDate);
        return Ok(report);
    }

    /// <summary>
    /// RP-2: REST API endpoint for Monthly Sales report
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpGet("api/admin/reports/monthly")]
    [HttpGet("api/reports/monthly")]
    public async Task<IActionResult> ApiMonthlySales([FromQuery] int? year = null, [FromQuery] int? month = null)
    {
        var localNow = _clock.GetCurrentInstant().InZone(_zone).Date;
        int targetYear = year ?? localNow.Year;
        int targetMonth = month ?? localNow.Month;
        if (targetMonth < 1 || targetMonth > 12) targetMonth = localNow.Month;

        var report = await _reportingService.GetMonthlySalesAsync(targetYear, targetMonth);
        return Ok(report);
    }

    /// <summary>
    /// REST API endpoint for Weekly Sales report
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpGet("api/admin/reports/weekly")]
    [HttpGet("api/reports/weekly")]
    public async Task<IActionResult> ApiWeeklySales([FromQuery] string? date = null, [FromQuery] string? weekDate = null)
    {
        var localNow = _clock.GetCurrentInstant().InZone(_zone).Date;
        var targetDate = localNow;
        var dateParam = weekDate ?? date;
        if (!string.IsNullOrEmpty(dateParam) && LocalDatePattern.Iso.Parse(dateParam).TryGetValue(localNow, out var parsed))
        {
            targetDate = parsed;
        }

        var report = await _reportingService.GetWeeklySalesAsync(targetDate);
        return Ok(report);
    }

    /// <summary>
    /// REST API endpoint for Yearly Sales report
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpGet("api/admin/reports/yearly")]
    [HttpGet("api/reports/yearly")]
    public async Task<IActionResult> ApiYearlySales([FromQuery] int? year = null)
    {
        var localNow = _clock.GetCurrentInstant().InZone(_zone).Date;
        int targetYear = year ?? localNow.Year;

        var report = await _reportingService.GetYearlySalesAsync(targetYear);
        return Ok(report);
    }

    /// <summary>
    /// RP-3: REST API endpoint for Customer Transactions
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpGet("api/admin/reports/customers/{customerId:int}")]
    [HttpGet("api/reports/customers/{customerId:int}")]
    public async Task<IActionResult> ApiCustomerTransactions(
        int customerId,
        [FromQuery] string? startDate = null,
        [FromQuery] string? endDate = null)
    {
        var localNow = _clock.GetCurrentInstant().InZone(_zone).Date;
        LocalDate? sDate = null;
        if (!string.IsNullOrEmpty(startDate) && LocalDatePattern.Iso.Parse(startDate).TryGetValue(localNow, out var parsedStart))
        {
            sDate = parsedStart;
        }

        LocalDate? eDate = null;
        if (!string.IsNullOrEmpty(endDate) && LocalDatePattern.Iso.Parse(endDate).TryGetValue(localNow, out var parsedEnd))
        {
            eDate = parsedEnd;
        }

        var report = await _reportingService.GetCustomerTransactionsAsync(customerId, sDate, eDate);
        return Ok(report);
    }

    /// <summary>
    /// RP-4, BF-5: REST API endpoint for Barber Transactions.
    /// Admins can read any barber's transactions; barbers can read only their own.
    /// </summary>
    [Authorize(Roles = "Admin,Barber")]
    [HttpGet("api/admin/reports/barbers/{barberId:int}")]
    [HttpGet("api/reports/barbers/{barberId:int}")]
    public async Task<IActionResult> ApiBarberTransactions(
        int barberId,
        [FromQuery] string? startDate = null,
        [FromQuery] string? endDate = null)
    {
        if (User.IsInRole("Barber"))
        {
            var userId = User.GetUserId();
            var barber = await _dbContext.Barbers.FirstOrDefaultAsync(b => b.UserId == userId);
            if (barber == null || barber.Id != barberId)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ApiErrorResponse("FORBIDDEN", "A barber can read only their own transactions."));
            }
        }

        var localNow = _clock.GetCurrentInstant().InZone(_zone).Date;
        LocalDate? sDate = null;
        if (!string.IsNullOrEmpty(startDate) && LocalDatePattern.Iso.Parse(startDate).TryGetValue(localNow, out var parsedStart))
        {
            sDate = parsedStart;
        }

        LocalDate? eDate = null;
        if (!string.IsNullOrEmpty(endDate) && LocalDatePattern.Iso.Parse(endDate).TryGetValue(localNow, out var parsedEnd))
        {
            eDate = parsedEnd;
        }

        var report = await _reportingService.GetBarberTransactionsAsync(barberId, sDate, eDate);
        return Ok(report);
    }

    #endregion
}

public class AdminReportsViewModel
{
    public string ActiveTab { get; set; } = "daily";
    public string SelectedDateString { get; set; } = string.Empty;
    public string SelectedDateFormatted { get; set; } = string.Empty;
    public string SelectedWeekDateString { get; set; } = string.Empty;
    public string SelectedWeekRangeFormatted { get; set; } = string.Empty;
    public int SelectedYear { get; set; }
    public int SelectedMonth { get; set; }
    public int SelectedBarberId { get; set; }
    public int SelectedCustomerId { get; set; }
    public string? StartDateString { get; set; }
    public string? EndDateString { get; set; }

    public List<BarberBook.Data.Entities.Barber> Barbers { get; set; } = new();
    public List<BarberBook.Data.Entities.ApplicationUser> Customers { get; set; } = new();

    public DailySalesReportDto DailyReport { get; set; } = new();
    public WeeklySalesReportDto WeeklyReport { get; set; } = new();
    public MonthlySalesReportDto MonthlyReport { get; set; } = new();
    public YearlySalesReportDto YearlyReport { get; set; } = new();
    public CustomerTransactionsReportDto CustomerReport { get; set; } = new();
    public BarberTransactionsReportDto BarberReport { get; set; } = new();
}
