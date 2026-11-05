using BarberBook.Web.Models.Schedule;

namespace BarberBook.Web.Models.Customer;

public class CustomerProfileDto
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
}

public class UpdateProfileRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
}

public class PublicServiceDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string FormattedPrice { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
}

public class PublicBarberDto
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? PhotoUrl { get; set; }
    public string? Specialties { get; set; }
    public string WeeklyScheduleText { get; set; } = string.Empty;
}

public class BarberDetailDto
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? PhotoUrl { get; set; }
    public string? Specialties { get; set; }
    public string WeeklyScheduleText { get; set; } = string.Empty;
    public List<PublicWorkingHoursDto> WeeklyHours { get; set; } = new();
    public List<PublicDayOffDto> UpcomingDaysOff { get; set; } = new();
}

