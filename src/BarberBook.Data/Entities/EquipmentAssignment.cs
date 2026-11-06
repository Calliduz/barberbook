using NodaTime;

namespace BarberBook.Data.Entities;

public class EquipmentAssignment
{
    public int Id { get; set; }

    public int EquipmentId { get; set; }
    public Equipment Equipment { get; set; } = null!;

    public int BarberId { get; set; }
    public Barber Barber { get; set; } = null!;

    public Instant AssignedAt { get; set; }
    public Instant? ReturnedAt { get; set; }

    public string? Notes { get; set; }
}

