using BarberBook.Data.Entities;
using BarberBook.Data.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NodaTime;

namespace BarberBook.Data.Seeding;

public static class DatabaseSeeder
{
    private static readonly DateTimeZone ManilaZone = DateTimeZoneProviders.Tzdb["Asia/Manila"];

    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DatabaseSeeder));

        logger.LogInformation("Starting idempotent database seeding...");

        // Ensure database and extensions
        await context.Database.MigrateAsync();

        // 1. Seed Roles
        string[] roleNames = ["Customer", "Barber", "Admin"];
        foreach (var roleName in roleNames)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole<int>(roleName));
            }
        }

        // Configuration values
        var adminEmail = config["Admin:Email"] ?? "admin@barberbook.local";
        var adminPassword = config["Admin:Password"] ?? "Admin@BarberBook2026!";
        var demoPassword = config["Seed:DemoPassword"] ?? "Demo@BarberBook2026!";

        var now = SystemClock.Instance.GetCurrentInstant();

        // 2. Seed Admin User
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FullName = "Shop Administrator",
                Role = UserRole.Admin,
                IsActive = true,
                CreatedAt = now
            };
            var result = await userManager.CreateAsync(adminUser, adminPassword);
            if (!result.Succeeded)
            {
                throw new Exception($"Failed to create admin user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }

        // 3. Seed Barbers: Ramon, Dennis, Miguel
        var barberData = new[]
        {
            (Name: "Ramon", Email: "ramon@barberbook.local", Bio: "Master barber specializing in classic cuts and beard styling.", Specialties: "Classic Cuts, Beard Sculpting"),
            (Name: "Dennis", Email: "dennis@barberbook.local", Bio: "Precision stylist with 10+ years experience in modern fades.", Specialties: "Modern Fades, Shaves"),
            (Name: "Miguel", Email: "miguel@barberbook.local", Bio: "Creative colorist and artistic stylist.", Specialties: "Hair Color, Texture Styling")
        };

        var barberMap = new Dictionary<string, Barber>();

        foreach (var (name, email, bio, specialties) in barberData)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FullName = name,
                    Role = UserRole.Barber,
                    IsActive = true,
                    CreatedAt = now
                };
                var result = await userManager.CreateAsync(user, demoPassword);
                if (!result.Succeeded)
                {
                    throw new Exception($"Failed to create barber user {name}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
                await userManager.AddToRoleAsync(user, "Barber");
            }

            var barber = await context.Barbers.Include(b => b.WorkingHours).FirstOrDefaultAsync(b => b.UserId == user.Id);
            if (barber == null)
            {
                barber = new Barber
                {
                    UserId = user.Id,
                    DisplayName = name,
                    Bio = bio,
                    Specialties = specialties,
                    IsActive = true
                };
                context.Barbers.Add(barber);
                await context.SaveChangesAsync();
            }

            // Seed Working Hours: Mon-Sat (1..6) 9:00 AM - 6:00 PM
            if (!barber.WorkingHours.Any())
            {
                for (int day = 1; day <= 6; day++)
                {
                    context.WorkingHours.Add(new WorkingHours
                    {
                        BarberId = barber.Id,
                        DayOfWeek = day,
                        StartTime = new LocalTime(9, 0),
                        EndTime = new LocalTime(18, 0)
                    });
                }
                await context.SaveChangesAsync();
            }

            barberMap[name] = barber;
        }

        // 4. Seed Services
        var servicesData = new[]
        {
            (Name: "Haircut", Price: 150.00m, Duration: 30, Description: "Standard professional haircut and styling"),
            (Name: "Haircut + Shave", Price: 220.00m, Duration: 60, Description: "Haircut paired with hot towel straight razor shave"),
            (Name: "Beard Trim", Price: 100.00m, Duration: 30, Description: "Precision beard shaping, edging, and conditioning"),
            (Name: "Hair Color", Price: 500.00m, Duration: 90, Description: "Full professional hair coloring and treatment")
        };

        var serviceMap = new Dictionary<string, Service>();

        foreach (var (name, price, duration, desc) in servicesData)
        {
            var service = await context.Services.FirstOrDefaultAsync(s => s.Name == name);
            if (service == null)
            {
                service = new Service
                {
                    Name = name,
                    Price = price,
                    DurationMinutes = duration,
                    Description = desc,
                    IsActive = true
                };
                context.Services.Add(service);
                await context.SaveChangesAsync();
            }
            serviceMap[name] = service;
        }

        // 5. Seed Customers
        var customerData = new[]
        {
            (Name: "Juan Dela Cruz", Email: "juan@barberbook.local", Phone: "09171234567"),
            (Name: "Mark Santos", Email: "mark@barberbook.local", Phone: "09172345678"),
            (Name: "Paolo Reyes", Email: "paolo@barberbook.local", Phone: "09173456789"),
            (Name: "Carlo Mendoza", Email: "carlo@barberbook.local", Phone: "09174567890"),
            (Name: "Adrian Cruz", Email: "adrian@barberbook.local", Phone: "09175678901"),
            (Name: "Leo Garcia", Email: "leo@barberbook.local", Phone: "09176789012")
        };

        var customerMap = new Dictionary<string, ApplicationUser>();

        foreach (var (name, email, phone) in customerData)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FullName = name,
                    PhoneNumber = phone,
                    Role = UserRole.Customer,
                    IsActive = true,
                    CreatedAt = now
                };
                var result = await userManager.CreateAsync(user, demoPassword);
                if (!result.Succeeded)
                {
                    throw new Exception($"Failed to create customer user {name}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
                await userManager.AddToRoleAsync(user, "Customer");
            }
            customerMap[name] = user;
        }

        // Helper to convert Manila local date & time to UTC Instant
        static Instant ManilaToUtc(int year, int month, int day, int hour, int minute)
        {
            var localDateTime = new LocalDateTime(year, month, day, hour, minute);
            var zonedDateTime = localDateTime.InZoneStrictly(ManilaZone);
            return zonedDateTime.ToInstant();
        }

        // 6. Seed Appointments
        // Defined in §7.3 of BARBERBOOK_REFERENCE.md:
        // Daily Sales Report for Sep 14, 2026 (6 completed = ₱1,340.00)
        // Earlier appointments for Juan Dela Cruz (Aug 30 ₱220.00, Aug 16 ₱150.00)
        // Total August 2026 completed = ₱370.00
        // Total Spent Juan Dela Cruz = ₱520.00 (3 transactions)
        // Two noise appointments on Sep 14 (1 cancelled, 1 pending) that do NOT count in sales.

        var appointmentSeedList = new[]
        {
            // Sep 14, 2026 completed appointments
            new { Ref = "BB-20260914-0001", Customer = "Juan Dela Cruz", Barber = "Ramon", Service = "Haircut", Year = 2026, Month = 9, Day = 14, Hour = 9, Minute = 0, Status = AppointmentStatus.Completed },
            new { Ref = "BB-20260914-0002", Customer = "Mark Santos", Barber = "Dennis", Service = "Haircut + Shave", Year = 2026, Month = 9, Day = 14, Hour = 10, Minute = 0, Status = AppointmentStatus.Completed },
            new { Ref = "BB-20260914-0003", Customer = "Paolo Reyes", Barber = "Ramon", Service = "Beard Trim", Year = 2026, Month = 9, Day = 14, Hour = 11, Minute = 30, Status = AppointmentStatus.Completed },
            new { Ref = "BB-20260914-0004", Customer = "Carlo Mendoza", Barber = "Miguel", Service = "Haircut", Year = 2026, Month = 9, Day = 14, Hour = 13, Minute = 0, Status = AppointmentStatus.Completed },
            new { Ref = "BB-20260914-0005", Customer = "Adrian Cruz", Barber = "Dennis", Service = "Hair Color", Year = 2026, Month = 9, Day = 14, Hour = 14, Minute = 30, Status = AppointmentStatus.Completed },
            new { Ref = "BB-20260914-0006", Customer = "Leo Garcia", Barber = "Miguel", Service = "Haircut + Shave", Year = 2026, Month = 9, Day = 14, Hour = 16, Minute = 0, Status = AppointmentStatus.Completed },

            // Juan Dela Cruz earlier August history
            new { Ref = "BB-20260830-0001", Customer = "Juan Dela Cruz", Barber = "Dennis", Service = "Haircut + Shave", Year = 2026, Month = 8, Day = 30, Hour = 10, Minute = 0, Status = AppointmentStatus.Completed },
            new { Ref = "BB-20260816-0001", Customer = "Juan Dela Cruz", Barber = "Ramon", Service = "Haircut", Year = 2026, Month = 8, Day = 16, Hour = 9, Minute = 0, Status = AppointmentStatus.Completed },

            // Noise appointments on 2026-09-14: Cancelled & Pending
            new { Ref = "BB-20260914-0007", Customer = "Mark Santos", Barber = "Ramon", Service = "Haircut", Year = 2026, Month = 9, Day = 14, Hour = 14, Minute = 0, Status = AppointmentStatus.Cancelled },
            new { Ref = "BB-20260914-0008", Customer = "Paolo Reyes", Barber = "Miguel", Service = "Beard Trim", Year = 2026, Month = 9, Day = 14, Hour = 14, Minute = 0, Status = AppointmentStatus.Pending }
        };

        foreach (var item in appointmentSeedList)
        {
            var existing = await context.Appointments.FirstOrDefaultAsync(a => a.ReferenceCode == item.Ref);
            if (existing != null)
            {
                continue;
            }

            var service = serviceMap[item.Service];
            var barber = barberMap[item.Barber];
            var customer = customerMap[item.Customer];

            var startsAt = ManilaToUtc(item.Year, item.Month, item.Day, item.Hour, item.Minute);
            var endsAt = startsAt.Plus(Duration.FromMinutes(service.DurationMinutes));

            var appointment = new Appointment
            {
                ReferenceCode = item.Ref,
                CustomerId = customer.Id,
                BarberId = barber.Id,
                ServiceId = service.Id,
                StartsAt = startsAt,
                EndsAt = endsAt,
                Status = item.Status,
                Price = service.Price, // Snapshot price at booking
                ServiceName = service.Name, // Snapshot service name
                Notes = $"Seeded test appointment {item.Ref}",
                CreatedAt = startsAt.Minus(Duration.FromDays(2)),
                UpdatedAt = startsAt
            };

            context.Appointments.Add(appointment);
            await context.SaveChangesAsync();

            // Seed status history for the appointment
            var history = new StatusHistory
            {
                AppointmentId = appointment.Id,
                FromStatus = item.Status == AppointmentStatus.Pending ? null : AppointmentStatus.Pending,
                ToStatus = item.Status,
                ChangedById = adminUser.Id,
                ChangedByRole = "Admin",
                Note = "Initial seeded appointment creation",
                ChangedAt = appointment.UpdatedAt
            };

            context.StatusHistories.Add(history);
            await context.SaveChangesAsync();
        }

        logger.LogInformation("Database seeding completed successfully.");
    }
}

