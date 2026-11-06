using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using BarberBook.Data.Enums;
using NodaTime;

namespace BarberBook.Web.Models.Inventory;

public class ProductDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("sku")]
    public string Sku { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("unit")]
    public string Unit { get; set; } = string.Empty;

    [JsonPropertyName("sell_price")]
    public decimal SellPrice { get; set; }

    [JsonPropertyName("formatted_sell_price")]
    public string FormattedSellPrice => $"₱{SellPrice:N2}";

    [JsonPropertyName("cost_price")]
    public decimal CostPrice { get; set; }

    [JsonPropertyName("formatted_cost_price")]
    public string FormattedCostPrice => $"₱{CostPrice:N2}";

    [JsonPropertyName("reorder_level")]
    public int ReorderLevel { get; set; }

    [JsonPropertyName("current_stock")]
    public int CurrentStock { get; set; }

    [JsonPropertyName("is_low_stock")]
    public bool IsLowStock => CurrentStock <= ReorderLevel;

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }
}

public class CreateProductRequest
{
    [Required]
    [MaxLength(50)]
    [JsonPropertyName("sku")]
    public string Sku { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [Required]
    [MaxLength(30)]
    [JsonPropertyName("unit")]
    public string Unit { get; set; } = "pcs";

    [Range(0, 1000000)]
    [JsonPropertyName("sell_price")]
    public decimal SellPrice { get; set; }

    [Range(0, 1000000)]
    [JsonPropertyName("cost_price")]
    public decimal CostPrice { get; set; }

    [Range(0, 10000)]
    [JsonPropertyName("reorder_level")]
    public int ReorderLevel { get; set; } = 5;

    [JsonPropertyName("initial_stock")]
    public int InitialStock { get; set; } = 0;
}

public class UpdateProductRequest
{
    [Required]
    [MaxLength(150)]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [Required]
    [MaxLength(30)]
    [JsonPropertyName("unit")]
    public string Unit { get; set; } = "pcs";

    [Range(0, 1000000)]
    [JsonPropertyName("sell_price")]
    public decimal SellPrice { get; set; }

    [Range(0, 1000000)]
    [JsonPropertyName("cost_price")]
    public decimal CostPrice { get; set; }

    [Range(0, 10000)]
    [JsonPropertyName("reorder_level")]
    public int ReorderLevel { get; set; } = 5;
}

public class AdjustStockRequest
{
    [Required]
    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [Required(ErrorMessage = "A reason for manual stock adjustment is mandatory.")]
    [MaxLength(500)]
    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}

public class StockMovementDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("product_id")]
    public int ProductId { get; set; }

    [JsonPropertyName("product_name")]
    public string ProductName { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("unit_amount")]
    public decimal? UnitAmount { get; set; }

    [JsonPropertyName("formatted_unit_amount")]
    public string? FormattedUnitAmount => UnitAmount.HasValue ? $"₱{UnitAmount.Value:N2}" : null;

    [JsonPropertyName("ref_type")]
    public string? RefType { get; set; }

    [JsonPropertyName("ref_id")]
    public int? RefId { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("created_by")]
    public string? CreatedByName { get; set; }

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = string.Empty;
}

public class EquipmentDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("asset_tag")]
    public string AssetTag { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("brand_model")]
    public string? BrandModel { get; set; }

    [JsonPropertyName("serial_no")]
    public string? SerialNo { get; set; }

    [JsonPropertyName("purchase_date")]
    public string? PurchaseDate { get; set; }

    [JsonPropertyName("purchase_cost")]
    public decimal? PurchaseCost { get; set; }

    [JsonPropertyName("formatted_purchase_cost")]
    public string? FormattedPurchaseCost => PurchaseCost.HasValue ? $"₱{PurchaseCost.Value:N2}" : null;

    [JsonPropertyName("condition")]
    public string Condition { get; set; } = "good";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "in_service";

    [JsonPropertyName("inspection_interval_days")]
    public int InspectionIntervalDays { get; set; } = 30;

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

public class CreateEquipmentRequest
{
    [Required]
    [MaxLength(50)]
    [JsonPropertyName("asset_tag")]
    public string AssetTag { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [MaxLength(150)]
    [JsonPropertyName("brand_model")]
    public string? BrandModel { get; set; }

    [MaxLength(100)]
    [JsonPropertyName("serial_no")]
    public string? SerialNo { get; set; }

    [JsonPropertyName("purchase_date")]
    public string? PurchaseDate { get; set; }

    [Range(0, 1000000)]
    [JsonPropertyName("purchase_cost")]
    public decimal? PurchaseCost { get; set; }

    [JsonPropertyName("condition")]
    public EquipmentCondition Condition { get; set; } = EquipmentCondition.Good;

    [JsonPropertyName("status")]
    public EquipmentStatus Status { get; set; } = EquipmentStatus.InService;

    [Range(1, 365)]
    [JsonPropertyName("inspection_interval_days")]
    public int InspectionIntervalDays { get; set; } = 30;

    [MaxLength(1000)]
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

public class UpdateEquipmentRequest
{
    [Required]
    [MaxLength(150)]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [MaxLength(150)]
    [JsonPropertyName("brand_model")]
    public string? BrandModel { get; set; }

    [MaxLength(100)]
    [JsonPropertyName("serial_no")]
    public string? SerialNo { get; set; }

    [JsonPropertyName("purchase_date")]
    public string? PurchaseDate { get; set; }

    [Range(0, 1000000)]
    [JsonPropertyName("purchase_cost")]
    public decimal? PurchaseCost { get; set; }

    [JsonPropertyName("condition")]
    public EquipmentCondition Condition { get; set; } = EquipmentCondition.Good;

    [JsonPropertyName("status")]
    public EquipmentStatus Status { get; set; } = EquipmentStatus.InService;

    [Range(1, 365)]
    [JsonPropertyName("inspection_interval_days")]
    public int InspectionIntervalDays { get; set; } = 30;

    [MaxLength(1000)]
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

public class AdminInventoryViewModel
{
    public List<ProductDto> Products { get; set; } = new();
    public List<EquipmentDto> Equipment { get; set; } = new();
    public int LowStockCount { get; set; }
    public int TotalProducts { get; set; }
    public int TotalEquipment { get; set; }
    public string ActiveTab { get; set; } = "products";
    public List<string> ProductCategories { get; set; } = new();
    public List<string> EquipmentCategories { get; set; } = new();
}

