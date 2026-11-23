using System.Globalization;
using BarberBook.Data;
using BarberBook.Data.Entities;
using BarberBook.Data.Enums;
using BarberBook.Web.Models.Inventory;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using NodaTime.Text;

namespace BarberBook.Web.Services;

public class InventoryService : IInventoryService
{
    private readonly BarberBookDbContext _dbContext;
    private readonly IClock _clock;
    private readonly DateTimeZone _zone;

    public InventoryService(BarberBookDbContext dbContext, IClock clock, DateTimeZone? zone = null)
    {
        _dbContext = dbContext;
        _clock = clock;
        _zone = zone ?? DateTimeZoneProviders.Tzdb["Asia/Manila"];
    }

    public async Task<IReadOnlyList<ProductDto>> GetProductsAsync(string? search = null, string? category = null, bool includeInactive = false, CancellationToken ct = default)
    {
        var query = _dbContext.Products.AsNoTracking().AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(p => p.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(p => p.Name.ToLower().Contains(term) || p.Sku.ToLower().Contains(term) || p.Category.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            var cat = category.Trim().ToLowerInvariant();
            query = query.Where(p => p.Category.ToLower() == cat);
        }

        var products = await query
            .OrderBy(p => p.Category)
            .ThenBy(p => p.Name)
            .ToListAsync(ct);

        var pIds = products.Select(p => p.Id).ToList();
        var movements = await _dbContext.StockMovements
            .AsNoTracking()
            .Where(sm => pIds.Contains(sm.ProductId))
            .ToListAsync(ct);
        var stockMap = movements
            .GroupBy(sm => sm.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(sm => sm.Quantity));

        return products.Select(p => new ProductDto
        {
            Id = p.Id,
            Sku = p.Sku,
            Name = p.Name,
            Category = p.Category,
            Unit = p.Unit,
            SellPrice = p.SellPrice,
            CostPrice = p.CostPrice,
            ReorderLevel = p.ReorderLevel,
            CurrentStock = stockMap.GetValueOrDefault(p.Id, 0),
            IsActive = p.IsActive
        }).ToList();
    }

    public async Task<ProductDto?> GetProductByIdAsync(int id, CancellationToken ct = default)
    {
        var product = await _dbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (product == null) return null;

        var movements = await _dbContext.StockMovements
            .AsNoTracking()
            .Where(sm => sm.ProductId == id)
            .ToListAsync(ct);
        var stock = movements.Sum(sm => sm.Quantity);

        return new ProductDto
        {
            Id = product.Id,
            Sku = product.Sku,
            Name = product.Name,
            Category = product.Category,
            Unit = product.Unit,
            SellPrice = product.SellPrice,
            CostPrice = product.CostPrice,
            ReorderLevel = product.ReorderLevel,
            CurrentStock = stock,
            IsActive = product.IsActive
        };
    }

    public async Task<ProductDto> CreateProductAsync(CreateProductRequest request, int? userId, CancellationToken ct = default)
    {
        var normalizedSku = request.Sku.Trim().ToUpperInvariant();

        var exists = await _dbContext.Products.AnyAsync(p => p.Sku.ToUpper() == normalizedSku, ct);
        if (exists)
        {
            throw new InvalidOperationException($"Product with SKU '{request.Sku}' already exists.");
        }

        var product = new Product
        {
            Sku = normalizedSku,
            Name = request.Name.Trim(),
            Category = request.Category.Trim(),
            Unit = request.Unit.Trim(),
            SellPrice = request.SellPrice,
            CostPrice = request.CostPrice,
            ReorderLevel = request.ReorderLevel,
            IsActive = true
        };

        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync(ct);

        int currentStock = 0;
        if (request.InitialStock > 0)
        {
            var initialMovement = new StockMovement
            {
                ProductId = product.Id,
                Type = StockMovementType.Adjustment,
                Quantity = request.InitialStock,
                UnitAmount = product.CostPrice,
                Reason = "Initial inventory stock count",
                CreatedById = userId,
                CreatedAt = _clock.GetCurrentInstant()
            };
            _dbContext.StockMovements.Add(initialMovement);
            await _dbContext.SaveChangesAsync(ct);
            currentStock = request.InitialStock;
        }

        return new ProductDto
        {
            Id = product.Id,
            Sku = product.Sku,
            Name = product.Name,
            Category = product.Category,
            Unit = product.Unit,
            SellPrice = product.SellPrice,
            CostPrice = product.CostPrice,
            ReorderLevel = product.ReorderLevel,
            CurrentStock = currentStock,
            IsActive = product.IsActive
        };
    }

    public async Task<ProductDto?> UpdateProductAsync(int id, UpdateProductRequest request, CancellationToken ct = default)
    {
        var product = await _dbContext.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product == null) return null;

        product.Name = request.Name.Trim();
        product.Category = request.Category.Trim();
        product.Unit = request.Unit.Trim();
        product.SellPrice = request.SellPrice;
        product.CostPrice = request.CostPrice;
        product.ReorderLevel = request.ReorderLevel;

        await _dbContext.SaveChangesAsync(ct);

        var currentStock = (await _dbContext.StockMovements
            .Where(sm => sm.ProductId == id)
            .ToListAsync(ct))
            .Sum(sm => sm.Quantity);

        return new ProductDto
        {
            Id = product.Id,
            Sku = product.Sku,
            Name = product.Name,
            Category = product.Category,
            Unit = product.Unit,
            SellPrice = product.SellPrice,
            CostPrice = product.CostPrice,
            ReorderLevel = product.ReorderLevel,
            CurrentStock = currentStock,
            IsActive = product.IsActive
        };
    }

    public async Task<bool> SetProductActiveAsync(int id, bool isActive, CancellationToken ct = default)
    {
        var product = await _dbContext.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product == null) return false;

        product.IsActive = isActive;
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<(bool Success, string? Error, StockMovementDto? Movement)> AdjustStockAsync(
        int productId, int quantity, string reason, int? userId, CancellationToken ct = default)
    {
        if (quantity == 0)
        {
            return (false, "Adjustment quantity cannot be zero.", null);
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return (false, "A reason for manual stock adjustment is mandatory.", null);
        }

        var product = await _dbContext.Products.FirstOrDefaultAsync(p => p.Id == productId, ct);
        if (product == null)
        {
            return (false, "Product not found.", null);
        }

        // Calculate derived current stock
        var currentStock = (await _dbContext.StockMovements
            .Where(sm => sm.ProductId == productId)
            .ToListAsync(ct))
            .Sum(sm => sm.Quantity);

        // INV-03: Stock cannot go negative
        if (currentStock + quantity < 0)
        {
            return (false, $"Adjustment would result in negative stock. Current: {currentStock}, adjustment: {quantity}.", null);
        }

        var now = _clock.GetCurrentInstant();
        var movement = new StockMovement
        {
            ProductId = productId,
            Type = StockMovementType.Adjustment,
            Quantity = quantity,
            UnitAmount = product.CostPrice,
            Reason = reason.Trim(),
            CreatedById = userId,
            CreatedAt = now
        };

        _dbContext.StockMovements.Add(movement);
        await _dbContext.SaveChangesAsync(ct);

        string? creatorName = null;
        if (userId.HasValue)
        {
            creatorName = await _dbContext.Users
                .Where(u => u.Id == userId.Value)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync(ct);
        }

        var zonedTime = now.InZone(_zone);
        var dto = new StockMovementDto
        {
            Id = movement.Id,
            ProductId = movement.ProductId,
            ProductName = product.Name,
            Type = movement.Type.ToString().ToLowerInvariant(),
            Quantity = movement.Quantity,
            UnitAmount = movement.UnitAmount,
            Reason = movement.Reason,
            CreatedByName = creatorName ?? "Administrator",
            CreatedAt = zonedTime.ToString("yyyy-MM-dd h:mm tt", CultureInfo.InvariantCulture)
        };

        return (true, null, dto);
    }

    public async Task<IReadOnlyList<StockMovementDto>> GetProductMovementsAsync(int productId, CancellationToken ct = default)
    {
        var movements = await _dbContext.StockMovements
            .AsNoTracking()
            .Where(sm => sm.ProductId == productId)
            .OrderByDescending(sm => sm.CreatedAt)
            .ToListAsync(ct);

        var product = await _dbContext.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId, ct);
        var creatorIds = movements.Where(sm => sm.CreatedById.HasValue).Select(sm => sm.CreatedById!.Value).Distinct().ToList();
        var creators = await _dbContext.Users.AsNoTracking().Where(u => creatorIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);

        foreach (var sm in movements)
        {
            if (product != null) sm.Product = product;
            if (sm.CreatedById.HasValue && creators.TryGetValue(sm.CreatedById.Value, out var c)) sm.CreatedBy = c;
        }

        return movements.Select(sm =>
        {
            var zonedTime = sm.CreatedAt.InZone(_zone);
            return new StockMovementDto
            {
                Id = sm.Id,
                ProductId = sm.ProductId,
                ProductName = sm.Product?.Name ?? string.Empty,
                Type = sm.Type.ToString().ToLowerInvariant(),
                Quantity = sm.Quantity,
                UnitAmount = sm.UnitAmount,
                RefType = sm.RefType,
                RefId = sm.RefId,
                Reason = sm.Reason,
                CreatedByName = sm.CreatedBy?.FullName ?? "System",
                CreatedAt = zonedTime.ToString("yyyy-MM-dd h:mm tt", CultureInfo.InvariantCulture)
            };
        }).ToList();
    }

    public async Task<IReadOnlyList<EquipmentDto>> GetEquipmentAsync(
        string? search = null, string? category = null, EquipmentStatus? status = null, EquipmentCondition? condition = null, CancellationToken ct = default)
    {
        var query = _dbContext.Equipment.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(e => e.Name.ToLower().Contains(term) ||
                                     e.AssetTag.ToLower().Contains(term) ||
                                     (e.BrandModel != null && e.BrandModel.ToLower().Contains(term)) ||
                                     (e.SerialNo != null && e.SerialNo.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            var cat = category.Trim().ToLowerInvariant();
            query = query.Where(e => e.Category.ToLower() == cat);
        }

        if (status.HasValue)
        {
            query = query.Where(e => e.Status == status.Value);
        }

        if (condition.HasValue)
        {
            query = query.Where(e => e.Condition == condition.Value);
        }

        var items = await query
            .OrderBy(e => e.Category)
            .ThenBy(e => e.AssetTag)
            .ToListAsync(ct);

        return items.Select(MapEquipmentDto).ToList();
    }

    public async Task<EquipmentDto?> GetEquipmentByIdAsync(int id, CancellationToken ct = default)
    {
        var item = await _dbContext.Equipment.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
        return item != null ? MapEquipmentDto(item) : null;
    }

    public async Task<EquipmentDto> CreateEquipmentAsync(CreateEquipmentRequest request, CancellationToken ct = default)
    {
        var normalizedAssetTag = request.AssetTag.Trim().ToUpperInvariant();

        var exists = await _dbContext.Equipment.AnyAsync(e => e.AssetTag.ToUpper() == normalizedAssetTag, ct);
        if (exists)
        {
            throw new InvalidOperationException($"Equipment with Asset Tag '{request.AssetTag}' already exists.");
        }

        var equipment = new Equipment
        {
            AssetTag = normalizedAssetTag,
            Name = request.Name.Trim(),
            Category = request.Category.Trim(),
            BrandModel = string.IsNullOrWhiteSpace(request.BrandModel) ? null : request.BrandModel.Trim(),
            SerialNo = string.IsNullOrWhiteSpace(request.SerialNo) ? null : request.SerialNo.Trim(),
            PurchaseDate = ParseLocalDate(request.PurchaseDate),
            PurchaseCost = request.PurchaseCost,
            Condition = request.Condition,
            Status = request.Status,
            InspectionIntervalDays = request.InspectionIntervalDays > 0 ? request.InspectionIntervalDays : 30,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
        };

        _dbContext.Equipment.Add(equipment);
        await _dbContext.SaveChangesAsync(ct);

        return MapEquipmentDto(equipment);
    }

    public async Task<EquipmentDto?> UpdateEquipmentAsync(int id, UpdateEquipmentRequest request, CancellationToken ct = default)
    {
        var equipment = await _dbContext.Equipment.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (equipment == null) return null;

        equipment.Name = request.Name.Trim();
        equipment.Category = request.Category.Trim();
        equipment.BrandModel = string.IsNullOrWhiteSpace(request.BrandModel) ? null : request.BrandModel.Trim();
        equipment.SerialNo = string.IsNullOrWhiteSpace(request.SerialNo) ? null : request.SerialNo.Trim();
        equipment.PurchaseDate = ParseLocalDate(request.PurchaseDate);
        equipment.PurchaseCost = request.PurchaseCost;
        equipment.Condition = request.Condition;
        equipment.Status = request.Status;
        equipment.InspectionIntervalDays = request.InspectionIntervalDays > 0 ? request.InspectionIntervalDays : 30;
        equipment.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        await _dbContext.SaveChangesAsync(ct);
        return MapEquipmentDto(equipment);
    }

    public async Task<bool> RetireEquipmentAsync(int id, string? notes = null, CancellationToken ct = default)
    {
        var equipment = await _dbContext.Equipment.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (equipment == null) return false;

        equipment.Status = EquipmentStatus.Retired;
        if (!string.IsNullOrWhiteSpace(notes))
        {
            equipment.Notes = string.IsNullOrWhiteSpace(equipment.Notes)
                ? $"Retired: {notes.Trim()}"
                : $"{equipment.Notes}\nRetired: {notes.Trim()}";
        }

        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    private static EquipmentDto MapEquipmentDto(Equipment e)
    {
        return new EquipmentDto
        {
            Id = e.Id,
            AssetTag = e.AssetTag,
            Name = e.Name,
            Category = e.Category,
            BrandModel = e.BrandModel,
            SerialNo = e.SerialNo,
            PurchaseDate = e.PurchaseDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            PurchaseCost = e.PurchaseCost,
            Condition = FormatEquipmentCondition(e.Condition),
            Status = FormatEquipmentStatus(e.Status),
            InspectionIntervalDays = e.InspectionIntervalDays,
            Notes = e.Notes
        };
    }

    private static string FormatEquipmentStatus(EquipmentStatus status) => status switch
    {
        EquipmentStatus.InService => "in_service",
        EquipmentStatus.UnderMaintenance => "under_maintenance",
        EquipmentStatus.OutOfService => "out_of_service",
        EquipmentStatus.Retired => "retired",
        _ => status.ToString().ToLowerInvariant()
    };

    private static string FormatEquipmentCondition(EquipmentCondition condition) => condition switch
    {
        EquipmentCondition.Excellent => "excellent",
        EquipmentCondition.Good => "good",
        EquipmentCondition.Fair => "fair",
        EquipmentCondition.Poor => "poor",
        _ => condition.ToString().ToLowerInvariant()
    };

    private static LocalDate? ParseLocalDate(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr)) return null;
        var parse = LocalDatePattern.Iso.Parse(dateStr.Trim());
        return parse.Success ? parse.Value : null;
    }
}

