using BarberBook.Data.Enums;

namespace BarberBook.Web.Models.Admin;

public class AdminDashboardDto
{
    public TodayCountsDto TodayCounts { get; set; } = new();
    public int TotalPendingBookingsCount { get; set; }
    public List<AdminDashboardPendingItemDto> PendingBookings { get; set; } = new();
    public decimal TodayServiceSales { get; set; }
    public string FormattedTodayServiceSales { get; set; } = "₱0.00";
    public decimal ThisMonthServiceSales { get; set; }
    public string FormattedThisMonthServiceSales { get; set; } = "₱0.00";

    // HW-2, INV-06: Product sales and grand totals
    public decimal TodayProductSales { get; set; }
    public string FormattedTodayProductSales { get; set; } = "₱0.00";
    public decimal ThisMonthProductSales { get; set; }
    public string FormattedThisMonthProductSales { get; set; } = "₱0.00";
    public decimal TodayGrandTotal { get; set; }
    public string FormattedTodayGrandTotal { get; set; } = "₱0.00";
    public decimal ThisMonthGrandTotal { get; set; }
    public string FormattedThisMonthGrandTotal { get; set; } = "₱0.00";

    public List<AdminDashboardBarberScheduleDto> TodayAppointmentsByBarber { get; set; } = new();
    public int ActiveBarbersCount { get; set; }
    public int TotalCustomersCount { get; set; }
    public int FlaggedEquipmentCount { get; set; }
    public List<BarberBook.Web.Models.Inventory.EquipmentMonitoringItemDto> FlaggedEquipmentAlerts { get; set; } = new();

    // HW-1, INV-05: Low-stock product alerts
    public int LowStockProductsCount { get; set; }
    public List<BarberBook.Web.Models.Inventory.ProductDto> LowStockProducts { get; set; } = new();
}

public class TodayCountsDto
{
    public int Pending { get; set; }
    public int Confirmed { get; set; }
    public int Completed { get; set; }
    public int Cancelled { get; set; }
    public int Rejected { get; set; }
    public int Total { get; set; }
}

public class AdminDashboardPendingItemDto
{
    public int Id { get; set; }
    public string ReferenceCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public string BarberName { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string FormattedPrice { get; set; } = string.Empty;
    public string ScheduledDateFormatted { get; set; } = string.Empty;
    public string ScheduledTime { get; set; } = string.Empty;
    public string CreatedAtFormatted { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class AdminDashboardBarberScheduleDto
{
    public int BarberId { get; set; }
    public string BarberName { get; set; } = string.Empty;
    public string Specialty { get; set; } = string.Empty;
    public List<AdminDashboardScheduleItemDto> Appointments { get; set; } = new();
}

public class AdminDashboardScheduleItemDto
{
    public int Id { get; set; }
    public string ReferenceCode { get; set; } = string.Empty;
    public string TimeFormatted { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public AppointmentStatus Status { get; set; }
    public string StatusString { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string FormattedPrice { get; set; } = string.Empty;
}

