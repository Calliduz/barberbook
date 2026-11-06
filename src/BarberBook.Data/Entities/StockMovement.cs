using BarberBook.Data.Enums;
using NodaTime;

namespace BarberBook.Data.Entities;

public class StockMovement
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public StockMovementType Type { get; set; }
    public int Quantity { get; set; }
    public decimal? UnitAmount { get; set; }
    public string? RefType { get; set; }
    public int? RefId { get; set; }
    public string? Reason { get; set; }

    public int? CreatedById { get; set; }
    public ApplicationUser? CreatedBy { get; set; }

    public Instant CreatedAt { get; set; }
}

