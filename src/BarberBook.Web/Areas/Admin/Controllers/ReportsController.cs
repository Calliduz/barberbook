using BarberBook.Data;
using BarberBook.Data.Enums;
using BarberBook.Web.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using NodaTime.Text;

namespace BarberBook.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class ReportsController : Controller
{
    private readonly BarberBookDbContext _dbContext;
    private readonly IClock _clock;
    private readonly DateTimeZone _zone;

    public ReportsController(
        BarberBookDbContext dbContext,
        IClock clock,
        DateTimeZone? zone = null)
    {
        _dbContext = dbContext;
        _clock = clock;
        _zone = zone ?? DateTimeZoneProviders.Tzdb["Asia/Manila"];
    }

    [HttpGet("admin/reports")]
    public async Task<IActionResult> Index(
        [FromQuery] string tab = "daily",
        [FromQuery] string? date = null,
        [FromQuery] int? year = null,
        [FromQuery] int? month = null,
        [FromQuery] int? barberId = null,
        [FromQuery] int? customerId = null)
    {
        var now = _clock.GetCurrentInstant();
        var localNow = now.InZone(_zone);
        var today = localNow.Date;

        // Parse requested daily date or fallback to today
        LocalDate selectedDate = today;
        if (!string.IsNullOrEmpty(date) && LocalDatePattern.Iso.Parse(date).TryGetValue(today, out var parsedDate))
        {
            selectedDate = parsedDate;
        }

        // Parse requested monthly year/month or fallback to current
        int selectedYear = year ?? today.Year;
        int selectedMonth = month ?? today.Month;
        if (selectedMonth < 1 || selectedMonth > 12) selectedMonth = today.Month;

        // Common seed/active barber and customer lists for filter dropdowns
        var barbers = await _dbContext.Barbers.OrderBy(b => b.DisplayName).ToListAsync();
        var customers = await _dbContext.Users.Where(u => u.Role == UserRole.Customer).OrderBy(u => u.FullName).ToListAsync();

        // 1. Daily Sales Query (Rule BR-07: Only completed appointments count in sales)
        var dayStartUtc = selectedDate.AtStartOfDayInZone(_zone).ToInstant();
        var nextDayStartUtc = selectedDate.PlusDays(1).AtStartOfDayInZone(_zone).ToInstant();

        var dailyAppointmentsRaw = await _dbContext.Appointments
            .Include(a => a.Customer)
            .Include(a => a.Barber)
            .Where(a => a.Status == AppointmentStatus.Completed &&
                        a.StartsAt >= dayStartUtc && a.StartsAt < nextDayStartUtc)
            .OrderBy(a => a.StartsAt)
            .ToListAsync();

        var dailyItems = dailyAppointmentsRaw.Select(a => new ReportDailyRowItem
        {
            AppointmentId = a.Id,
            ReferenceCode = a.ReferenceCode,
            TimeFormatted = a.StartsAt.InZone(_zone).ToString("h:mm tt", null),
            CustomerName = a.Customer.FullName,
            BarberName = a.Barber.DisplayName,
            ServiceName = a.ServiceName,
            Price = a.Price,
            FormattedPrice = FormattingHelpers.FormatPeso(a.Price)
        }).ToList();

        var dailyTotalSales = dailyItems.Sum(i => i.Price);

        // Daily per-barber breakdown
        var dailyBarberBreakdown = dailyAppointmentsRaw
            .GroupBy(a => a.Barber.DisplayName)
            .Select(g => new ReportGroupBreakdown
            {
                Name = g.Key,
                Count = g.Count(),
                TotalSales = g.Sum(a => a.Price),
                FormattedTotalSales = FormattingHelpers.FormatPeso(g.Sum(a => a.Price))
            }).ToList();

        // Daily per-service breakdown
        var dailyServiceBreakdown = dailyAppointmentsRaw
            .GroupBy(a => a.ServiceName)
            .Select(g => new ReportGroupBreakdown
            {
                Name = g.Key,
                Count = g.Count(),
                TotalSales = g.Sum(a => a.Price),
                FormattedTotalSales = FormattingHelpers.FormatPeso(g.Sum(a => a.Price))
            }).ToList();

        // 2. Monthly Sales Query (RP-2)
        var monthStart = new LocalDate(selectedYear, selectedMonth, 1);
        var nextMonthStart = monthStart.PlusMonths(1);
        var monthStartUtc = monthStart.AtStartOfDayInZone(_zone).ToInstant();
        var nextMonthStartUtc = nextMonthStart.AtStartOfDayInZone(_zone).ToInstant();

        var monthlyAppointmentsRaw = await _dbContext.Appointments
            .Where(a => a.Status == AppointmentStatus.Completed &&
                        a.StartsAt >= monthStartUtc && a.StartsAt < nextMonthStartUtc)
            .ToListAsync();

        // Group by day of month
        var daysInMonth = monthStart.Calendar.GetDaysInMonth(selectedYear, selectedMonth);
        var monthlyDailyRows = new List<ReportMonthDayRow>();
        for (int d = 1; d <= daysInMonth; d++)
        {
            var dayDate = new LocalDate(selectedYear, selectedMonth, d);
            var dStartUtc = dayDate.AtStartOfDayInZone(_zone).ToInstant();
            var dEndUtc = dayDate.PlusDays(1).AtStartOfDayInZone(_zone).ToInstant();

            var dayAppts = monthlyAppointmentsRaw.Where(a => a.StartsAt >= dStartUtc && a.StartsAt < dEndUtc).ToList();
            if (dayAppts.Count > 0)
            {
                var daySum = dayAppts.Sum(a => a.Price);
                monthlyDailyRows.Add(new ReportMonthDayRow
                {
                    Date = dayDate.ToString("yyyy-MM-dd", null),
                    DateFormatted = dayDate.ToString("MMM d, yyyy (ddd)", null),
                    CompletedCount = dayAppts.Count,
                    TotalSales = daySum,
                    FormattedTotalSales = FormattingHelpers.FormatPeso(daySum)
                });
            }
        }

        var monthlyTotalSales = monthlyAppointmentsRaw.Sum(a => a.Price);

        // 3. Customer Transactions Query (RP-3)
        var customerQuery = _dbContext.Appointments
            .Include(a => a.Customer)
            .Include(a => a.Barber)
            .Where(a => a.Status == AppointmentStatus.Completed);

        if (customerId.HasValue && customerId.Value > 0)
        {
            customerQuery = customerQuery.Where(a => a.CustomerId == customerId.Value);
        }

        var customerTxRaw = await customerQuery.OrderByDescending(a => a.StartsAt).Take(50).ToListAsync();
        var customerTxItems = customerTxRaw.Select(a => new ReportTransactionRow
        {
            ReferenceCode = a.ReferenceCode,
            DateFormatted = a.StartsAt.InZone(_zone).ToString("MMM d, yyyy · h:mm tt", null),
            CustomerName = a.Customer.FullName,
            BarberName = a.Barber.DisplayName,
            ServiceName = a.ServiceName,
            Price = a.Price,
            FormattedPrice = FormattingHelpers.FormatPeso(a.Price)
        }).ToList();

        // 4. Barber Transactions Query (RP-4)
        var barberQuery = _dbContext.Appointments
            .Include(a => a.Customer)
            .Include(a => a.Barber)
            .Where(a => a.Status == AppointmentStatus.Completed);

        if (barberId.HasValue && barberId.Value > 0)
        {
            barberQuery = barberQuery.Where(a => a.BarberId == barberId.Value);
        }

        var barberTxRaw = await barberQuery.OrderByDescending(a => a.StartsAt).Take(50).ToListAsync();
        var barberTxItems = barberTxRaw.Select(a => new ReportTransactionRow
        {
            ReferenceCode = a.ReferenceCode,
            DateFormatted = a.StartsAt.InZone(_zone).ToString("MMM d, yyyy · h:mm tt", null),
            CustomerName = a.Customer.FullName,
            BarberName = a.Barber.DisplayName,
            ServiceName = a.ServiceName,
            Price = a.Price,
            FormattedPrice = FormattingHelpers.FormatPeso(a.Price)
        }).ToList();

        var viewModel = new AdminReportsViewModel
        {
            ActiveTab = tab.ToLower(),
            SelectedDateString = selectedDate.ToString("yyyy-MM-dd", null),
            SelectedDateFormatted = selectedDate.ToString("MMMM d, yyyy", null),
            SelectedYear = selectedYear,
            SelectedMonth = selectedMonth,
            SelectedBarberId = barberId,
            SelectedCustomerId = customerId,
            Barbers = barbers,
            Customers = customers,

            // Daily Sales
            DailyRows = dailyItems,
            DailyTotalCount = dailyItems.Count,
            DailyTotalSales = dailyTotalSales,
            FormattedDailyTotalSales = FormattingHelpers.FormatPeso(dailyTotalSales),
            DailyBarberBreakdown = dailyBarberBreakdown,
            DailyServiceBreakdown = dailyServiceBreakdown,

            // Monthly Sales
            MonthlyDailyRows = monthlyDailyRows,
            MonthlyTotalCount = monthlyAppointmentsRaw.Count,
            MonthlyTotalSales = monthlyTotalSales,
            FormattedMonthlyTotalSales = FormattingHelpers.FormatPeso(monthlyTotalSales),

            // Customer Transactions
            CustomerTransactions = customerTxItems,
            CustomerTotalSpent = customerTxItems.Sum(t => t.Price),
            FormattedCustomerTotalSpent = FormattingHelpers.FormatPeso(customerTxItems.Sum(t => t.Price)),

            // Barber Transactions
            BarberTransactions = barberTxItems,
            BarberTotalGenerated = barberTxItems.Sum(t => t.Price),
            FormattedBarberTotalGenerated = FormattingHelpers.FormatPeso(barberTxItems.Sum(t => t.Price))
        };

        return View(viewModel);
    }
}

public class AdminReportsViewModel
{
    public string ActiveTab { get; set; } = "daily";
    public string SelectedDateString { get; set; } = string.Empty;
    public string SelectedDateFormatted { get; set; } = string.Empty;
    public int SelectedYear { get; set; }
    public int SelectedMonth { get; set; }
    public int? SelectedBarberId { get; set; }
    public int? SelectedCustomerId { get; set; }

    public List<BarberBook.Data.Entities.Barber> Barbers { get; set; } = new();
    public List<BarberBook.Data.Entities.ApplicationUser> Customers { get; set; } = new();

    // Daily
    public List<ReportDailyRowItem> DailyRows { get; set; } = new();
    public int DailyTotalCount { get; set; }
    public decimal DailyTotalSales { get; set; }
    public string FormattedDailyTotalSales { get; set; } = "₱0.00";
    public List<ReportGroupBreakdown> DailyBarberBreakdown { get; set; } = new();
    public List<ReportGroupBreakdown> DailyServiceBreakdown { get; set; } = new();

    // Monthly
    public List<ReportMonthDayRow> MonthlyDailyRows { get; set; } = new();
    public int MonthlyTotalCount { get; set; }
    public decimal MonthlyTotalSales { get; set; }
    public string FormattedMonthlyTotalSales { get; set; } = "₱0.00";

    // Customer Tx
    public List<ReportTransactionRow> CustomerTransactions { get; set; } = new();
    public decimal CustomerTotalSpent { get; set; }
    public string FormattedCustomerTotalSpent { get; set; } = "₱0.00";

    // Barber Tx
    public List<ReportTransactionRow> BarberTransactions { get; set; } = new();
    public decimal BarberTotalGenerated { get; set; }
    public string FormattedBarberTotalGenerated { get; set; } = "₱0.00";
}

public class ReportDailyRowItem
{
    public int AppointmentId { get; set; }
    public string ReferenceCode { get; set; } = string.Empty;
    public string TimeFormatted { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string BarberName { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string FormattedPrice { get; set; } = string.Empty;
}

public class ReportGroupBreakdown
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal TotalSales { get; set; }
    public string FormattedTotalSales { get; set; } = string.Empty;
}

public class ReportMonthDayRow
{
    public string Date { get; set; } = string.Empty;
    public string DateFormatted { get; set; } = string.Empty;
    public int CompletedCount { get; set; }
    public decimal TotalSales { get; set; }
    public string FormattedTotalSales { get; set; } = string.Empty;
}

public class ReportTransactionRow
{
    public string ReferenceCode { get; set; } = string.Empty;
    public string DateFormatted { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string BarberName { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string FormattedPrice { get; set; } = string.Empty;
}
