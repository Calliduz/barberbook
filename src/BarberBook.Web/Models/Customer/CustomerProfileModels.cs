using BarberBook.Web.Models.Schedule;

namespace BarberBook.Web.Models.Customer;

public class CustomerProfileDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
}

public class UpdateProfileRequest
{
    private string _firstName = string.Empty;
    private string _lastName = string.Empty;
    private string _fullName = string.Empty;

    public string FirstName
    {
        get => !string.IsNullOrWhiteSpace(_firstName) ? _firstName : GetFirstFromFullName(_fullName);
        set
        {
            _firstName = value ?? string.Empty;
            SyncFullName();
        }
    }

    public string LastName
    {
        get => !string.IsNullOrWhiteSpace(_lastName) ? _lastName : GetLastFromFullName(_fullName);
        set
        {
            _lastName = value ?? string.Empty;
            SyncFullName();
        }
    }

    public string FullName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_firstName) || !string.IsNullOrWhiteSpace(_lastName))
            {
                return $"{_firstName} {_lastName}".Trim();
            }
            return _fullName;
        }
        set
        {
            _fullName = value ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(_fullName) && string.IsNullOrWhiteSpace(_firstName) && string.IsNullOrWhiteSpace(_lastName))
            {
                var parts = _fullName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                _firstName = parts.Length > 0 ? parts[0] : string.Empty;
                _lastName = parts.Length > 1 ? parts[1] : string.Empty;
            }
        }
    }

    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }

    private void SyncFullName()
    {
        if (!string.IsNullOrWhiteSpace(_firstName) || !string.IsNullOrWhiteSpace(_lastName))
        {
            _fullName = $"{_firstName} {_lastName}".Trim();
        }
    }

    private static string GetFirstFromFullName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return string.Empty;
        var parts = fullName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : string.Empty;
    }

    private static string GetLastFromFullName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return string.Empty;
        var parts = fullName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? parts[1] : string.Empty;
    }
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
    public int? BranchId { get; set; }
    public string? BranchName { get; set; }
    public string WeeklyScheduleText { get; set; } = string.Empty;
}

public class BarberDetailDto
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? PhotoUrl { get; set; }
    public string? Specialties { get; set; }
    public int? BranchId { get; set; }
    public string? BranchName { get; set; }
    public string WeeklyScheduleText { get; set; } = string.Empty;
    public List<PublicWorkingHoursDto> WeeklyHours { get; set; } = new();
    public List<PublicDayOffDto> UpcomingDaysOff { get; set; } = new();
}

