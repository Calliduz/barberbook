using BarberBook.Data;
using BarberBook.Data.Enums;
using BarberBook.Web.Common;
using BarberBook.Web.Models.Inventory;
using BarberBook.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BarberBook.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class InventoryController : Controller
{
    private readonly IInventoryService _inventoryService;
    private readonly BarberBookDbContext _dbContext;
    private readonly ILogger<InventoryController> _logger;

    public InventoryController(
        IInventoryService inventoryService,
        BarberBookDbContext dbContext,
        ILogger<InventoryController> logger)
    {
        _inventoryService = inventoryService;
        _dbContext = dbContext;
        _logger = logger;
    }

    // ==========================================
    // 1. MVC View Endpoints
    // ==========================================

    [HttpGet("admin/inventory")]
    public async Task<IActionResult> Index(
        [FromQuery] string tab = "products",
        [FromQuery] string? search = null,
        [FromQuery] string? category = null,
        [FromQuery] EquipmentStatus? status = null,
        [FromQuery] EquipmentCondition? condition = null)
    {
        var products = await _inventoryService.GetProductsAsync(
            search: tab == "products" ? search : null,
            category: tab == "products" ? category : null,
            includeInactive: true);

        var equipment = await _inventoryService.GetEquipmentAsync(
            search: tab == "equipment" ? search : null,
            category: tab == "equipment" ? category : null,
            status: tab == "equipment" ? status : null,
            condition: tab == "equipment" ? condition : null);

        var productCategories = await _dbContext.Products
            .Select(p => p.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        var equipmentCategories = await _dbContext.Equipment
            .Select(e => e.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        var model = new AdminInventoryViewModel
        {
            Products = products.ToList(),
            Equipment = equipment.ToList(),
            LowStockCount = products.Count(p => p.IsActive && p.IsLowStock),
            TotalProducts = products.Count(p => p.IsActive),
            TotalEquipment = equipment.Count(e => e.Status != "retired"),
            ActiveTab = tab.ToLowerInvariant() == "equipment" ? "equipment" : "products",
            ProductCategories = productCategories,
            EquipmentCategories = equipmentCategories
        };

        return View(model);
    }

    [HttpPost("admin/inventory/products/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateProduct([FromForm] CreateProductRequest request)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Please fill in all required fields accurately.";
            return RedirectToAction(nameof(Index), new { tab = "products" });
        }

        try
        {
            var userId = User.GetUserId();
            await _inventoryService.CreateProductAsync(request, userId);
            TempData["SuccessMessage"] = $"Product '{request.Name}' ({request.Sku}) created successfully.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { tab = "products" });
    }

    [HttpPost("admin/inventory/products/{id:int}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProduct(int id, [FromForm] UpdateProductRequest request)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Validation failed for product update.";
            return RedirectToAction(nameof(Index), new { tab = "products" });
        }

        var result = await _inventoryService.UpdateProductAsync(id, request);
        if (result == null)
        {
            TempData["ErrorMessage"] = "Product not found.";
        }
        else
        {
            TempData["SuccessMessage"] = $"Product '{result.Name}' updated successfully.";
        }

        return RedirectToAction(nameof(Index), new { tab = "products" });
    }

    [HttpPost("admin/inventory/products/{id:int}/adjust")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdjustStock(int id, [FromForm] AdjustStockRequest request)
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(request.Reason))
        {
            TempData["ErrorMessage"] = "A non-zero quantity and mandatory reason are required.";
            return RedirectToAction(nameof(Index), new { tab = "products" });
        }

        var userId = User.GetUserId();
        var (success, error, _) = await _inventoryService.AdjustStockAsync(id, request.Quantity, request.Reason, userId);
        if (!success)
        {
            TempData["ErrorMessage"] = error ?? "Failed to adjust stock.";
        }
        else
        {
            TempData["SuccessMessage"] = $"Stock adjusted successfully ({request.Quantity:+0;-0;0}).";
        }

        return RedirectToAction(nameof(Index), new { tab = "products" });
    }

    [HttpPost("admin/inventory/products/{id:int}/toggle-status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleProductStatus(int id, [FromForm] bool isActive)
    {
        var success = await _inventoryService.SetProductActiveAsync(id, isActive);
        if (!success)
        {
            TempData["ErrorMessage"] = "Product not found.";
        }
        else
        {
            TempData["SuccessMessage"] = isActive ? "Product activated." : "Product deactivated.";
        }

        return RedirectToAction(nameof(Index), new { tab = "products" });
    }

    [HttpPost("admin/inventory/equipment/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEquipment([FromForm] CreateEquipmentRequest request)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Please fill in all required equipment fields.";
            return RedirectToAction(nameof(Index), new { tab = "equipment" });
        }

        try
        {
            var result = await _inventoryService.CreateEquipmentAsync(request);
            TempData["SuccessMessage"] = $"Equipment '{result.Name}' ({result.AssetTag}) registered.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { tab = "equipment" });
    }

    [HttpPost("admin/inventory/equipment/{id:int}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateEquipment(int id, [FromForm] UpdateEquipmentRequest request)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Validation failed for equipment update.";
            return RedirectToAction(nameof(Index), new { tab = "equipment" });
        }

        var result = await _inventoryService.UpdateEquipmentAsync(id, request);
        if (result == null)
        {
            TempData["ErrorMessage"] = "Equipment not found.";
        }
        else
        {
            TempData["SuccessMessage"] = $"Equipment '{result.Name}' updated successfully.";
        }

        return RedirectToAction(nameof(Index), new { tab = "equipment" });
    }

    [HttpPost("admin/inventory/equipment/{id:int}/retire")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RetireEquipment(int id, [FromForm] string? reason)
    {
        var success = await _inventoryService.RetireEquipmentAsync(id, reason);
        if (!success)
        {
            TempData["ErrorMessage"] = "Equipment not found.";
        }
        else
        {
            TempData["SuccessMessage"] = "Equipment marked as retired.";
        }

        return RedirectToAction(nameof(Index), new { tab = "equipment" });
    }

    // ==========================================
    // 2. JSON REST API Endpoints (Admin Only)
    // ==========================================

    [HttpGet("api/admin/inventory/products")]
    public async Task<IActionResult> GetProductsApi(
        [FromQuery] string? search = null,
        [FromQuery] string? category = null,
        [FromQuery] bool includeInactive = false)
    {
        var products = await _inventoryService.GetProductsAsync(search, category, includeInactive);
        return Ok(products);
    }

    [HttpGet("api/admin/inventory/products/{id:int}")]
    public async Task<IActionResult> GetProductByIdApi(int id)
    {
        var product = await _inventoryService.GetProductByIdAsync(id);
        if (product == null)
        {
            return NotFound(new ApiErrorResponse("PRODUCT_NOT_FOUND", $"Product with ID {id} not found."));
        }
        return Ok(product);
    }

    [HttpPost("api/admin/inventory/products")]
    public async Task<IActionResult> CreateProductApi([FromBody] CreateProductRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ApiErrorResponse("VALIDATION_ERROR", "Validation failed.", ModelState));
        }

        try
        {
            var userId = User.GetUserId();
            var created = await _inventoryService.CreateProductAsync(request, userId);
            return CreatedAtAction(nameof(GetProductByIdApi), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ApiErrorResponse("DUPLICATE_SKU", ex.Message));
        }
    }

    [HttpPut("api/admin/inventory/products/{id:int}")]
    public async Task<IActionResult> UpdateProductApi(int id, [FromBody] UpdateProductRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ApiErrorResponse("VALIDATION_ERROR", "Validation failed.", ModelState));
        }

        var updated = await _inventoryService.UpdateProductAsync(id, request);
        if (updated == null)
        {
            return NotFound(new ApiErrorResponse("PRODUCT_NOT_FOUND", $"Product with ID {id} not found."));
        }
        return Ok(updated);
    }

    [HttpDelete("api/admin/inventory/products/{id:int}")]
    public async Task<IActionResult> DeactivateProductApi(int id)
    {
        var success = await _inventoryService.SetProductActiveAsync(id, false);
        if (!success)
        {
            return NotFound(new ApiErrorResponse("PRODUCT_NOT_FOUND", $"Product with ID {id} not found."));
        }
        return Ok(new { message = "Product deactivated successfully." });
    }

    [HttpPost("api/admin/inventory/products/{id:int}/activate")]
    public async Task<IActionResult> ActivateProductApi(int id)
    {
        var success = await _inventoryService.SetProductActiveAsync(id, true);
        if (!success)
        {
            return NotFound(new ApiErrorResponse("PRODUCT_NOT_FOUND", $"Product with ID {id} not found."));
        }
        return Ok(new { message = "Product activated successfully." });
    }

    [HttpPost("api/admin/inventory/products/{id:int}/adjust")]
    public async Task<IActionResult> AdjustStockApi(int id, [FromBody] AdjustStockRequest request)
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(request.Reason))
        {
            return BadRequest(new ApiErrorResponse("VALIDATION_ERROR", "A non-zero quantity and mandatory reason are required."));
        }

        var userId = User.GetUserId();
        var (success, error, movement) = await _inventoryService.AdjustStockAsync(id, request.Quantity, request.Reason, userId);
        if (!success)
        {
            if (error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
            {
                return NotFound(new ApiErrorResponse("PRODUCT_NOT_FOUND", error));
            }
            return BadRequest(new ApiErrorResponse("STOCK_CANNOT_BE_NEGATIVE", error ?? "Invalid stock adjustment."));
        }

        return Ok(movement);
    }

    [HttpGet("api/admin/inventory/products/{id:int}/movements")]
    public async Task<IActionResult> GetProductMovementsApi(int id)
    {
        var product = await _inventoryService.GetProductByIdAsync(id);
        if (product == null)
        {
            return NotFound(new ApiErrorResponse("PRODUCT_NOT_FOUND", $"Product with ID {id} not found."));
        }

        var movements = await _inventoryService.GetProductMovementsAsync(id);
        return Ok(movements);
    }

    [HttpGet("api/admin/inventory/equipment")]
    public async Task<IActionResult> GetEquipmentApi(
        [FromQuery] string? search = null,
        [FromQuery] string? category = null,
        [FromQuery] EquipmentStatus? status = null,
        [FromQuery] EquipmentCondition? condition = null)
    {
        var items = await _inventoryService.GetEquipmentAsync(search, category, status, condition);
        return Ok(items);
    }

    [HttpGet("api/admin/inventory/equipment/{id:int}")]
    public async Task<IActionResult> GetEquipmentByIdApi(int id)
    {
        var item = await _inventoryService.GetEquipmentByIdAsync(id);
        if (item == null)
        {
            return NotFound(new ApiErrorResponse("EQUIPMENT_NOT_FOUND", $"Equipment with ID {id} not found."));
        }
        return Ok(item);
    }

    [HttpPost("api/admin/inventory/equipment")]
    public async Task<IActionResult> CreateEquipmentApi([FromBody] CreateEquipmentRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ApiErrorResponse("VALIDATION_ERROR", "Validation failed.", ModelState));
        }

        try
        {
            var created = await _inventoryService.CreateEquipmentAsync(request);
            return CreatedAtAction(nameof(GetEquipmentByIdApi), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ApiErrorResponse("DUPLICATE_ASSET_TAG", ex.Message));
        }
    }

    [HttpPut("api/admin/inventory/equipment/{id:int}")]
    public async Task<IActionResult> UpdateEquipmentApi(int id, [FromBody] UpdateEquipmentRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ApiErrorResponse("VALIDATION_ERROR", "Validation failed.", ModelState));
        }

        var updated = await _inventoryService.UpdateEquipmentAsync(id, request);
        if (updated == null)
        {
            return NotFound(new ApiErrorResponse("EQUIPMENT_NOT_FOUND", $"Equipment with ID {id} not found."));
        }
        return Ok(updated);
    }

    [HttpDelete("api/admin/inventory/equipment/{id:int}")]
    public async Task<IActionResult> RetireEquipmentApi(int id, [FromBody] RetireEquipmentRequest? request = null)
    {
        var success = await _inventoryService.RetireEquipmentAsync(id, request?.Reason);
        if (!success)
        {
            return NotFound(new ApiErrorResponse("EQUIPMENT_NOT_FOUND", $"Equipment with ID {id} not found."));
        }
        return Ok(new { message = "Equipment retired successfully." });
    }
}

public class RetireEquipmentRequest
{
    public string? Reason { get; set; }
}
