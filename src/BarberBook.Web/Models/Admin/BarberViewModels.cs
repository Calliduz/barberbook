using System.ComponentModel.DataAnnotations;

namespace BarberBook.Web.Models.Admin;

public class CreateBarberRequest
{
    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public string FullName { get; set; } = string.Empty;

    public string ResolvedFullName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(FullName))
                return FullName.Trim();

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(FirstName)) parts.Add(FirstName.Trim());
            if (!string.IsNullOrWhiteSpace(MiddleName)) parts.Add(MiddleName.Trim());
            if (!string.IsNullOrWhiteSpace(LastName)) parts.Add(LastName.Trim());
            return string.Join(" ", parts);
        }
    }

    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Password { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? PhotoUrl { get; set; }
    public string? Specialties { get; set; }
    public int? BranchId { get; set; }
}

public class UpdateBarberRequest
{
    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public string FullName { get; set; } = string.Empty;

    public string ResolvedFullName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(FullName))
                return FullName.Trim();

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(FirstName)) parts.Add(FirstName.Trim());
            if (!string.IsNullOrWhiteSpace(MiddleName)) parts.Add(MiddleName.Trim());
            if (!string.IsNullOrWhiteSpace(LastName)) parts.Add(LastName.Trim());
            return string.Join(" ", parts);
        }
    }

    public string? PhoneNumber { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? PhotoUrl { get; set; }
    public string? Specialties { get; set; }
    public int? BranchId { get; set; }
}

public class ResetBarberPasswordRequest
{
    public string NewPassword { get; set; } = string.Empty;
}

public class BranchOptionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class AdminBarberItemDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? PhotoUrl { get; set; }
    public string? Specialties { get; set; }
    public int? BranchId { get; set; }
    public string? BranchName { get; set; }
    public bool IsActive { get; set; }
    public int CompletedAppointmentsCount { get; set; }
}

public class BarberListViewModel
{
    public List<AdminBarberItemDto> Items { get; set; } = new();
    public List<BranchOptionDto> Branches { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public string? SearchQuery { get; set; }
    public string StatusFilter { get; set; } = "all"; // all, active, inactive
}

