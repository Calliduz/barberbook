using BarberBook.Data.Enums;
using NodaTime;

namespace BarberBook.Data.Entities;

public class Appointment
{
    public int Id { get; set; }
    public string ReferenceCode { get; set; } = string.Empty;

    public int CustomerId { get; set; }
    public ApplicationUser Customer { get; set; } = null!;

    public int BarberId { get; set; }
    public Barber Barber { get; set; } = null!;

    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }

    public int ServiceId { get; set; }
    public Service Service { get; set; } = null!;

    public Instant StartsAt { get; set; }
    public Instant EndsAt { get; set; }

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;

    /// <summary>
    /// Price snapshot at the time of booking.
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// Service name snapshot at the time of booking.
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    public string? Notes { get; set; }
    public Instant CreatedAt { get; set; }
    public Instant UpdatedAt { get; set; }

    // Navigation property
    public ICollection<StatusHistory> StatusHistories { get; set; } = new List<StatusHistory>();
}

