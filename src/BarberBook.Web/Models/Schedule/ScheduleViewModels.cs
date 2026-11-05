using BarberBook.Web.Models.Admin;
using BarberBook.Web.Services;
using NodaTime;

namespace BarberBook.Web.Models.Schedule;

public class WorkingHoursRangeDto
{
    public int DayOfWeek { get; set; } // 0 = Sunday, 1 = Monday, ..., 6 = Saturday
    public string StartTime { get; set; } = string.Empty; // "09:00"
    public string EndTime { get; set; } = string.Empty; // "18:00"
}

public class WorkingHoursItemDto
{
    public int Id { get; set; }
    public int DayOfWeek { get; set; }
    public string DayName { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
}

public class SetWorkingHoursRequest
{
    public List<WorkingHoursRangeDto> Ranges { get; set; } = new();
}

public class CreateDayOffRequest
{
    public string StartDate { get; set; } = string.Empty; // "yyyy-MM-dd"
    public string? EndDate { get; set; } // "yyyy-MM-dd"
    public string? Reason { get; set; }
}

public class DayOffItemDto
{
    public int Id { get; set; }
    public string Date { get; set; } = string.Empty; // "yyyy-MM-dd"
    public string FormattedDate { get; set; } = string.Empty; // "September 20, 2026"
    public string? Reason { get; set; }
}

public class DayOffConflictDto
{
    public int AppointmentId { get; set; }
    public string ReferenceCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string ScheduledDate { get; set; } = string.Empty;
    public string ScheduledTime { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class CreateDayOffResultDto
{
    public int CreatedCount { get; set; }
    public List<string> DatesAdded { get; set; } = new();
    public List<DayOffConflictDto> Conflicts { get; set; } = new();
    public bool HasConflicts => Conflicts.Count > 0;
    public string Message { get; set; } = string.Empty;
}

public class PublicWorkingHoursDto
{
    public int DayOfWeek { get; set; }
    public string DayName { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
}

public class PublicDayOffDto
{
    public string Date { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public class PublicBarberScheduleDto
{
    public int BarberId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public List<PublicWorkingHoursDto> WeeklyHours { get; set; } = new();
    public List<PublicDayOffDto> UpcomingDaysOff { get; set; } = new();
}

public class PublicAvailabilityResponse
{
    public int BarberId { get; set; }
    public int ServiceId { get; set; }
    public string Date { get; set; } = string.Empty;
    public List<string> Slots { get; set; } = new(); // ["09:00", "09:30"]
    public List<AvailableSlotDto> SlotDetails { get; set; } = new();
}

public class AdminBarberScheduleViewModel
{
    public List<AdminBarberItemDto> Barbers { get; set; } = new();
    public int SelectedBarberId { get; set; }
    public AdminBarberItemDto? SelectedBarber { get; set; }
    public Dictionary<int, List<WorkingHoursItemDto>> WeeklyHours { get; set; } = new();
    public List<DayOffItemDto> DaysOff { get; set; } = new();
    public List<DayOffConflictDto> RecentConflicts { get; set; } = new();
}
