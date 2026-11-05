using Microsoft.AspNetCore.Identity;
using NodaTime;

namespace BarberBook.Data.Entities;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public Instant CreatedAt { get; set; }
}

