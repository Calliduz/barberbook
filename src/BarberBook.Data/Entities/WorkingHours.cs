using NodaTime;

namespace BarberBook.Data.Entities;

public class WorkingHours
{
    public int Id { get; set; }
    public int BarberId { get; set; }
    public Barber Barber { get; set; } = null!;

    /// <summary>
    /// 0 = Sunday, 1 = Monday, ..., 6 = Saturday (matching standard System.DayOfWeek)
    /// </summary>
    public int DayOfWeek { get; set; }
    public LocalTime StartTime { get; set; }
    public LocalTime EndTime { get; set; }
}

