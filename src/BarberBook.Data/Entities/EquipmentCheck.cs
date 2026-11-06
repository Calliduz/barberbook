using BarberBook.Data.Enums;
using NodaTime;

namespace BarberBook.Data.Entities;

public class EquipmentCheck
{
    public int Id { get; set; }

    public int EquipmentId { get; set; }
    public Equipment Equipment { get; set; } = null!;

    public int? CheckedById { get; set; }
    public ApplicationUser? CheckedBy { get; set; }

    public Instant CheckedAt { get; set; }
    public EquipmentCondition Condition { get; set; }
    public string? Notes { get; set; }
    public LocalDate NextCheckDue { get; set; }
}

