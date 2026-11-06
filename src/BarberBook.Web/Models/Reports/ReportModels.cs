using NodaTime;

namespace BarberBook.Web.Models.Reports;

public class DailySalesReportDto
{
    public LocalDate Date { get; set; }
    public string DateFormatted { get; set; } = string.Empty;
    public decimal TotalSales { get; set; }
    public string FormattedTotalSales { get; set; } = "₱0.00";
    public decimal ServiceSales { get => TotalSales; set => TotalSales = value; }
    public string FormattedServiceSales => FormattedTotalSales;
    public int CompletedCount { get; set; }
    public List<GroupSalesBreakdownDto> ByService { get; set; } = new();
    public List<GroupSalesBreakdownDto> ByBarber { get; set; } = new();
    public List<DailySalesLineItemDto> Items { get; set; } = new();

    // HW-2, INV-06: Product sales reported separately beside service sales; Grand total = both
    public ProductSalesSummaryDto ProductSales { get; set; } = new();
    public decimal GrandTotal { get; set; }
    public string FormattedGrandTotal { get; set; } = "₱0.00";
}

public class ProductSalesSummaryDto
{
    public decimal Total { get; set; }
    public string FormattedTotal { get; set; } = "₱0.00";
    public int Count { get; set; }
    public List<GroupSalesBreakdownDto> PerProduct { get; set; } = new();
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
    public decimal ServiceSales { get => TotalSales; set => TotalSales = value; }
    public string FormattedServiceSales => FormattedTotalSales;
    public int CompletedCount { get; set; }
    public List<MonthlyDaySalesDto> DailyTotals { get; set; } = new();
    public List<GroupSalesBreakdownDto> ByService { get; set; } = new();
    public List<GroupSalesBreakdownDto> ByBarber { get; set; } = new();

    // HW-2, INV-06: Product sales reported separately beside service sales; Grand total = both
    public decimal ProductSales { get; set; }
    public string FormattedProductSales { get; set; } = "₱0.00";
    public int ProductSalesCount { get; set; }
    public List<GroupSalesBreakdownDto> ProductSalesPerProduct { get; set; } = new();
    public decimal GrandTotal { get; set; }
    public string FormattedGrandTotal { get; set; } = "₱0.00";
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
    public decimal ServiceSales { get => TotalSales; set => TotalSales = value; }
    public string FormattedServiceSales => FormattedTotalSales;

    // HW-2, INV-06
    public decimal ProductSales { get; set; }
    public string FormattedProductSales { get; set; } = "₱0.00";
    public decimal GrandTotal { get; set; }
    public string FormattedGrandTotal { get; set; } = "₱0.00";
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

    // Barber commission computation (standard 50% split)
    public decimal CommissionRate { get; set; } = 0.50m;
    public decimal TotalCommission { get; set; }
    public string FormattedTotalCommission { get; set; } = "₱0.00";
    public decimal ShopNetShare { get; set; }
    public string FormattedShopNetShare { get; set; } = "₱0.00";

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

    // Commission per transaction
    public decimal CommissionRate { get; set; } = 0.50m;
    public decimal CommissionAmount { get; set; }
    public string FormattedCommissionAmount { get; set; } = "₱0.00";
    public decimal ShopShare { get; set; }
    public string FormattedShopShare { get; set; } = "₱0.00";
}

