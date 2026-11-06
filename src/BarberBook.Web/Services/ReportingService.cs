using System.Globalization;
using BarberBook.Data;
using BarberBook.Data.Enums;
using BarberBook.Web.Common;
using BarberBook.Web.Models.Reports;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace BarberBook.Web.Services;

public class ReportingService : IReportingService
{
    private readonly BarberBookDbContext _dbContext;
    private readonly DateTimeZone _zone;

    public ReportingService(BarberBookDbContext dbContext, DateTimeZone? zone = null)
    {
        _dbContext = dbContext;
        _zone = zone ?? DateTimeZoneProviders.Tzdb["Asia/Manila"];
    }

    /// <inheritdoc />
    public async Task<DailySalesReportDto> GetDailySalesAsync(LocalDate date, CancellationToken ct = default)
    {
        // Scheduled date in Asia/Manila (BR-11)
        var dayStartUtc = date.AtStartOfDayInZone(_zone).ToInstant();
        var nextDayStartUtc = date.PlusDays(1).AtStartOfDayInZone(_zone).ToInstant();

        // Rule BR-07: Only appointments with status "completed" count in sales
        // Rule BR-08: Amount taken from appointment's price snapshot
        var appointments = await _dbContext.Appointments
            .AsNoTracking()
            .Include(a => a.Customer)
            .Include(a => a.Barber)
            .Where(a => a.Status == AppointmentStatus.Completed &&
                        a.StartsAt >= dayStartUtc &&
                        a.StartsAt < nextDayStartUtc)
            .OrderBy(a => a.StartsAt)
            .ToListAsync(ct);

        int counter = 1;
        var items = appointments.Select(a =>
        {
            var zonedTime = a.StartsAt.InZone(_zone);
            return new DailySalesLineItemDto
            {
                No = counter++,
                AppointmentId = a.Id,
                ReferenceCode = a.ReferenceCode,
                Time = zonedTime.ToString("h:mm tt", CultureInfo.InvariantCulture),
                Customer = a.Customer.FullName,
                Barber = a.Barber.DisplayName,
                Service = a.ServiceName,
                Amount = a.Price,
                FormattedAmount = FormattingHelpers.FormatPeso(a.Price)
            };
        }).ToList();

        var totalSales = items.Sum(i => i.Amount);

        var byService = appointments
            .GroupBy(a => a.ServiceName)
            .Select(g => new GroupSalesBreakdownDto
            {
                Name = g.Key,
                Count = g.Count(),
                TotalSales = g.Sum(a => a.Price),
                FormattedTotalSales = FormattingHelpers.FormatPeso(g.Sum(a => a.Price))
            })
            .OrderByDescending(g => g.TotalSales)
            .ThenBy(g => g.Name)
            .ToList();

        var byBarber = appointments
            .GroupBy(a => a.Barber.DisplayName)
            .Select(g => new GroupSalesBreakdownDto
            {
                Name = g.Key,
                Count = g.Count(),
                TotalSales = g.Sum(a => a.Price),
                FormattedTotalSales = FormattingHelpers.FormatPeso(g.Sum(a => a.Price))
            })
            .OrderByDescending(g => g.TotalSales)
            .ThenBy(g => g.Name)
            .ToList();

        return new DailySalesReportDto
        {
            Date = date,
            DateFormatted = date.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture),
            TotalSales = totalSales,
            FormattedTotalSales = FormattingHelpers.FormatPeso(totalSales),
            CompletedCount = items.Count,
            ByService = byService,
            ByBarber = byBarber,
            Items = items
        };
    }

    /// <inheritdoc />
    public async Task<MonthlySalesReportDto> GetMonthlySalesAsync(int year, int month, CancellationToken ct = default)
    {
        if (month < 1 || month > 12)
        {
            month = 1;
        }

        var monthStart = new LocalDate(year, month, 1);
        var nextMonthStart = monthStart.PlusMonths(1);
        var monthStartUtc = monthStart.AtStartOfDayInZone(_zone).ToInstant();
        var nextMonthStartUtc = nextMonthStart.AtStartOfDayInZone(_zone).ToInstant();

        // Query all completed appointments in the month in Asia/Manila (BR-07, BR-11)
        var appointments = await _dbContext.Appointments
            .AsNoTracking()
            .Include(a => a.Barber)
            .Where(a => a.Status == AppointmentStatus.Completed &&
                        a.StartsAt >= monthStartUtc &&
                        a.StartsAt < nextMonthStartUtc)
            .ToListAsync(ct);

        // Group appointments by their local calendar date in Asia/Manila
        var appointmentsByDate = appointments
            .GroupBy(a => a.StartsAt.InZone(_zone).Date)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Day-by-day totals for EVERY day of the month (zero days included)
        var daysInMonth = monthStart.Calendar.GetDaysInMonth(year, month);
        var dailyTotals = new List<MonthlyDaySalesDto>(daysInMonth);

        for (int d = 1; d <= daysInMonth; d++)
        {
            var dayDate = new LocalDate(year, month, d);
            if (appointmentsByDate.TryGetValue(dayDate, out var dayAppts) && dayAppts.Count > 0)
            {
                var daySum = dayAppts.Sum(a => a.Price);
                dailyTotals.Add(new MonthlyDaySalesDto
                {
                    Day = d,
                    Date = dayDate,
                    DateFormatted = dayDate.ToString("MMM d, yyyy", CultureInfo.InvariantCulture),
                    DayOfWeek = dayDate.ToString("ddd", CultureInfo.InvariantCulture),
                    CompletedCount = dayAppts.Count,
                    TotalSales = daySum,
                    FormattedTotalSales = FormattingHelpers.FormatPeso(daySum)
                });
            }
            else
            {
                dailyTotals.Add(new MonthlyDaySalesDto
                {
                    Day = d,
                    Date = dayDate,
                    DateFormatted = dayDate.ToString("MMM d, yyyy", CultureInfo.InvariantCulture),
                    DayOfWeek = dayDate.ToString("ddd", CultureInfo.InvariantCulture),
                    CompletedCount = 0,
                    TotalSales = 0m,
                    FormattedTotalSales = "₱0.00"
                });
            }
        }

        // The month total must equal the sum of the daily totals (BR-11)
        var totalSales = dailyTotals.Sum(d => d.TotalSales);
        var totalCompleted = dailyTotals.Sum(d => d.CompletedCount);

        var byService = appointments
            .GroupBy(a => a.ServiceName)
            .Select(g => new GroupSalesBreakdownDto
            {
                Name = g.Key,
                Count = g.Count(),
                TotalSales = g.Sum(a => a.Price),
                FormattedTotalSales = FormattingHelpers.FormatPeso(g.Sum(a => a.Price))
            })
            .OrderByDescending(g => g.TotalSales)
            .ThenBy(g => g.Name)
            .ToList();

        var byBarber = appointments
            .GroupBy(a => a.Barber.DisplayName)
            .Select(g => new GroupSalesBreakdownDto
            {
                Name = g.Key,
                Count = g.Count(),
                TotalSales = g.Sum(a => a.Price),
                FormattedTotalSales = FormattingHelpers.FormatPeso(g.Sum(a => a.Price))
            })
            .OrderByDescending(g => g.TotalSales)
            .ThenBy(g => g.Name)
            .ToList();

        return new MonthlySalesReportDto
        {
            Year = year,
            Month = month,
            MonthName = monthStart.ToString("MMMM", CultureInfo.InvariantCulture),
            TotalSales = totalSales,
            FormattedTotalSales = FormattingHelpers.FormatPeso(totalSales),
            CompletedCount = totalCompleted,
            DailyTotals = dailyTotals,
            ByService = byService,
            ByBarber = byBarber
        };
    }

    /// <inheritdoc />
    public async Task<CustomerTransactionsReportDto> GetCustomerTransactionsAsync(
        int customerId,
        LocalDate? startDate = null,
        LocalDate? endDate = null,
        CancellationToken ct = default)
    {
        var customer = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == customerId, ct);

        var query = _dbContext.Appointments
            .AsNoTracking()
            .Include(a => a.Barber)
            .Where(a => a.CustomerId == customerId && a.Status == AppointmentStatus.Completed);

        if (startDate.HasValue)
        {
            var startUtc = startDate.Value.AtStartOfDayInZone(_zone).ToInstant();
            query = query.Where(a => a.StartsAt >= startUtc);
        }

        if (endDate.HasValue)
        {
            var endUtc = endDate.Value.PlusDays(1).AtStartOfDayInZone(_zone).ToInstant();
            query = query.Where(a => a.StartsAt < endUtc);
        }

        // Newest first (RP-3, BR-12)
        var appointments = await query
            .OrderByDescending(a => a.StartsAt)
            .ToListAsync(ct);

        var items = appointments.Select(a =>
        {
            var zoned = a.StartsAt.InZone(_zone);
            return new CustomerTransactionItemDto
            {
                AppointmentId = a.Id,
                ReferenceCode = a.ReferenceCode,
                Date = zoned.Date,
                DateFormatted = zoned.Date.ToString("MMM d, yyyy", CultureInfo.InvariantCulture),
                TimeFormatted = zoned.ToString("h:mm tt", CultureInfo.InvariantCulture),
                Service = a.ServiceName,
                Barber = a.Barber.DisplayName,
                Status = "Completed",
                Amount = a.Price,
                FormattedAmount = FormattingHelpers.FormatPeso(a.Price)
            };
        }).ToList();

        var totalSpent = items.Sum(i => i.Amount);

        return new CustomerTransactionsReportDto
        {
            CustomerId = customerId,
            CustomerName = customer?.FullName ?? "Unknown Customer",
            CustomerEmail = customer?.Email,
            TotalSpent = totalSpent,
            FormattedTotalSpent = FormattingHelpers.FormatPeso(totalSpent),
            CompletedCount = items.Count,
            Transactions = items
        };
    }

    /// <inheritdoc />
    public async Task<BarberTransactionsReportDto> GetBarberTransactionsAsync(
        int barberId,
        LocalDate? startDate = null,
        LocalDate? endDate = null,
        CancellationToken ct = default)
    {
        var barber = await _dbContext.Barbers
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == barberId, ct);

        var query = _dbContext.Appointments
            .AsNoTracking()
            .Include(a => a.Customer)
            .Where(a => a.BarberId == barberId && a.Status == AppointmentStatus.Completed);

        if (startDate.HasValue)
        {
            var startUtc = startDate.Value.AtStartOfDayInZone(_zone).ToInstant();
            query = query.Where(a => a.StartsAt >= startUtc);
        }

        if (endDate.HasValue)
        {
            var endUtc = endDate.Value.PlusDays(1).AtStartOfDayInZone(_zone).ToInstant();
            query = query.Where(a => a.StartsAt < endUtc);
        }

        // Newest first (RP-4, BR-12)
        var appointments = await query
            .OrderByDescending(a => a.StartsAt)
            .ToListAsync(ct);

        var items = appointments.Select(a =>
        {
            var zoned = a.StartsAt.InZone(_zone);
            return new BarberTransactionItemDto
            {
                AppointmentId = a.Id,
                ReferenceCode = a.ReferenceCode,
                Date = zoned.Date,
                DateFormatted = zoned.Date.ToString("MMM d, yyyy", CultureInfo.InvariantCulture),
                TimeFormatted = zoned.ToString("h:mm tt", CultureInfo.InvariantCulture),
                Customer = a.Customer.FullName,
                Service = a.ServiceName,
                Amount = a.Price,
                FormattedAmount = FormattingHelpers.FormatPeso(a.Price)
            };
        }).ToList();

        var totalSales = items.Sum(i => i.Amount);

        return new BarberTransactionsReportDto
        {
            BarberId = barberId,
            BarberName = barber?.DisplayName ?? "Unknown Barber",
            TotalSales = totalSales,
            FormattedTotalSales = FormattingHelpers.FormatPeso(totalSales),
            CompletedCount = items.Count,
            Transactions = items
        };
    }
}

