using BarberBook.Data.Entities;
using BarberBook.Data.Enums;
using BarberBook.Web.Services;
using NodaTime;

namespace BarberBook.Web.Models.Barber;

public class BarberDashboardViewModel
{
    public string BarberName { get; set; } = string.Empty;
    public string? BarberSpecialty { get; set; }
    public string? BarberPhoto { get; set; }
    public string TodayFormattedDate { get; set; } = string.Empty;
    public int TodayAppointmentsCount { get; set; }
    public int PendingAppointmentsCount { get; set; }
    public int CompletedAppointmentsCount { get; set; }
    public decimal TodayEarnings { get; set; }
    public string FormattedTodayEarnings { get; set; } = string.Empty;
    public BarberAppointmentItemViewModel? NextAppointment { get; set; }
    public List<BarberAppointmentItemViewModel> TodayAppointments { get; set; } = new();
}

public class BarberScheduleViewModel
{
    public int BarberId { get; set; }
    public string BarberName { get; set; } = string.Empty;
    public LocalDate SelectedDate { get; set; }
    public string SelectedDateFormatted { get; set; } = string.Empty;
    public string ViewMode { get; set; } = "day"; // "day" or "week"
    public string PrevDateString { get; set; } = string.Empty;
    public string NextDateString { get; set; } = string.Empty;
    public string TodayDateString { get; set; } = string.Empty;
    public string WeekStartDateFormatted { get; set; } = string.Empty;
    public string WeekEndDateFormatted { get; set; } = string.Empty;
    public string? WorkingHoursToday { get; set; }
    public List<BarberAppointmentItemViewModel> DayAppointments { get; set; } = new();
    public List<BarberScheduleDayGroup> WeekDays { get; set; } = new();
}

public class BarberScheduleDayGroup
{
    public LocalDate Date { get; set; }
    public string DateFormatted { get; set; } = string.Empty;
    public string DayName { get; set; } = string.Empty;
    public bool IsToday { get; set; }
    public bool IsSelected { get; set; }
    public List<BarberAppointmentItemViewModel> Appointments { get; set; } = new();
}

public class BarberAppointmentsViewModel
{
    public int BarberId { get; set; }
    public string BarberName { get; set; } = string.Empty;
    public string? StatusFilter { get; set; }
    public string? SearchTerm { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public int TotalCount { get; set; }
    public int PendingCount { get; set; }
    public int ConfirmedCount { get; set; }
    public int CompletedCount { get; set; }
    public int CancelledCount { get; set; }
    public int RejectedCount { get; set; }
    public List<BarberAppointmentItemViewModel> Appointments { get; set; } = new();
}

public class BarberAppointmentItemViewModel
{
    public int Id { get; set; }
    public string ReferenceCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string FormattedPrice { get; set; } = string.Empty;
    public Instant StartsAt { get; set; }
    public Instant EndsAt { get; set; }
    public string ScheduledDateFormatted { get; set; } = string.Empty;
    public string ScheduledDateString { get; set; } = string.Empty;
    public string ScheduledTime12h { get; set; } = string.Empty;
    public AppointmentStatus Status { get; set; }
    public string StatusString => Status.ToString().ToLowerInvariant();
    public string? Notes { get; set; }
    public List<BarberStatusHistoryItemViewModel> StatusHistories { get; set; } = new();
}

public class BarberStatusHistoryItemViewModel
{
    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;
    public string ChangedByRole { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string TimestampFormatted { get; set; } = string.Empty;
}

public class BarberAvailabilityViewModel
{
    public int BarberId { get; set; }
    public string BarberName { get; set; } = string.Empty;
    public List<BarberWorkingHoursItemViewModel> WorkingHours { get; set; } = new();
    public List<BarberDayOffItemViewModel> DaysOff { get; set; } = new();
}

public class BarberWorkingHoursItemViewModel
{
    public int? Id { get; set; }
    public int DayOfWeek { get; set; }
    public string DayName { get; set; } = string.Empty;
    public string StartTime { get; set; } = "09:00";
    public string EndTime { get; set; } = "18:00";
    public bool IsWorking { get; set; }
}

public class BarberDayOffItemViewModel
{
    public int Id { get; set; }
    public string DateString { get; set; } = string.Empty;
    public string FormattedDate { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public class BarberTransactionsViewModel
{
    public int BarberId { get; set; }
    public string BarberName { get; set; } = string.Empty;
    public int TotalCompletedCount { get; set; }
    public decimal TotalRevenue { get; set; }
    public string FormattedTotalRevenue { get; set; } = "₱0.00";
    public decimal AverageTicket { get; set; }
    public string FormattedAverageTicket { get; set; } = "₱0.00";

    // Commission fields (50% split)
    public decimal CommissionRate { get; set; } = 0.50m;
    public decimal TotalCommission { get; set; }
    public string FormattedTotalCommission { get; set; } = "₱0.00";
    public decimal ShopShare { get; set; }
    public string FormattedShopShare { get; set; } = "₱0.00";

    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public List<BarberTransactionItemViewModel> Transactions { get; set; } = new();
}

public class BarberTransactionItemViewModel
{
    public int AppointmentId { get; set; }
    public string ReferenceCode { get; set; } = string.Empty;
    public string DateFormatted { get; set; } = string.Empty;
    public string TimeFormatted { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string FormattedPrice { get; set; } = string.Empty;
    public decimal CommissionRate { get; set; } = 0.50m;
    public decimal CommissionAmount { get; set; }
    public string FormattedCommissionAmount { get; set; } = "₱0.00";
}

public class BarberEquipmentViewModel
{
    public int BarberId { get; set; }
    public string BarberName { get; set; } = string.Empty;
    public IReadOnlyList<EquipmentItem> Items { get; set; } = new List<EquipmentItem>();
}

public class BarberAccountViewModel
{
    public int BarberId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Specialties { get; set; }
    public string? PhotoUrl { get; set; }
    public bool IsActive { get; set; }
}
