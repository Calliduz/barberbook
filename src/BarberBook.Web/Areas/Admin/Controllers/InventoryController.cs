using BarberBook.Data;
using BarberBook.Web.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BarberBook.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class InventoryController : Controller
{
    private readonly BarberBookDbContext _dbContext;

    public InventoryController(BarberBookDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("admin/inventory")]
    public async Task<IActionResult> Index()
    {
        var barbers = await _dbContext.Barbers.Where(b => b.IsActive).OrderBy(b => b.DisplayName).ToListAsync();

        // Sample initial equipment assignment catalog per HW-1/HW-3 reference
        var equipmentList = new List<AdminEquipmentItem>
        {
            new() { Id = 1, Name = "Wahl Magic Clip Cordless", SerialNumber = "EQ-WMC-01", AssignedToBarber = "Ramon", Condition = "Good", LastInspectionDate = "Oct 28, 2026", NextInspectionDue = "Nov 28, 2026" },
            new() { Id = 2, Name = "Andis Master Cordless Clipper", SerialNumber = "EQ-AMC-02", AssignedToBarber = "Dennis", Condition = "Good", LastInspectionDate = "Nov 02, 2026", NextInspectionDue = "Dec 02, 2026" },
            new() { Id = 3, Name = "BaBylissPRO GoldFX Trimmer", SerialNumber = "EQ-BGT-03", AssignedToBarber = "Miguel", Condition = "Good", LastInspectionDate = "Nov 01, 2026", NextInspectionDue = "Dec 01, 2026" },
            new() { Id = 4, Name = "Kamisori Classic Japanese Shears", SerialNumber = "EQ-KJS-04", AssignedToBarber = "Ramon", Condition = "Fair", LastInspectionDate = "Oct 15, 2026", NextInspectionDue = "Nov 15, 2026" },
            new() { Id = 5, Name = "Feather Artist Club DX Straight Razor", SerialNumber = "EQ-FAR-05", AssignedToBarber = "Dennis", Condition = "Good", LastInspectionDate = "Oct 22, 2026", NextInspectionDue = "Nov 22, 2026" },
            new() { Id = 6, Name = "Takara Belmont Vintage Barber Chair #1", SerialNumber = "EQ-CH-01", AssignedToBarber = "Ramon", Condition = "Good", LastInspectionDate = "Oct 01, 2026", NextInspectionDue = "Dec 01, 2026" }
        };

        // Retail product inventory per HW-2 reference
        var productList = new List<AdminRetailProductItem>
        {
            new() { Id = 1, Name = "Classic High-Shine Pomade (100g)", Sku = "PRD-POM-01", StockQuantity = 24, ReorderLevel = 10, RetailPrice = 450.00m, FormattedPrice = "₱450.00", Category = "Styling" },
            new() { Id = 2, Name = "Artisanal Cedarwood Beard Oil (50ml)", Sku = "PRD-BOIL-02", StockQuantity = 15, ReorderLevel = 8, RetailPrice = 380.00m, FormattedPrice = "₱380.00", Category = "Beard Care" },
            new() { Id = 3, Name = "Matte Texture Clay (85g)", Sku = "PRD-CLAY-03", StockQuantity = 6, ReorderLevel = 10, RetailPrice = 420.00m, FormattedPrice = "₱420.00", Category = "Styling" }, // Low stock!
            new() { Id = 4, Name = "Eucalyptus Hot Towel Shave Cream (150ml)", Sku = "PRD-SHAV-04", StockQuantity = 18, ReorderLevel = 5, RetailPrice = 320.00m, FormattedPrice = "₱320.00", Category = "Shaving" },
            new() { Id = 5, Name = "Bay Rum Soothing Aftershave Tonic (120ml)", Sku = "PRD-TON-05", StockQuantity = 4, ReorderLevel = 8, RetailPrice = 390.00m, FormattedPrice = "₱390.00", Category = "Aftercare" } // Low stock!
        };

        var model = new AdminInventoryViewModel
        {
            Equipment = equipmentList,
            Products = productList,
            LowStockCount = productList.Count(p => p.StockQuantity <= p.ReorderLevel),
            TotalAssignedTools = equipmentList.Count,
            TotalRetailSKUs = productList.Count
        };

        return View(model);
    }
}

public class AdminInventoryViewModel
{
    public List<AdminEquipmentItem> Equipment { get; set; } = new();
    public List<AdminRetailProductItem> Products { get; set; } = new();
    public int LowStockCount { get; set; }
    public int TotalAssignedTools { get; set; }
    public int TotalRetailSKUs { get; set; }
}

public class AdminEquipmentItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string AssignedToBarber { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;
    public string LastInspectionDate { get; set; } = string.Empty;
    public string NextInspectionDue { get; set; } = string.Empty;
}

public class AdminRetailProductItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public int StockQuantity { get; set; }
    public int ReorderLevel { get; set; }
    public decimal RetailPrice { get; set; }
    public string FormattedPrice { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}
