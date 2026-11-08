using NodaTime;

namespace BarberBook.Web.Models.Admin;

public class CreateBranchRequest
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string OpeningTime { get; set; } = "09:00";
    public string ClosingTime { get; set; } = "18:00";
}

public class UpdateBranchRequest
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string OpeningTime { get; set; } = "09:00";
    public string ClosingTime { get; set; } = "18:00";
}

public class AdminBranchItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string OpeningTime { get; set; } = "09:00";
    public string ClosingTime { get; set; } = "18:00";
    public bool IsActive { get; set; }
    public int BarbersCount { get; set; }
    public int CompletedAppointmentsCount { get; set; }
}

public class BranchListViewModel
{
    public List<AdminBranchItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public string? SearchQuery { get; set; }
    public string StatusFilter { get; set; } = "all";
}

public class BranchBarberItemDto
{
    public int BarberId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Specialties { get; set; }
    public bool IsAssignedToThisBranch { get; set; }
    public string? CurrentBranchName { get; set; }
}

public class AssignBranchBarbersRequest
{
    public List<int> BarberIds { get; set; } = new();
}
