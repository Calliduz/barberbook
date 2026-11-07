using BarberBook.Web.Models.Appointment;
using BarberBook.Web.Models.Customer;

namespace BarberBook.Web.Models.Admin;

public class UpdateAppointmentStatusRequest
{
    public string To { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string? PaymentMethod { get; set; } // Cash, GCash, Maya, Card
    public List<AddOnProductItemDto>? AddOns { get; set; }
}

public class AddOnProductItemDto
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}

public class CreateWalkInAppointmentRequest
{
    public int BarberId { get; set; }
    public int ServiceId { get; set; }
    public int? BranchId { get; set; }
    public string? ClientName { get; set; }
    public string? ClientPhone { get; set; }
    public string? StartsAt { get; set; }
    public string? Notes { get; set; }
}

public class BulkUpdateStatusRequest
{
    public List<int> AppointmentIds { get; set; } = new();
    public string To { get; set; } = string.Empty;
    public string? Note { get; set; }
}

public class AdminAppointmentListQuery
{
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public string? Status { get; set; }
    public int? BarberId { get; set; }
    public int? CustomerId { get; set; }
    public int? ServiceId { get; set; }
    public int? BranchId { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SortBy { get; set; } = "date";
    public string? SortOrder { get; set; } = "desc";
}

public class AdminAppointmentItemDto
{
    public int Id { get; set; }
    public string ReferenceCode { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public int BarberId { get; set; }
    public string BarberName { get; set; } = string.Empty;
    public int? BranchId { get; set; }
    public string? BranchName { get; set; }
    public int ServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string FormattedPrice { get; set; } = string.Empty;
    public string StartsAt { get; set; } = string.Empty;
    public string EndsAt { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public string FormattedDate { get; set; } = string.Empty;
    public string FormattedTime { get; set; } = string.Empty;
    public string ScheduledDateString { get; set; } = string.Empty;
    public string ScheduledTimeString { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusDisplay { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
    public List<StatusTimelineItemDto> Timeline { get; set; } = new();
}

public class AdminAppointmentPagedResultDto
{
    public List<AdminAppointmentItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}

public class AdminRetailProductDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string FormattedPrice { get; set; } = string.Empty;
}

public class AdminAppointmentsViewModel
{
    public List<PublicServiceDto> Services { get; set; } = new();
    public List<PublicBarberDto> Barbers { get; set; } = new();
    public List<AdminBranchItemDto> Branches { get; set; } = new();
    public List<AdminRetailProductDto> Products { get; set; } = new();
    public string TodayDateString { get; set; } = string.Empty;
    public string InitialView { get; set; } = "list"; // list, day, week
    public string? InitialStatus { get; set; }
}

