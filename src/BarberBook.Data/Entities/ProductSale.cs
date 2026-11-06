using NodaTime;

namespace BarberBook.Data.Entities;

public class ProductSale
{
    public int Id { get; set; }
    public Instant SoldAt { get; set; }
    public decimal TotalAmount { get; set; }

    public int? CustomerId { get; set; }
    public ApplicationUser? Customer { get; set; }

    public int? AppointmentId { get; set; }
    public Appointment? Appointment { get; set; }

    public string? Notes { get; set; }

    public int? RecordedById { get; set; }
    public ApplicationUser? RecordedBy { get; set; }

    public Instant CreatedAt { get; set; }

    public ICollection<ProductSaleItem> Items { get; set; } = new List<ProductSaleItem>();
}

