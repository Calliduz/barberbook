namespace BarberBook.Data.Entities;

public class Product
{
    public int Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal SellPrice { get; set; }
    public decimal CostPrice { get; set; }
    public int ReorderLevel { get; set; } = 5;
    public bool IsActive { get; set; } = true;

    public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
}

