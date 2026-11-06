using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BarberBook.Web.Models.Inventory;

// ==========================================
// Purchases DTOs & Requests (HW-2, AF-11)
// ==========================================

public class CreatePurchaseItemRequest
{
    [Required]
    [JsonPropertyName("productId")]
    public int ProductId { get; set; }

    [Range(1, 100000, ErrorMessage = "Quantity must be at least 1.")]
    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [Range(0, 1000000, ErrorMessage = "Unit cost cannot be negative.")]
    [JsonPropertyName("unitCost")]
    public decimal UnitCost { get; set; }
}

public class CreatePurchaseRequest
{
    [Required(ErrorMessage = "Supplier name is required.")]
    [StringLength(200)]
    [JsonPropertyName("supplierName")]
    public string SupplierName { get; set; } = string.Empty;

    [JsonPropertyName("purchasedAt")]
    public string? PurchasedAt { get; set; }

    [StringLength(1000)]
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "At least one item must be included in a purchase.")]
    [JsonPropertyName("items")]
    public List<CreatePurchaseItemRequest> Items { get; set; } = new();

    [JsonPropertyName("updateCostPrice")]
    public bool UpdateCostPrice { get; set; } = true;
}

public class PurchaseItemDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("productId")]
    public int ProductId { get; set; }

    [JsonPropertyName("productName")]
    public string ProductName { get; set; } = string.Empty;

    [JsonPropertyName("sku")]
    public string Sku { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("unitCost")]
    public decimal UnitCost { get; set; }

    [JsonPropertyName("formattedUnitCost")]
    public string FormattedUnitCost { get; set; } = string.Empty;

    [JsonPropertyName("lineTotal")]
    public decimal LineTotal { get; set; }

    [JsonPropertyName("formattedLineTotal")]
    public string FormattedLineTotal { get; set; } = string.Empty;
}

public class PurchaseDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("supplierName")]
    public string SupplierName { get; set; } = string.Empty;

    [JsonPropertyName("purchasedAt")]
    public string PurchasedAt { get; set; } = string.Empty;

    [JsonPropertyName("totalCost")]
    public decimal TotalCost { get; set; }

    [JsonPropertyName("formattedTotalCost")]
    public string FormattedTotalCost { get; set; } = string.Empty;

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("createdById")]
    public int? CreatedById { get; set; }

    [JsonPropertyName("createdByName")]
    public string? CreatedByName { get; set; }

    [JsonPropertyName("itemsCount")]
    public int ItemsCount => Items.Count;

    [JsonPropertyName("items")]
    public List<PurchaseItemDto> Items { get; set; } = new();
}

// ==========================================
// Product Sales DTOs & Requests (HW-2, AF-12)
// ==========================================

public class CreateProductSaleItemRequest
{
    [Required]
    [JsonPropertyName("productId")]
    public int ProductId { get; set; }

    [Range(1, 100000, ErrorMessage = "Quantity must be at least 1.")]
    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }
}

public class CreateProductSaleRequest
{
    [JsonPropertyName("soldAt")]
    public string? SoldAt { get; set; }

    [JsonPropertyName("customerId")]
    public int? CustomerId { get; set; }

    [JsonPropertyName("appointmentId")]
    public int? AppointmentId { get; set; }

    [StringLength(1000)]
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "At least one item must be included in a product sale.")]
    [JsonPropertyName("items")]
    public List<CreateProductSaleItemRequest> Items { get; set; } = new();
}

public class ProductSaleItemDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("productId")]
    public int ProductId { get; set; }

    [JsonPropertyName("productName")]
    public string ProductName { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("unitPrice")]
    public decimal UnitPrice { get; set; }

    [JsonPropertyName("formattedUnitPrice")]
    public string FormattedUnitPrice { get; set; } = string.Empty;

    [JsonPropertyName("lineTotal")]
    public decimal LineTotal { get; set; }

    [JsonPropertyName("formattedLineTotal")]
    public string FormattedLineTotal { get; set; } = string.Empty;
}

public class ProductSaleDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("soldAt")]
    public string SoldAt { get; set; } = string.Empty;

    [JsonPropertyName("totalAmount")]
    public decimal TotalAmount { get; set; }

    [JsonPropertyName("formattedTotalAmount")]
    public string FormattedTotalAmount { get; set; } = string.Empty;

    [JsonPropertyName("customerId")]
    public int? CustomerId { get; set; }

    [JsonPropertyName("customerName")]
    public string? CustomerName { get; set; }

    [JsonPropertyName("appointmentId")]
    public int? AppointmentId { get; set; }

    [JsonPropertyName("appointmentReference")]
    public string? AppointmentReference { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("recordedById")]
    public int? RecordedById { get; set; }

    [JsonPropertyName("recordedByName")]
    public string? RecordedByName { get; set; }

    [JsonPropertyName("itemsCount")]
    public int ItemsCount => Items.Count;

    [JsonPropertyName("items")]
    public List<ProductSaleItemDto> Items { get; set; } = new();
}

// ==========================================
// View Models for Admin UI Tabs
// ==========================================

public class AdminPurchasesViewModel
{
    public IReadOnlyList<PurchaseDto> Purchases { get; set; } = new List<PurchaseDto>();
    public IReadOnlyList<EquipmentDto> Products { get; set; } = new List<EquipmentDto>(); // Products available for purchasing
    public IReadOnlyList<ProductDto> ActiveProducts { get; set; } = new List<ProductDto>();
    public decimal TotalPurchasedCost { get; set; }
    public string FormattedTotalPurchasedCost { get; set; } = "₱0.00";
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
}

public class AdminProductSalesViewModel
{
    public IReadOnlyList<ProductSaleDto> Sales { get; set; } = new List<ProductSaleDto>();
    public IReadOnlyList<ProductDto> Products { get; set; } = new List<ProductDto>();
    public IReadOnlyList<CustomerLookupDto> Customers { get; set; } = new List<CustomerLookupDto>();
    public decimal TotalSalesAmount { get; set; }
    public string FormattedTotalSalesAmount { get; set; } = "₱0.00";
    public List<ProductSalesSummaryItemDto> PerProductSummary { get; set; } = new();
    public int? FilterProductId { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
}

public class CustomerLookupDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
}

public class ProductSalesSummaryItemDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int QuantitySold { get; set; }
    public decimal TotalRevenue { get; set; }
    public string FormattedTotalRevenue { get; set; } = "₱0.00";
}
