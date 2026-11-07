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
    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Equipment> Equipment => Set<Equipment>();
    public DbSet<EquipmentAssignment> EquipmentAssignments => Set<EquipmentAssignment>();
    public DbSet<EquipmentCheck> EquipmentChecks => Set<EquipmentCheck>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();
    public DbSet<ProductSale> ProductSales => Set<ProductSale>();
    public DbSet<ProductSaleItem> ProductSaleItems => Set<ProductSaleItem>();
    public DbSet<Branch> Branches => Set<Branch>();

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
            entity.Property(e => e.BranchId).HasColumnName("branch_id");

            entity.HasOne(e => e.Branch)
                .WithMany(b => b.Barbers)
                .HasForeignKey(e => e.BranchId)
                .OnDelete(DeleteBehavior.SetNull);

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
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
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

            entity.HasOne(e => e.Branch)
                .WithMany(b => b.Appointments)
                .HasForeignKey(e => e.BranchId)
                .OnDelete(DeleteBehavior.SetNull);

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

        // 8. Products (HW-1, SC-10, AF-10)
        builder.Entity<Product>(entity =>
        {
            entity.ToTable("products");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Sku).HasColumnName("sku").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
            entity.Property(e => e.Category).HasColumnName("category").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Unit).HasColumnName("unit").HasMaxLength(30).IsRequired();
            entity.Property(e => e.SellPrice).HasColumnName("sell_price").HasColumnType("numeric(10,2)").IsRequired();
            entity.Property(e => e.CostPrice).HasColumnName("cost_price").HasColumnType("numeric(10,2)").IsRequired();
            entity.Property(e => e.ReorderLevel).HasColumnName("reorder_level").HasDefaultValue(5).IsRequired();
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);

            entity.HasIndex(e => e.Sku).IsUnique();
            entity.HasIndex(e => e.Category);
        });

        // 9. Stock Movements (HW-1, INV-03, INV-04)
        builder.Entity<StockMovement>(entity =>
        {
            entity.ToTable("stock_movements");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ProductId).HasColumnName("product_id").IsRequired();
            entity.Property(e => e.Type)
                .HasColumnName("type")
                .HasConversion(
                    v => v.ToString().ToLowerInvariant(),
                    v => Enum.Parse<StockMovementType>(v, true))
                .HasMaxLength(20)
                .IsRequired();
            entity.Property(e => e.Quantity).HasColumnName("quantity").IsRequired();
            entity.Property(e => e.UnitAmount).HasColumnName("unit_amount").HasColumnType("numeric(10,2)");
            entity.Property(e => e.RefType).HasColumnName("ref_type").HasMaxLength(50);
            entity.Property(e => e.RefId).HasColumnName("ref_id");
            entity.Property(e => e.Reason).HasColumnName("reason").HasMaxLength(500);
            entity.Property(e => e.CreatedById).HasColumnName("created_by_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

            entity.HasOne(e => e.Product)
                .WithMany(p => p.StockMovements)
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.CreatedBy)
                .WithMany()
                .HasForeignKey(e => e.CreatedById)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.ProductId);
            entity.HasIndex(e => e.CreatedAt);
        });

        // 10. Equipment (HW-1, SC-10, AF-9)
        builder.Entity<Equipment>(entity =>
        {
            entity.ToTable("equipment");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AssetTag).HasColumnName("asset_tag").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
            entity.Property(e => e.Category).HasColumnName("category").HasMaxLength(100).IsRequired();
            entity.Property(e => e.BrandModel).HasColumnName("brand_model").HasMaxLength(150);
            entity.Property(e => e.SerialNo).HasColumnName("serial_no").HasMaxLength(100);
            entity.Property(e => e.PurchaseDate).HasColumnName("purchase_date");
            entity.Property(e => e.PurchaseCost).HasColumnName("purchase_cost").HasColumnType("numeric(10,2)");
            entity.Property(e => e.Condition)
                .HasColumnName("condition")
                .HasConversion(
                    v => v.ToString().ToLowerInvariant(),
                    v => Enum.Parse<EquipmentCondition>(v, true))
                .HasMaxLength(20)
                .IsRequired();
            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasConversion(
                    v => v.ToString().ToLowerInvariant(),
                    v => Enum.Parse<EquipmentStatus>(v, true))
                .HasMaxLength(30)
                .IsRequired();
            entity.Property(e => e.InspectionIntervalDays).HasColumnName("inspection_interval_days").HasDefaultValue(30).IsRequired();
            entity.Property(e => e.Notes).HasColumnName("notes").HasMaxLength(1000);

            entity.HasIndex(e => e.AssetTag).IsUnique();
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.Status);
        });

        // 11. Equipment Assignments (HW-1, SC-10, AF-9, INV-01)
        builder.Entity<EquipmentAssignment>(entity =>
        {
            entity.ToTable("equipment_assignments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.EquipmentId).HasColumnName("equipment_id").IsRequired();
            entity.Property(e => e.BarberId).HasColumnName("barber_id").IsRequired();
            entity.Property(e => e.AssignedAt).HasColumnName("assigned_at").IsRequired();
            entity.Property(e => e.ReturnedAt).HasColumnName("returned_at");
            entity.Property(e => e.Notes).HasColumnName("notes").HasMaxLength(500);

            entity.HasOne(e => e.Equipment)
                .WithMany(eq => eq.Assignments)
                .HasForeignKey(e => e.EquipmentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Barber)
                .WithMany(b => b.EquipmentAssignments)
                .HasForeignKey(e => e.BarberId)
                .OnDelete(DeleteBehavior.Cascade);

            // Partial unique index: one active assignment per equipment where returned_at IS NULL (INV-01)
            entity.HasIndex(e => e.EquipmentId)
                .HasFilter("returned_at IS NULL")
                .IsUnique();

            entity.HasIndex(e => e.BarberId);
        });

        // 12. Equipment Checks (HW-3, SC-12, AF-13, INV-02)
        builder.Entity<EquipmentCheck>(entity =>
        {
            entity.ToTable("equipment_checks");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.EquipmentId).HasColumnName("equipment_id").IsRequired();
            entity.Property(e => e.CheckedById).HasColumnName("checked_by_id");
            entity.Property(e => e.CheckedAt).HasColumnName("checked_at").IsRequired();
            entity.Property(e => e.Condition)
                .HasColumnName("condition")
                .HasConversion(
                    v => v.ToString().ToLowerInvariant(),
                    v => Enum.Parse<EquipmentCondition>(v, true))
                .HasMaxLength(20)
                .IsRequired();
            entity.Property(e => e.Notes).HasColumnName("notes").HasMaxLength(1000);
            entity.Property(e => e.NextCheckDue).HasColumnName("next_check_due").IsRequired();

            entity.HasOne(e => e.Equipment)
                .WithMany(eq => eq.Checks)
                .HasForeignKey(e => e.EquipmentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.CheckedBy)
                .WithMany()
                .HasForeignKey(e => e.CheckedById)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => new { e.EquipmentId, e.CheckedAt });
            entity.HasIndex(e => e.NextCheckDue);
        });

        // 13. Purchases (HW-2, SC-11, AF-11, INV-03)
        builder.Entity<Purchase>(entity =>
        {
            entity.ToTable("purchases");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.SupplierName).HasColumnName("supplier_name").HasMaxLength(200).IsRequired();
            entity.Property(e => e.PurchasedAt).HasColumnName("purchased_at").IsRequired();
            entity.Property(e => e.TotalCost).HasColumnName("total_cost").HasColumnType("numeric(10,2)").IsRequired();
            entity.Property(e => e.Notes).HasColumnName("notes").HasMaxLength(1000);
            entity.Property(e => e.CreatedById).HasColumnName("created_by_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

            entity.HasOne(e => e.CreatedBy)
                .WithMany()
                .HasForeignKey(e => e.CreatedById)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(e => e.Items)
                .WithOne(i => i.Purchase)
                .HasForeignKey(i => i.PurchaseId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.PurchasedAt);
        });

        // 14. Purchase Items (HW-2, AF-11)
        builder.Entity<PurchaseItem>(entity =>
        {
            entity.ToTable("purchase_items");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.PurchaseId).HasColumnName("purchase_id").IsRequired();
            entity.Property(e => e.ProductId).HasColumnName("product_id").IsRequired();
            entity.Property(e => e.Quantity).HasColumnName("quantity").IsRequired();
            entity.Property(e => e.UnitCost).HasColumnName("unit_cost").HasColumnType("numeric(10,2)").IsRequired();
            entity.Property(e => e.LineTotal).HasColumnName("line_total").HasColumnType("numeric(10,2)").IsRequired();

            entity.HasOne(e => e.Product)
                .WithMany()
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.PurchaseId);
            entity.HasIndex(e => e.ProductId);
        });

        // 15. Product Sales (HW-2, SC-11, AF-12, INV-03, INV-04, INV-06)
        builder.Entity<ProductSale>(entity =>
        {
            entity.ToTable("product_sales");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.SoldAt).HasColumnName("sold_at").IsRequired();
            entity.Property(e => e.TotalAmount).HasColumnName("total_amount").HasColumnType("numeric(10,2)").IsRequired();
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.AppointmentId).HasColumnName("appointment_id");
            entity.Property(e => e.Notes).HasColumnName("notes").HasMaxLength(1000);
            entity.Property(e => e.RecordedById).HasColumnName("recorded_by_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

            entity.HasOne(e => e.Customer)
                .WithMany()
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Appointment)
                .WithMany()
                .HasForeignKey(e => e.AppointmentId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.RecordedBy)
                .WithMany()
                .HasForeignKey(e => e.RecordedById)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(e => e.Items)
                .WithOne(i => i.ProductSale)
                .HasForeignKey(i => i.ProductSaleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.SoldAt);
            entity.HasIndex(e => e.CustomerId);
        });

        // 16. Product Sale Items (HW-2, AF-12, INV-04)
        builder.Entity<ProductSaleItem>(entity =>
        {
            entity.ToTable("product_sale_items");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ProductSaleId).HasColumnName("product_sale_id").IsRequired();
            entity.Property(e => e.ProductId).HasColumnName("product_id").IsRequired();
            entity.Property(e => e.ProductName).HasColumnName("product_name").HasMaxLength(200).IsRequired();
            entity.Property(e => e.Quantity).HasColumnName("quantity").IsRequired();
            entity.Property(e => e.UnitPrice).HasColumnName("unit_price").HasColumnType("numeric(10,2)").IsRequired();
            entity.Property(e => e.LineTotal).HasColumnName("line_total").HasColumnType("numeric(10,2)").IsRequired();

            entity.HasOne(e => e.Product)
                .WithMany()
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.ProductSaleId);
            entity.HasIndex(e => e.ProductId);
        });

        // 17. Branches (Multi-Branch Management)
        builder.Entity<Branch>(entity =>
        {
            entity.ToTable("branches");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
            entity.Property(e => e.Address).HasColumnName("address").HasMaxLength(300).IsRequired();
            entity.Property(e => e.PhoneNumber).HasColumnName("phone_number").HasMaxLength(30);
            entity.Property(e => e.OpeningTime).HasColumnName("opening_time").IsRequired();
            entity.Property(e => e.ClosingTime).HasColumnName("closing_time").IsRequired();
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

            entity.HasIndex(e => e.Name);
        });
    }
}
