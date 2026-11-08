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

        // HW-2, INV-06: Query product sales on that day in Asia/Manila
        var daySales = await _dbContext.ProductSales
            .AsNoTracking()
            .Include(s => s.Items)
            .Where(s => s.SoldAt >= dayStartUtc && s.SoldAt < nextDayStartUtc)
            .ToListAsync(ct);

        var productSalesItems = daySales.SelectMany(s => s.Items).ToList();
        var productSalesTotal = productSalesItems.Sum(i => i.LineTotal);

        var productSalesPerProduct = productSalesItems
            .GroupBy(i => i.ProductName)
            .Select(g => new GroupSalesBreakdownDto
            {
                Name = g.Key,
                Count = g.Sum(x => x.Quantity),
                TotalSales = g.Sum(x => x.LineTotal),
                FormattedTotalSales = FormattingHelpers.FormatPeso(g.Sum(x => x.LineTotal))
            })
            .OrderByDescending(g => g.TotalSales)
            .ThenBy(g => g.Name)
            .ToList();

        var grandTotal = totalSales + productSalesTotal;

        // Unified sales journal containing both completed services and settled retail counter sales
        var combinedJournal = new List<DailySalesLineItemDto>(items);
        foreach (var sale in daySales)
        {
            var saleTime = sale.SoldAt.InZone(_zone).ToString("h:mm tt", CultureInfo.InvariantCulture);
            var customerName = sale.Customer?.FullName ?? (string.IsNullOrWhiteSpace(sale.Notes) ? "Counter Client" : sale.Notes);
            foreach (var item in sale.Items)
            {
                combinedJournal.Add(new DailySalesLineItemDto
                {
                    No = 0, // reindexed below
                    AppointmentId = 0,
                    ReferenceCode = $"SALE-#{sale.Id:D4}",
                    Time = saleTime,
                    Customer = customerName,
                    Barber = "Shop Counter",
                    Service = item.ProductName + (item.Quantity > 1 ? $" (x{item.Quantity})" : ""),
                    Amount = item.LineTotal,
                    FormattedAmount = FormattingHelpers.FormatPeso(item.LineTotal),
                    ItemType = "Retail Product"
                });
            }
        }

        // Re-index line item numbering
        for (int i = 0; i < combinedJournal.Count; i++)
        {
            combinedJournal[i].No = i + 1;
        }

        return new DailySalesReportDto
        {
            Date = date,
            DateFormatted = date.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture),
            TotalSales = totalSales,
            FormattedTotalSales = FormattingHelpers.FormatPeso(totalSales),
            CompletedCount = items.Count,
            ByService = byService,
            ByBarber = byBarber,
            Items = items,
            CombinedJournal = combinedJournal,
            ProductSales = new ProductSalesSummaryDto
            {
                Total = productSalesTotal,
                FormattedTotal = FormattingHelpers.FormatPeso(productSalesTotal),
                Count = daySales.Count,
                PerProduct = productSalesPerProduct
            },
            GrandTotal = grandTotal,
            FormattedGrandTotal = FormattingHelpers.FormatPeso(grandTotal)
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

        // HW-2, INV-06: Query product sales in month
        var monthProductSales = await _dbContext.ProductSales
            .AsNoTracking()
            .Include(s => s.Items)
            .Where(s => s.SoldAt >= monthStartUtc && s.SoldAt < nextMonthStartUtc)
            .ToListAsync(ct);

        var productSalesByDate = monthProductSales
            .GroupBy(s => s.SoldAt.InZone(_zone).Date)
            .ToDictionary(g => g.Key, g => g.SelectMany(s => s.Items).ToList());

        // Day-by-day totals for EVERY day of the month (zero days included)
        var daysInMonth = monthStart.Calendar.GetDaysInMonth(year, month);
        var dailyTotals = new List<MonthlyDaySalesDto>(daysInMonth);

        for (int d = 1; d <= daysInMonth; d++)
        {
            var dayDate = new LocalDate(year, month, d);
            decimal dayServiceSales = 0m;
            int dayApptsCount = 0;

            if (appointmentsByDate.TryGetValue(dayDate, out var dayAppts) && dayAppts.Count > 0)
            {
                dayServiceSales = dayAppts.Sum(a => a.Price);
                dayApptsCount = dayAppts.Count;
            }

            decimal dayProductSales = 0m;
            if (productSalesByDate.TryGetValue(dayDate, out var dayPItems) && dayPItems.Count > 0)
            {
                dayProductSales = dayPItems.Sum(i => i.LineTotal);
            }

            var dayGrandTotal = dayServiceSales + dayProductSales;

            dailyTotals.Add(new MonthlyDaySalesDto
            {
                Day = d,
                Date = dayDate,
                DateFormatted = dayDate.ToString("MMM d, yyyy", CultureInfo.InvariantCulture),
                DayOfWeek = dayDate.ToString("ddd", CultureInfo.InvariantCulture),
                CompletedCount = dayApptsCount,
                TotalSales = dayServiceSales,
                FormattedTotalSales = FormattingHelpers.FormatPeso(dayServiceSales),
                ProductSales = dayProductSales,
                FormattedProductSales = FormattingHelpers.FormatPeso(dayProductSales),
                GrandTotal = dayGrandTotal,
                FormattedGrandTotal = FormattingHelpers.FormatPeso(dayGrandTotal)
            });
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

        // Product sales breakdowns
        var allMonthPItems = monthProductSales.SelectMany(s => s.Items).ToList();
        var monthProductSalesTotal = allMonthPItems.Sum(i => i.LineTotal);
        var monthGrandTotal = totalSales + monthProductSalesTotal;

        var productSalesPerProduct = allMonthPItems
            .GroupBy(i => i.ProductName)
            .Select(g => new GroupSalesBreakdownDto
            {
                Name = g.Key,
                Count = g.Sum(x => x.Quantity),
                TotalSales = g.Sum(x => x.LineTotal),
                FormattedTotalSales = FormattingHelpers.FormatPeso(g.Sum(x => x.LineTotal))
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
            ByBarber = byBarber,
            ProductSales = monthProductSalesTotal,
            FormattedProductSales = FormattingHelpers.FormatPeso(monthProductSalesTotal),
            ProductSalesCount = monthProductSales.Count,
            ProductSalesPerProduct = productSalesPerProduct,
            GrandTotal = monthGrandTotal,
            FormattedGrandTotal = FormattingHelpers.FormatPeso(monthGrandTotal)
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

        var effectiveRate = barber?.CommissionRate > 0 ? barber.CommissionRate : 0.50m;

        var items = appointments.Select(a =>
        {
            var zoned = a.StartsAt.InZone(_zone);
            var commission = a.Price * effectiveRate;
            var shopShare = a.Price - commission;
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
                FormattedAmount = FormattingHelpers.FormatPeso(a.Price),
                CommissionRate = effectiveRate,
                CommissionAmount = commission,
                FormattedCommissionAmount = FormattingHelpers.FormatPeso(commission),
                ShopShare = shopShare,
                FormattedShopShare = FormattingHelpers.FormatPeso(shopShare)
            };
        }).ToList();

        var totalSales = items.Sum(i => i.Amount);
        var totalCommission = items.Sum(i => i.CommissionAmount);
        var shopNetShare = totalSales - totalCommission;

        return new BarberTransactionsReportDto
        {
            BarberId = barberId,
            BarberName = barber?.DisplayName ?? "Unknown Barber",
            TotalSales = totalSales,
            FormattedTotalSales = FormattingHelpers.FormatPeso(totalSales),
            CommissionRate = effectiveRate,
            TotalCommission = totalCommission,
            FormattedTotalCommission = FormattingHelpers.FormatPeso(totalCommission),
            ShopNetShare = shopNetShare,
            FormattedShopNetShare = FormattingHelpers.FormatPeso(shopNetShare),
            CompletedCount = items.Count,
            Transactions = items
        };
    }
}

