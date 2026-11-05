using BarberBook.Data.Entities;
using BarberBook.Data.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BarberBook.Data;

public class BarberBookDbContext : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>
{
    public DbSet<Barber> Barbers => Set<Barber>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<WorkingHours> WorkingHours => Set<WorkingHours>();
    public DbSet<DayOff> DaysOff => Set<DayOff>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<StatusHistory> StatusHistories => Set<StatusHistory>();

    public BarberBookDbContext(DbContextOptions<BarberBookDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // 1. Users (Identity)
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("users");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.FullName).HasColumnName("full_name").HasMaxLength(150).IsRequired();
            entity.Property(e => e.Role)
                .HasColumnName("role")
                .HasConversion(
                    v => v.ToString().ToLowerInvariant(),
                    v => Enum.Parse<UserRole>(v, true))
                .HasMaxLength(20)
                .IsRequired();
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasOne(u => u.BarberProfile)
                .WithOne(b => b.User)
                .HasForeignKey<Barber>(b => b.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<IdentityRole<int>>(entity => entity.ToTable("roles"));
        builder.Entity<IdentityUserRole<int>>(entity => entity.ToTable("user_roles"));
        builder.Entity<IdentityUserClaim<int>>(entity => entity.ToTable("user_claims"));
        builder.Entity<IdentityUserLogin<int>>(entity => entity.ToTable("user_logins"));
        builder.Entity<IdentityRoleClaim<int>>(entity => entity.ToTable("role_claims"));
        builder.Entity<IdentityUserToken<int>>(entity => entity.ToTable("user_tokens"));

        // 2. Barbers
        builder.Entity<Barber>(entity =>
        {
            entity.ToTable("barbers");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(e => e.DisplayName).HasColumnName("display_name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Bio).HasColumnName("bio").HasMaxLength(1000);
            entity.Property(e => e.PhotoUrl).HasColumnName("photo_url").HasMaxLength(500);
            entity.Property(e => e.Specialties).HasColumnName("specialties").HasMaxLength(255);
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);

            entity.HasIndex(e => e.UserId).IsUnique();
        });

        // 3. Services
        builder.Entity<Service>(entity =>
        {
            entity.ToTable("services");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Description).HasColumnName("description").HasMaxLength(500);
            entity.Property(e => e.Price).HasColumnName("price").HasColumnType("numeric(10,2)").IsRequired();
            entity.Property(e => e.DurationMinutes).HasColumnName("duration_minutes").IsRequired();
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        });

        // 4. Working Hours
        builder.Entity<WorkingHours>(entity =>
        {
            entity.ToTable("working_hours");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.BarberId).HasColumnName("barber_id").IsRequired();
            entity.Property(e => e.DayOfWeek).HasColumnName("day_of_week").IsRequired();
            entity.Property(e => e.StartTime).HasColumnName("start_time").IsRequired();
            entity.Property(e => e.EndTime).HasColumnName("end_time").IsRequired();

            entity.HasOne(e => e.Barber)
                .WithMany(b => b.WorkingHours)
                .HasForeignKey(e => e.BarberId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.BarberId, e.DayOfWeek });
        });

        // 5. Days Off
        builder.Entity<DayOff>(entity =>
        {
            entity.ToTable("days_off");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.BarberId).HasColumnName("barber_id").IsRequired();
            entity.Property(e => e.Date).HasColumnName("date").IsRequired();
            entity.Property(e => e.Reason).HasColumnName("reason").HasMaxLength(255);

            entity.HasOne(e => e.Barber)
                .WithMany(b => b.DaysOff)
                .HasForeignKey(e => e.BarberId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.BarberId, e.Date }).IsUnique();
        });

        // 6. Appointments
        builder.Entity<Appointment>(entity =>
        {
            entity.ToTable("appointments", t =>
            {
                t.HasCheckConstraint("ck_appointments_ends_at_after_starts_at", "ends_at > starts_at");
            });

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ReferenceCode).HasColumnName("reference_code").HasMaxLength(50).IsRequired();
            entity.Property(e => e.CustomerId).HasColumnName("customer_id").IsRequired();
            entity.Property(e => e.BarberId).HasColumnName("barber_id").IsRequired();
            entity.Property(e => e.ServiceId).HasColumnName("service_id").IsRequired();
            entity.Property(e => e.StartsAt).HasColumnName("starts_at").IsRequired();
            entity.Property(e => e.EndsAt).HasColumnName("ends_at").IsRequired();
            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasConversion(
                    v => v.ToString().ToLowerInvariant(),
                    v => Enum.Parse<AppointmentStatus>(v, true))
                .HasMaxLength(20)
                .IsRequired();
            entity.Property(e => e.Price).HasColumnName("price").HasColumnType("numeric(10,2)").IsRequired();
            entity.Property(e => e.ServiceName).HasColumnName("service_name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Notes).HasColumnName("notes").HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

            entity.HasOne(e => e.Customer)
                .WithMany(c => c.CustomerAppointments)
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Barber)
                .WithMany(b => b.Appointments)
                .HasForeignKey(e => e.BarberId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Service)
                .WithMany(s => s.Appointments)
                .HasForeignKey(e => e.ServiceId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.ReferenceCode).IsUnique();
            entity.HasIndex(e => new { e.BarberId, e.StartsAt });
            entity.HasIndex(e => new { e.CustomerId, e.StartsAt });
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.StartsAt);
        });

        // 7. Status History
        builder.Entity<StatusHistory>(entity =>
        {
            entity.ToTable("status_history");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AppointmentId).HasColumnName("appointment_id").IsRequired();
            entity.Property(e => e.FromStatus)
                .HasColumnName("from_status")
                .HasConversion(
                    v => v.HasValue ? v.Value.ToString().ToLowerInvariant() : null,
                    v => !string.IsNullOrEmpty(v) ? Enum.Parse<AppointmentStatus>(v, true) : (AppointmentStatus?)null)
                .HasMaxLength(20);
            entity.Property(e => e.ToStatus)
                .HasColumnName("to_status")
                .HasConversion(
                    v => v.ToString().ToLowerInvariant(),
                    v => Enum.Parse<AppointmentStatus>(v, true))
                .HasMaxLength(20)
                .IsRequired();
            entity.Property(e => e.ChangedById).HasColumnName("changed_by_id");
            entity.Property(e => e.ChangedByRole).HasColumnName("changed_by_role").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Note).HasColumnName("note").HasMaxLength(500);
            entity.Property(e => e.ChangedAt).HasColumnName("changed_at").IsRequired();

            entity.HasOne(e => e.Appointment)
                .WithMany(a => a.StatusHistories)
                .HasForeignKey(e => e.AppointmentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.ChangedBy)
                .WithMany()
                .HasForeignKey(e => e.ChangedById)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.AppointmentId);
        });
    }
}
