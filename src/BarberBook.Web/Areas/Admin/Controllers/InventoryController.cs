using BarberBook.Data;
using BarberBook.Web.Common;
using BarberBook.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BarberBook.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class InventoryController : Controller
{
    private readonly BarberBookDbContext _dbContext;
    private readonly IEquipmentService _equipmentService;

    public InventoryController(BarberBookDbContext dbContext, IEquipmentService equipmentService)
    {
        _dbContext = dbContext;
        _equipmentService = equipmentService;
    }

    [HttpGet("admin/inventory")]
    public async Task<IActionResult> Index()
    {
        var barbers = await _dbContext.Barbers.Where(b => b.IsActive).OrderBy(b => b.DisplayName).ToListAsync();

        var equipmentItems = _equipmentService.GetAll();
        var equipmentList = equipmentItems.Select(e => new AdminEquipmentItem
        {
            Id = e.Id,
            Name = e.Name,
            SerialNumber = e.SerialNumber,
            AssignedToBarber = e.AssignedToBarber,
            Condition = e.Condition,
            LastInspectionDate = e.LastInspectionDate,
            NextInspectionDue = e.NextInspectionDue
        }).ToList();

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

