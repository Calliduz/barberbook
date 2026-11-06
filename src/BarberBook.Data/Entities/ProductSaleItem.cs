namespace BarberBook.Data.Entities;

public class ProductSaleItem
{
    public int Id { get; set; }

    public int ProductSaleId { get; set; }
    public ProductSale ProductSale { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

