using NodaTime;

namespace BarberBook.Data.Entities;

public class DayOff
{
    public int Id { get; set; }
    public int BarberId { get; set; }
    public Barber Barber { get; set; } = null!;

    public LocalDate Date { get; set; }
    public string? Reason { get; set; }
}

