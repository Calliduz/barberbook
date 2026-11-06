using BarberBook.Data.Enums;

namespace BarberBook.Web.Models.Admin;

public class AdminDashboardDto
{
    public TodayCountsDto TodayCounts { get; set; } = new();
    public List<AdminDashboardPendingItemDto> PendingBookings { get; set; } = new();
    public decimal TodayServiceSales { get; set; }
    public string FormattedTodayServiceSales { get; set; } = "₱0.00";
    public decimal ThisMonthServiceSales { get; set; }
    public string FormattedThisMonthServiceSales { get; set; } = "₱0.00";
    public List<AdminDashboardBarberScheduleDto> TodayAppointmentsByBarber { get; set; } = new();
    public int ActiveBarbersCount { get; set; }
    public int TotalCustomersCount { get; set; }
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
