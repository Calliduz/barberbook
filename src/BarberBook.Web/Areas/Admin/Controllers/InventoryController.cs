using BarberBook.Data;
using BarberBook.Data.Enums;
using BarberBook.Web.Common;
using BarberBook.Web.Models.Inventory;
using BarberBook.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using NodaTime.Text;

namespace BarberBook.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class InventoryController : Controller
{
    private readonly IInventoryService _inventoryService;
    private readonly IEquipmentService _equipmentService;
    private readonly IPurchaseSaleService _purchaseSaleService;
    private readonly BarberBookDbContext _dbContext;
    private readonly ILogger<InventoryController> _logger;

    public InventoryController(
        IInventoryService inventoryService,
        IEquipmentService equipmentService,
        IPurchaseSaleService purchaseSaleService,
        BarberBookDbContext dbContext,
        ILogger<InventoryController> logger)
    {
        _inventoryService = inventoryService;
        _equipmentService = equipmentService;
        _purchaseSaleService = purchaseSaleService;
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

        // Fetch live monitoring and active barber assignments (P16, HW-1, HW-3)
        var monitoring = await _equipmentService.GetEquipmentMonitoringAsync();
        var monitoringMap = monitoring.PoorCondition
            .Concat(monitoring.UnderMaintenanceOrOutOfService)
            .Concat(monitoring.OverdueForInspection)
            .Concat(monitoring.Healthy)
            .GroupBy(i => i.Id)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var eq in equipment)
        {
            if (monitoringMap.TryGetValue(eq.Id, out var m))
            {
                eq.CurrentBarberId = m.CurrentBarberId;
                eq.CurrentBarberName = m.CurrentBarberName;
                eq.LastInspectedAt = m.LastInspectedAt;
                eq.NextInspectionDue = m.NextCheckDue;
                eq.DaysOverdue = m.DaysOverdue;
                eq.IsFlagged = m.IsFlagged;
                eq.FlagReasons = m.FlagReasons;
            }
        }

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

        var activeBarbers = await _dbContext.Barbers
            .Where(b => b.IsActive)
            .OrderBy(b => b.DisplayName)
            .Select(b => new BarberEquipmentSelectionItem
            {
                Id = b.Id,
                DisplayName = b.DisplayName,
                Specialties = b.Specialties,
                ActiveToolCount = b.EquipmentAssignments.Count(a => a.ReturnedAt == null)
            })
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
            EquipmentCategories = equipmentCategories,
            Barbers = activeBarbers,
            Monitoring = monitoring
        };

        return View(model);
    }

    [HttpGet("admin/equipment/per-barber")]
    public async Task<IActionResult> EquipmentPerBarber([FromQuery] int? barberId = null)
    {
        var activeBarbers = await _dbContext.Barbers
            .Where(b => b.IsActive)
            .OrderBy(b => b.DisplayName)
            .Select(b => new BarberEquipmentSelectionItem
            {
                Id = b.Id,
                DisplayName = b.DisplayName,
                Specialties = b.Specialties,
                ActiveToolCount = b.EquipmentAssignments.Count(a => a.ReturnedAt == null)
            })
            .ToListAsync();

        int selectedId = barberId ?? activeBarbers.FirstOrDefault()?.Id ?? 0;

        var activeAssignments = selectedId > 0
            ? await _equipmentService.GetBarberAssignmentsAsync(selectedId, activeOnly: true)
            : new List<EquipmentAssignmentDto>();

        var pastAssignments = selectedId > 0
            ? (await _equipmentService.GetBarberAssignmentsAsync(selectedId, activeOnly: false))
                .Where(a => !a.IsActive)
                .ToList()
            : new List<EquipmentAssignmentDto>();

        var unassigned = await _equipmentService.GetUnassignedEquipmentAsync();

        var model = new AdminEquipmentPerBarberViewModel
        {
            SelectedBarberId = selectedId,
            Barbers = activeBarbers,
            ActiveAssignments = activeAssignments.ToList(),
            PastAssignments = pastAssignments,
            UnassignedEquipment = unassigned.ToList()
        };

        return View(model);
    }

    [HttpGet("admin/equipment/quality")]
    [HttpGet("admin/equipment/monitoring")]
    [HttpGet("admin/inventory/equipment/monitoring")]
    public async Task<IActionResult> EquipmentQuality()
    {
        var monitoring = await _equipmentService.GetEquipmentMonitoringAsync();
        var recentChecks = await _equipmentService.GetRecentChecksAsync(30);
        var allEquipment = await _inventoryService.GetEquipmentAsync();

        var model = new AdminEquipmentQualityViewModel
        {
            Monitoring = monitoring,
            RecentChecks = recentChecks.ToList(),
            AllEquipment = allEquipment.Where(e => e.Status != "retired").ToList()
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

    [HttpPost("admin/inventory/equipment/{id:int}/assign")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignEquipment(int id, [FromForm] int barberId, [FromForm] string? notes = null)
    {
        var (success, error, _) = await _equipmentService.AssignEquipmentAsync(id, barberId, notes);
        if (!success)
        {
            TempData["ErrorMessage"] = error ?? "Failed to assign equipment.";
        }
        else
        {
            TempData["SuccessMessage"] = "Equipment assigned successfully.";
        }

        return RedirectToAction(nameof(Index), new { tab = "equipment" });
    }

    [HttpPost("admin/inventory/equipment/{id:int}/return")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReturnEquipment(int id, [FromForm] string? notes = null)
    {
        var (success, error) = await _equipmentService.ReturnEquipmentAsync(id, notes);
        if (!success)
        {
            TempData["ErrorMessage"] = error ?? "Failed to return equipment.";
        }
        else
        {
            TempData["SuccessMessage"] = "Equipment returned to shop storage.";
        }

        return RedirectToAction(nameof(Index), new { tab = "equipment" });
    }

    [HttpPost("admin/inventory/equipment/{id:int}/inspect")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> InspectEquipment(
        int id,
        [FromForm] EquipmentCondition condition,
        [FromForm] string? notes = null,
        [FromForm] string? nextCheckDue = null)
    {
        var userId = User.GetUserId();
        LocalDate? nextDue = null;
        if (!string.IsNullOrWhiteSpace(nextCheckDue) && LocalDatePattern.Iso.Parse(nextCheckDue.Trim()).TryGetValue(LocalDate.MinIsoValue, out var parsed))
        {
            nextDue = parsed;
        }

        var (success, error, _) = await _equipmentService.LogInspectionAsync(id, condition, notes, nextDue, userId);
        if (!success)
        {
            TempData["ErrorMessage"] = error ?? "Failed to record equipment inspection.";
        }
        else
        {
            TempData["SuccessMessage"] = "Equipment inspection logged successfully.";
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

    // ==========================================
    // 3. Equipment Assignments & Checks APIs (P16)
    // ==========================================

    [HttpPost("api/admin/equipment/{id:int}/assign")]
    public async Task<IActionResult> AssignEquipmentApi(int id, [FromBody] AssignEquipmentRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ApiErrorResponse("VALIDATION_ERROR", "Validation failed.", ModelState));
        }

        var (success, error, assignment) = await _equipmentService.AssignEquipmentAsync(id, request.BarberId, request.Notes);
        if (!success)
        {
            if (error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
            {
                return NotFound(new ApiErrorResponse("NOT_FOUND", error));
            }
            return BadRequest(new ApiErrorResponse("ASSIGNMENT_ERROR", error ?? "Failed to assign equipment."));
        }

        return Ok(assignment);
    }

    [HttpPost("api/admin/equipment/{id:int}/return")]
    public async Task<IActionResult> ReturnEquipmentApi(int id, [FromBody] ReturnEquipmentRequest? request = null)
    {
        var (success, error) = await _equipmentService.ReturnEquipmentAsync(id, request?.Notes);
        if (!success)
        {
            return BadRequest(new ApiErrorResponse("RETURN_ERROR", error ?? "Failed to return equipment."));
        }

        return Ok(new { message = "Equipment returned successfully." });
    }

    [HttpGet("api/admin/equipment/{id:int}/assignments")]
    public async Task<IActionResult> GetEquipmentAssignmentsApi(int id)
    {
        var equipment = await _inventoryService.GetEquipmentByIdAsync(id);
        if (equipment == null)
        {
            return NotFound(new ApiErrorResponse("EQUIPMENT_NOT_FOUND", $"Equipment with ID {id} not found."));
        }

        var history = await _equipmentService.GetEquipmentAssignmentHistoryAsync(id);
        return Ok(history);
    }

    [HttpPost("api/admin/equipment/{id:int}/inspect")]
    public async Task<IActionResult> InspectEquipmentApi(int id, [FromBody] LogEquipmentCheckRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ApiErrorResponse("VALIDATION_ERROR", "Validation failed.", ModelState));
        }

        LocalDate? nextDue = null;
        if (!string.IsNullOrWhiteSpace(request.NextCheckDue) &&
            LocalDatePattern.Iso.Parse(request.NextCheckDue.Trim()).TryGetValue(LocalDate.MinIsoValue, out var parsed))
        {
            nextDue = parsed;
        }

        var userId = User.GetUserId();
        var (success, error, check) = await _equipmentService.LogInspectionAsync(id, request.Condition, request.Notes, nextDue, userId);
        if (!success)
        {
            return NotFound(new ApiErrorResponse("INSPECTION_ERROR", error ?? "Failed to record inspection."));
        }

        return Ok(check);
    }

    [HttpGet("api/admin/equipment/{id:int}/checks")]
    public async Task<IActionResult> GetEquipmentChecksApi(int id)
    {
        var equipment = await _inventoryService.GetEquipmentByIdAsync(id);
        if (equipment == null)
        {
            return NotFound(new ApiErrorResponse("EQUIPMENT_NOT_FOUND", $"Equipment with ID {id} not found."));
        }

        var checks = await _equipmentService.GetEquipmentChecksAsync(id);
        return Ok(checks);
    }

    [HttpGet("api/admin/equipment/monitoring")]
    public async Task<IActionResult> GetEquipmentMonitoringApi()
    {
        var monitoring = await _equipmentService.GetEquipmentMonitoringAsync();
        return Ok(monitoring);
    }

    [HttpGet("api/admin/barbers/{barberId:int}/equipment")]
    public async Task<IActionResult> GetBarberEquipmentApi(int barberId)
    {
        var barber = await _dbContext.Barbers.FirstOrDefaultAsync(b => b.Id == barberId);
        if (barber == null)
        {
            return NotFound(new ApiErrorResponse("BARBER_NOT_FOUND", $"Barber with ID {barberId} not found."));
        }

        var assignments = await _equipmentService.GetBarberAssignmentsAsync(barberId, activeOnly: false);
        return Ok(assignments);
    }

    // ==========================================
    // Purchases View & Form Handlers (HW-2, AF-11)
    // ==========================================

    [HttpGet("admin/purchases")]
    public async Task<IActionResult> Purchases(
        [FromQuery] string? startDate = null,
        [FromQuery] string? endDate = null)
    {
        var start = ParseLocalDate(startDate);
        var end = ParseLocalDate(endDate);

        var purchases = await _purchaseSaleService.GetPurchasesAsync(start, end);
        var activeProducts = await _inventoryService.GetProductsAsync(includeInactive: false);

        var totalCost = purchases.Sum(p => p.TotalCost);

        var model = new AdminPurchasesViewModel
        {
            Purchases = purchases,
            ActiveProducts = activeProducts,
            TotalPurchasedCost = totalCost,
            FormattedTotalPurchasedCost = FormattingHelpers.FormatPeso(totalCost),
            StartDate = startDate,
            EndDate = endDate
        };

        return View("Purchases", model);
    }

    [HttpPost("admin/purchases")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePurchaseMvc([FromBody] CreatePurchaseRequest request)
    {
        var userId = User.GetUserId();
        var (success, error, purchase) = await _purchaseSaleService.CreatePurchaseAsync(request, userId);
        if (!success)
        {
            return BadRequest(new { error = error ?? "Failed to record purchase." });
        }

        TempData["SuccessMessage"] = $"Purchase from {purchase?.SupplierName} recorded successfully. Inventory updated.";
        return Ok(new { success = true, purchaseId = purchase?.Id });
    }

    // ==========================================
    // Product Sales View & Form Handlers (HW-2, AF-12)
    // ==========================================

    [HttpGet("admin/product-sales")]
    public async Task<IActionResult> ProductSales(
        [FromQuery] string? startDate = null,
        [FromQuery] string? endDate = null,
        [FromQuery] int? productId = null)
    {
        var start = ParseLocalDate(startDate);
        var end = ParseLocalDate(endDate);

        var sales = await _purchaseSaleService.GetProductSalesAsync(start, end, productId);
        var activeProducts = await _inventoryService.GetProductsAsync(includeInactive: false);
        var perProductSummary = await _purchaseSaleService.GetProductSalesSummaryAsync(start, end);

        var customers = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Role == UserRole.Customer && u.IsActive)
            .OrderBy(u => u.FullName)
            .Select(u => new CustomerLookupDto
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email,
                Phone = u.PhoneNumber
            })
            .ToListAsync();

        var totalSales = sales.Sum(s => s.TotalAmount);

        var model = new AdminProductSalesViewModel
        {
            Sales = sales,
            Products = activeProducts,
            Customers = customers,
            TotalSalesAmount = totalSales,
            FormattedTotalSalesAmount = FormattingHelpers.FormatPeso(totalSales),
            PerProductSummary = perProductSummary,
            FilterProductId = productId,
            StartDate = startDate,
            EndDate = endDate
        };

        return View("ProductSales", model);
    }

    [HttpPost("admin/product-sales")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateProductSaleMvc([FromBody] CreateProductSaleRequest request)
    {
        var userId = User.GetUserId();
        var (success, error, errorCode, sale) = await _purchaseSaleService.CreateProductSaleAsync(request, userId);
        if (!success)
        {
            if (errorCode == "INSUFFICIENT_STOCK")
            {
                return StatusCode(StatusCodes.Status409Conflict, new { error, code = errorCode });
            }
            return BadRequest(new { error = error ?? "Failed to record product sale.", code = errorCode });
        }

        TempData["SuccessMessage"] = $"Counter product sale recorded successfully ({FormattingHelpers.FormatPeso(sale?.TotalAmount ?? 0)}).";
        return Ok(new { success = true, saleId = sale?.Id });
    }

    // ==========================================
    // Purchases JSON API (Admin Only) (HW-2, AF-11)
    // ==========================================

    [HttpPost("api/admin/purchases")]
    public async Task<IActionResult> CreatePurchaseApi([FromBody] CreatePurchaseRequest request)
    {
        if (request == null)
        {
            return BadRequest(new ApiErrorResponse("BAD_REQUEST", "Request body cannot be null."));
        }

        var userId = User.GetUserId();
        var (success, error, purchase) = await _purchaseSaleService.CreatePurchaseAsync(request, userId);
        if (!success)
        {
            return BadRequest(new ApiErrorResponse("PURCHASE_FAILED", error ?? "Could not record purchase."));
        }

        return StatusCode(StatusCodes.Status201Created, purchase);
    }

    [HttpGet("api/admin/purchases")]
    public async Task<IActionResult> GetPurchasesApi(
        [FromQuery] string? startDate = null,
        [FromQuery] string? endDate = null)
    {
        var start = ParseLocalDate(startDate);
        var end = ParseLocalDate(endDate);
        var purchases = await _purchaseSaleService.GetPurchasesAsync(start, end);
        return Ok(purchases);
    }

    [HttpGet("api/admin/purchases/{id:int}")]
    public async Task<IActionResult> GetPurchaseByIdApi(int id)
    {
        var purchase = await _purchaseSaleService.GetPurchaseByIdAsync(id);
        if (purchase == null)
        {
            return NotFound(new ApiErrorResponse("PURCHASE_NOT_FOUND", $"Purchase with ID {id} not found."));
        }
        return Ok(purchase);
    }

    // ==========================================
    // Product Sales JSON API (Admin Only) (HW-2, AF-12)
    // ==========================================

    [HttpPost("api/admin/product-sales")]
    public async Task<IActionResult> CreateProductSaleApi([FromBody] CreateProductSaleRequest request)
    {
        if (request == null)
        {
            return BadRequest(new ApiErrorResponse("BAD_REQUEST", "Request body cannot be null."));
        }

        var userId = User.GetUserId();
        var (success, error, errorCode, sale) = await _purchaseSaleService.CreateProductSaleAsync(request, userId);
        if (!success)
        {
            if (errorCode == "INSUFFICIENT_STOCK")
            {
                return StatusCode(StatusCodes.Status409Conflict,
                    new ApiErrorResponse("INSUFFICIENT_STOCK", error ?? "Requested quantity exceeds available stock."));
            }
            return BadRequest(new ApiErrorResponse(errorCode ?? "SALE_FAILED", error ?? "Could not record product sale."));
        }

        return StatusCode(StatusCodes.Status201Created, sale);
    }

    [HttpGet("api/admin/product-sales")]
    public async Task<IActionResult> GetProductSalesApi(
        [FromQuery] string? startDate = null,
        [FromQuery] string? endDate = null,
        [FromQuery] int? productId = null)
    {
        var start = ParseLocalDate(startDate);
        var end = ParseLocalDate(endDate);
        var sales = await _purchaseSaleService.GetProductSalesAsync(start, end, productId);
        return Ok(sales);
    }

    [HttpGet("api/admin/product-sales/{id:int}")]
    public async Task<IActionResult> GetProductSaleByIdApi(int id)
    {
        var sale = await _purchaseSaleService.GetProductSaleByIdAsync(id);
        if (sale == null)
        {
            return NotFound(new ApiErrorResponse("SALE_NOT_FOUND", $"Product sale with ID {id} not found."));
        }
        return Ok(sale);
    }

    [HttpGet("api/admin/product-sales/summary")]
    public async Task<IActionResult> GetProductSalesSummaryApi(
        [FromQuery] string? startDate = null,
        [FromQuery] string? endDate = null)
    {
        var start = ParseLocalDate(startDate);
        var end = ParseLocalDate(endDate);
        var summary = await _purchaseSaleService.GetProductSalesSummaryAsync(start, end);
        return Ok(summary);
    }

    private static LocalDate? ParseLocalDate(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr)) return null;
        var pattern = LocalDatePattern.Iso;
        var parseResult = pattern.Parse(dateStr.Trim());
        return parseResult.Success ? parseResult.Value : null;
    }
}

public class RetireEquipmentRequest
{
    public string? Reason { get; set; }
}
