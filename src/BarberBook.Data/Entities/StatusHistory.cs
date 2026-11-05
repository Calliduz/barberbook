using BarberBook.Data.Enums;
using NodaTime;

namespace BarberBook.Data.Entities;

public class StatusHistory
{
    public int Id { get; set; }

    public int AppointmentId { get; set; }
    public Appointment Appointment { get; set; } = null!;

    public AppointmentStatus? FromStatus { get; set; }
    public AppointmentStatus ToStatus { get; set; }

    public int? ChangedById { get; set; }
    public ApplicationUser? ChangedBy { get; set; }

    public string ChangedByRole { get; set; } = string.Empty;
    public string? Note { get; set; }
    public Instant ChangedAt { get; set; }
}

