using BarberBook.Data.Enums;

namespace BarberBook.Web.Models.Admin;

public class AdminDashboardViewModel
{
    public int TodayAppointmentsCount { get; set; }
    public int PendingApprovalsCount { get; set; }
    public int ActiveBarbersCount { get; set; }
    public int TotalCustomersCount { get; set; }
    public decimal TodayCompletedSales { get; set; }
    public decimal MonthToDateCompletedSales { get; set; }
    public string FormattedTodayCompletedSales { get; set; } = "₱0.00";
    public string FormattedMonthToDateCompletedSales { get; set; } = "₱0.00";

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
