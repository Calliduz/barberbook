namespace BarberBook.Data.Entities;

public class Service
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int DurationMinutes { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation property
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}

