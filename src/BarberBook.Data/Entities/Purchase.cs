using NodaTime;

namespace BarberBook.Data.Entities;

public class Purchase
{
    public int Id { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public Instant PurchasedAt { get; set; }
    public decimal TotalCost { get; set; }
    public string? Notes { get; set; }

    public int? CreatedById { get; set; }
    public ApplicationUser? CreatedBy { get; set; }

    public Instant CreatedAt { get; set; }

    public ICollection<PurchaseItem> Items { get; set; } = new List<PurchaseItem>();
}

