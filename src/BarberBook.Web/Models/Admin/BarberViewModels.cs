using System.ComponentModel.DataAnnotations;

namespace BarberBook.Web.Models.Admin;

public class CreateBarberRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? PhotoUrl { get; set; }
    public string? Specialties { get; set; }
}

public class UpdateBarberRequest
{
    public string FullName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? PhotoUrl { get; set; }
    public string? Specialties { get; set; }
}

public class ResetBarberPasswordRequest
{
    public string NewPassword { get; set; } = string.Empty;
}

public class AdminBarberItemDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? PhotoUrl { get; set; }
    public string? Specialties { get; set; }
    public bool IsActive { get; set; }
    public int CompletedAppointmentsCount { get; set; }
}

public class BarberListViewModel
{
    public List<AdminBarberItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public string? SearchQuery { get; set; }
    public string StatusFilter { get; set; } = "all"; // all, active, inactive
}

