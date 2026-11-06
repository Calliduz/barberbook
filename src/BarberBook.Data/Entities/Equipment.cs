using BarberBook.Data.Enums;
using NodaTime;

namespace BarberBook.Data.Entities;

public class Equipment
{
    public int Id { get; set; }
    public string AssetTag { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? BrandModel { get; set; }
    public string? SerialNo { get; set; }
    public LocalDate? PurchaseDate { get; set; }
    public decimal? PurchaseCost { get; set; }
    public EquipmentCondition Condition { get; set; } = EquipmentCondition.Good;
    public EquipmentStatus Status { get; set; } = EquipmentStatus.InService;
    public int InspectionIntervalDays { get; set; } = 30;
    public string? Notes { get; set; }

    public ICollection<EquipmentAssignment> Assignments { get; set; } = new List<EquipmentAssignment>();
    public ICollection<EquipmentCheck> Checks { get; set; } = new List<EquipmentCheck>();
}

