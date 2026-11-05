using BarberBook.Data.Enums;
using Microsoft.AspNetCore.Identity;
using NodaTime;

namespace BarberBook.Data.Entities;

public class ApplicationUser : IdentityUser<int>
{
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Customer;
    public bool IsActive { get; set; } = true;
    public Instant CreatedAt { get; set; }

    // Navigation property
    public Barber? BarberProfile { get; set; }
    public ICollection<Appointment> CustomerAppointments { get; set; } = new List<Appointment>();
}
