namespace BarberBook.Data.Entities;

public class Barber
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public string DisplayName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? PhotoUrl { get; set; }
    public string? Specialties { get; set; }
    public bool IsActive { get; set; } = true;

    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }

    /// <summary>
    /// Barber commission share (e.g. 0.50m for 50%, 0.60m for 60%). The shop owner retains (1 - CommissionRate).
    /// </summary>
    public decimal CommissionRate { get; set; } = 0.50m;

    // Navigation properties
    public ICollection<WorkingHours> WorkingHours { get; set; } = new List<WorkingHours>();
    public ICollection<DayOff> DaysOff { get; set; } = new List<DayOff>();
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    public ICollection<EquipmentAssignment> EquipmentAssignments { get; set; } = new List<EquipmentAssignment>();
}

