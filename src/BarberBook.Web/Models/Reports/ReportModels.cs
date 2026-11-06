using NodaTime;

namespace BarberBook.Web.Models.Reports;

public class DailySalesReportDto
{
    public LocalDate Date { get; set; }
    public string DateFormatted { get; set; } = string.Empty;
    public decimal TotalSales { get; set; }
    public string FormattedTotalSales { get; set; } = "₱0.00";
    public int CompletedCount { get; set; }
    public List<GroupSalesBreakdownDto> ByService { get; set; } = new();
    public List<GroupSalesBreakdownDto> ByBarber { get; set; } = new();
    public List<DailySalesLineItemDto> Items { get; set; } = new();
}

public class DailySalesLineItemDto
{
    public int No { get; set; }
    public int AppointmentId { get; set; }
    public string ReferenceCode { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public string Customer { get; set; } = string.Empty;
    public string Barber { get; set; } = string.Empty;
    public string Service { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string FormattedAmount { get; set; } = string.Empty;
}

public class GroupSalesBreakdownDto
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal TotalSales { get; set; }
    public string FormattedTotalSales { get; set; } = string.Empty;
}

public class MonthlySalesReportDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public decimal TotalSales { get; set; }
    public string FormattedTotalSales { get; set; } = "₱0.00";
    public int CompletedCount { get; set; }
    public List<MonthlyDaySalesDto> DailyTotals { get; set; } = new();
    public List<GroupSalesBreakdownDto> ByService { get; set; } = new();
    public List<GroupSalesBreakdownDto> ByBarber { get; set; } = new();
}

public class MonthlyDaySalesDto
{
    public int Day { get; set; }
    public LocalDate Date { get; set; }
    public string DateFormatted { get; set; } = string.Empty;
    public string DayOfWeek { get; set; } = string.Empty;
    public int CompletedCount { get; set; }
    public decimal TotalSales { get; set; }
    public string FormattedTotalSales { get; set; } = "₱0.00";
}

public class CustomerTransactionsReportDto
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public decimal TotalSpent { get; set; }
    public string FormattedTotalSpent { get; set; } = "₱0.00";
    public int CompletedCount { get; set; }
    public List<CustomerTransactionItemDto> Transactions { get; set; } = new();
}

public class CustomerTransactionItemDto
{
    public int AppointmentId { get; set; }
    public string ReferenceCode { get; set; } = string.Empty;
    public LocalDate Date { get; set; }
    public string DateFormatted { get; set; } = string.Empty;
    public string TimeFormatted { get; set; } = string.Empty;
    public string Service { get; set; } = string.Empty;
    public string Barber { get; set; } = string.Empty;
    public string Status { get; set; } = "Completed";
    public decimal Amount { get; set; }
    public string FormattedAmount { get; set; } = string.Empty;
}

public class BarberTransactionsReportDto
{
    public int BarberId { get; set; }
    public string BarberName { get; set; } = string.Empty;
    public decimal TotalSales { get; set; }
    public string FormattedTotalSales { get; set; } = "₱0.00";
    public int CompletedCount { get; set; }
    public List<BarberTransactionItemDto> Transactions { get; set; } = new();
}

public class BarberTransactionItemDto
{
    public int AppointmentId { get; set; }
    public string ReferenceCode { get; set; } = string.Empty;
    public LocalDate Date { get; set; }
    public string DateFormatted { get; set; } = string.Empty;
    public string TimeFormatted { get; set; } = string.Empty;
    public string Customer { get; set; } = string.Empty;
    public string Service { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string FormattedAmount { get; set; } = string.Empty;
}

