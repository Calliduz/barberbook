using BarberBook.Web.Models.Customer;

namespace BarberBook.Web.Models.Appointment;

public class CreateAppointmentRequest
{
    public int ServiceId { get; set; }
    public int BarberId { get; set; }
    public string StartsAt { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class AppointmentDetailDto
{
    public int Id { get; set; }
    public string ReferenceCode { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public int BarberId { get; set; }
    public string BarberName { get; set; } = string.Empty;
    public int ServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string FormattedPrice { get; set; } = string.Empty;
    public string StartsAt { get; set; } = string.Empty;
    public string EndsAt { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public string FormattedDate { get; set; } = string.Empty;
    public string FormattedTime { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusDisplay { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
}

public class BookingWizardViewModel
{
    public List<PublicServiceDto> Services { get; set; } = new();
    public List<PublicBarberDto> Barbers { get; set; } = new();
    public int? PreSelectedServiceId { get; set; }
    public int? PreSelectedBarberId { get; set; }
    public string TodayDateString { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
}
