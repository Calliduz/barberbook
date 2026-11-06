using NodaTime;

namespace BarberBook.Web.Services;

public class EquipmentService : IEquipmentService
{
    private readonly List<EquipmentItem> _items;
    private readonly object _lock = new();
    private readonly IClock _clock;
    private readonly DateTimeZone _zone;

    public EquipmentService(IClock clock, DateTimeZone? zone = null)
    {
        _clock = clock;
        _zone = zone ?? DateTimeZoneProviders.Tzdb["Asia/Manila"];

        // Seed initial equipment matching HW-1 & HW-3 requirements
        _items = new List<EquipmentItem>
        {
            new() { Id = 1, Name = "Wahl Magic Clip Cordless", SerialNumber = "EQ-WMC-01", AssignedToBarber = "Ramon", Condition = "Good", LastInspectionDate = "Oct 28, 2026", NextInspectionDue = "Nov 28, 2026", LastNotes = "Blades oiled and zero-gapped." },
            new() { Id = 2, Name = "Andis Master Cordless Clipper", SerialNumber = "EQ-AMC-02", AssignedToBarber = "Dennis", Condition = "Good", LastInspectionDate = "Nov 02, 2026", NextInspectionDue = "Dec 02, 2026", LastNotes = "Motor runs smooth; fully sanitized." },
            new() { Id = 3, Name = "BaBylissPRO GoldFX Trimmer", SerialNumber = "EQ-BGT-03", AssignedToBarber = "Miguel", Condition = "Good", LastInspectionDate = "Nov 01, 2026", NextInspectionDue = "Dec 01, 2026", LastNotes = "T-blade sharp and aligned." },
            new() { Id = 4, Name = "Kamisori Classic Japanese Shears", SerialNumber = "EQ-KJS-04", AssignedToBarber = "Ramon", Condition = "Fair", LastInspectionDate = "Oct 15, 2026", NextInspectionDue = "Nov 15, 2026", LastNotes = "Tension screw adjusted; scheduled for sharpening." },
            new() { Id = 5, Name = "Feather Artist Club DX Straight Razor", SerialNumber = "EQ-FAR-05", AssignedToBarber = "Dennis", Condition = "Good", LastInspectionDate = "Oct 22, 2026", NextInspectionDue = "Nov 22, 2026", LastNotes = "Blade release spring inspected and cleaned." },
            new() { Id = 6, Name = "Takara Belmont Vintage Barber Chair #1", SerialNumber = "EQ-CH-01", AssignedToBarber = "Ramon", Condition = "Good", LastInspectionDate = "Oct 01, 2026", NextInspectionDue = "Dec 01, 2026", LastNotes = "Hydraulic lift inspected and operational." },
            new() { Id = 7, Name = "Oster Octane Heavy Duty Detachable Clipper", SerialNumber = "EQ-OHD-07", AssignedToBarber = "Dennis", Condition = "Good", LastInspectionDate = "Oct 25, 2026", NextInspectionDue = "Nov 25, 2026", LastNotes = "Spare battery pack charged and tested." },
            new() { Id = 8, Name = "Mizutani D-19 Precision Texturizing Shears", SerialNumber = "EQ-MP-08", AssignedToBarber = "Miguel", Condition = "Good", LastInspectionDate = "Nov 04, 2026", NextInspectionDue = "Dec 04, 2026", LastNotes = "Teeth checked for burs; cutting smoothly." }
        };
    }

    public IReadOnlyList<EquipmentItem> GetAll()
    {
        lock (_lock)
        {
            return _items.Select(Clone).ToList();
        }
    }

    public IReadOnlyList<EquipmentItem> GetByBarber(string barberDisplayName)
    {
        lock (_lock)
        {
            return _items
                .Where(i => string.Equals(i.AssignedToBarber, barberDisplayName, StringComparison.OrdinalIgnoreCase))
                .Select(Clone)
                .ToList();
        }
    }

    public bool LogInspection(int equipmentId, string condition, string? notes, string inspectedBy)
    {
        lock (_lock)
        {
            var item = _items.FirstOrDefault(i => i.Id == equipmentId);
            if (item == null) return false;

            var today = _clock.GetCurrentInstant().InZone(_zone).Date;
            var nextDue = today.PlusDays(30);

            item.Condition = condition;
            item.LastInspectionDate = today.ToString("MMM dd, yyyy", System.Globalization.CultureInfo.InvariantCulture);
            item.NextInspectionDue = nextDue.ToString("MMM dd, yyyy", System.Globalization.CultureInfo.InvariantCulture);
            item.LastNotes = string.IsNullOrWhiteSpace(notes) ? $"Inspected by {inspectedBy}" : notes.Trim();
            return true;
        }
    }

    public bool AssignEquipment(int equipmentId, string barberDisplayName)
    {
        lock (_lock)
        {
            var item = _items.FirstOrDefault(i => i.Id == equipmentId);
            if (item == null) return false;

            item.AssignedToBarber = barberDisplayName;
            return true;
        }
    }

    private static EquipmentItem Clone(EquipmentItem src) => new()
    {
        Id = src.Id,
        Name = src.Name,
        SerialNumber = src.SerialNumber,
        AssignedToBarber = src.AssignedToBarber,
        Condition = src.Condition,
        LastInspectionDate = src.LastInspectionDate,
        NextInspectionDue = src.NextInspectionDue,
        LastNotes = src.LastNotes
    };
}

