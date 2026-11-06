using System.ComponentModel.DataAnnotations;
using BarberBook.Data.Enums;
using NodaTime;

namespace BarberBook.Web.Models.Admin;

public class AdminCustomerListViewModel
{
    public List<AdminCustomerItemViewModel> Items { get; set; } = new();
    public string? SearchQuery { get; set; }
    public string StatusFilter { get; set; } = "all";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalCustomers { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling((double)TotalCustomers / PageSize));
    public int ActiveCount { get; set; }
    public int InactiveCount { get; set; }
}

public class AdminCustomerItemViewModel
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string MemberSince { get; set; } = string.Empty;
    public int TotalAppointments { get; set; }
    public int CompletedVisits { get; set; }
    public decimal TotalSpent { get; set; }
    public string FormattedTotalSpent { get; set; } = string.Empty;
    public string LastVisit { get; set; } = string.Empty;
}

public class AdminCustomerDetailViewModel
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string MemberSince { get; set; } = string.Empty;

    // Summary statistics
    public int TotalAppointments { get; set; }
    public int CompletedVisits { get; set; }
    public int CancelledVisits { get; set; }
    public decimal TotalSpent { get; set; }
    public string FormattedTotalSpent { get; set; } = "₱0.00";
    public string LastVisit { get; set; } = "No visits yet";

    // Navigation and sub-components
    public string ActiveTab { get; set; } = "profile"; // "profile", "history", "transactions", "security"
    public List<AdminCustomerAppointmentHistoryItemViewModel> Appointments { get; set; } = new();
    public AdminCustomerEditProfileModel EditForm { get; set; } = new();
}

public class AdminCustomerAppointmentHistoryItemViewModel
{
    public int Id { get; set; }
    public string ReferenceCode { get; set; } = string.Empty;
    public string BarberName { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string FormattedPrice { get; set; } = string.Empty;
    public Instant StartsAt { get; set; }
    public string ScheduledDateFormatted { get; set; } = string.Empty;
    public string ScheduledTime12h { get; set; } = string.Empty;
    public AppointmentStatus Status { get; set; }
    public string StatusString => Status.ToString().ToLowerInvariant();
    public string? Notes { get; set; }
    public List<AdminCustomerStatusHistoryItemViewModel> Timeline { get; set; } = new();
}

public class AdminCustomerStatusHistoryItemViewModel
{
    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;
    public string ChangedByRole { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string TimestampFormatted { get; set; } = string.Empty;
}

public class AdminCustomerEditProfileModel
{
    [Required(ErrorMessage = "Full Name is required.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Full Name must be between 2 and 150 characters.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email Address is required.")]
    [EmailAddress(ErrorMessage = "Invalid email address.")]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Invalid phone number.")]
    public string? Phone { get; set; }
}

public class AdminCustomerResetPasswordModel
{
    [Required(ErrorMessage = "New Password is required.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters long.")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm Password is required.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class AdminCustomerApiItemDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string MemberSince { get; set; } = string.Empty;
    public int TotalAppointments { get; set; }
    public int CompletedVisits { get; set; }
    public decimal TotalSpent { get; set; }
    public string LastVisit { get; set; } = string.Empty;
}

public class AdminCustomerPagedResultDto
{
    public List<AdminCustomerApiItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

