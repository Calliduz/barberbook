using NodaTime;

namespace BarberBook.Data.Entities;

public class Branch
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public LocalTime OpeningTime { get; set; } = new LocalTime(9, 0);
    public LocalTime ClosingTime { get; set; } = new LocalTime(18, 0);
    public bool IsActive { get; set; } = true;
    public Instant CreatedAt { get; set; }

    // Navigation properties
    public ICollection<Barber> Barbers { get; set; } = new List<Barber>();
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
