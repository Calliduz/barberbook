using BarberBook.Data.Enums;

namespace BarberBook.Web.Models.Admin;

public class AdminDashboardViewModel
{
    public TodayCountsDto TodayCounts { get; set; } = new();
    public int TodayAppointmentsCount => TodayCounts.Total;
    public int PendingApprovalsCount => TodayCounts.Pending;
    public int ActiveBarbersCount { get; set; }
    public int TotalCustomersCount { get; set; }
    public int FlaggedEquipmentCount { get; set; }
    public List<BarberBook.Web.Models.Inventory.EquipmentMonitoringItemDto> FlaggedEquipmentAlerts { get; set; } = new();
    public decimal TodayCompletedSales { get; set; }
    public decimal MonthToDateCompletedSales { get; set; }
    public string FormattedTodayCompletedSales { get; set; } = "₱0.00";
    public string FormattedMonthToDateCompletedSales { get; set; } = "₱0.00";

    // HW-2, INV-06: Product sales and grand totals
    public decimal TodayProductSales { get; set; }
    public string FormattedTodayProductSales { get; set; } = "₱0.00";
    public decimal MonthToDateProductSales { get; set; }
    public string FormattedMonthToDateProductSales { get; set; } = "₱0.00";
    public decimal TodayGrandTotal { get; set; }
    public string FormattedTodayGrandTotal { get; set; } = "₱0.00";
    public decimal MonthToDateGrandTotal { get; set; }
    public string FormattedMonthToDateGrandTotal { get; set; } = "₱0.00";

    // HW-1, INV-05: Low-stock product alerts
    public int LowStockProductsCount { get; set; }
    public List<BarberBook.Web.Models.Inventory.ProductDto> LowStockProducts { get; set; } = new();

    // Needs action (pending bookings, latest first, up to 10-15)
    public int TotalPendingBookingsCount { get; set; }
    public List<AdminDashboardPendingItemDto> PendingBookings { get; set; } = new();

    // Chart.js Visual Analytics (7-day revenue trend + bookings breakdown)
    public List<string> RevenueChartDates { get; set; } = new();
    public List<decimal> RevenueChartServices { get; set; } = new();
    public List<decimal> RevenueChartProducts { get; set; } = new();
    public int ChartConfirmedCount { get; set; }
    public int ChartCompletedCount { get; set; }
    public int ChartPendingCount { get; set; }
    public int ChartCancelledCount { get; set; }
    public int ChartNoShowCount { get; set; }

    // Today's schedule grouped by barber
    public List<AdminDashboardBarberScheduleDto> TodayAppointmentsByBarber { get; set; } = new();

    // Live recent activity
    public List<AdminDashboardAppointmentItem> RecentAppointments { get; set; } = new();
    public List<AdminDashboardBarberItem> Barbers { get; set; } = new();
}

public class AdminDashboardAppointmentItem
{
    public int Id { get; set; }
    public string ReferenceCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string BarberName { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string FormattedPrice { get; set; } = string.Empty;
    public string StartsAtFormatted { get; set; } = string.Empty;
    public AppointmentStatus Status { get; set; }
}

public class AdminDashboardBarberItem
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Specialties { get; set; } = string.Empty;
    public int CompletedAppointmentsCount { get; set; }
    public decimal TotalRevenueGenerated { get; set; }
    public string FormattedRevenue { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
