using BarberBook.Data.Enums;
using BarberBook.Web.Models.Inventory;

namespace BarberBook.Web.Services;

public interface IInventoryService
{
    // Products (HW-1, AF-10, INV-03, INV-05)
    Task<IReadOnlyList<ProductDto>> GetProductsAsync(string? search = null, string? category = null, bool includeInactive = false, CancellationToken ct = default);
    Task<ProductDto?> GetProductByIdAsync(int id, CancellationToken ct = default);
    Task<ProductDto> CreateProductAsync(CreateProductRequest request, int? userId, CancellationToken ct = default);
    Task<ProductDto?> UpdateProductAsync(int id, UpdateProductRequest request, CancellationToken ct = default);
    Task<bool> SetProductActiveAsync(int id, bool isActive, CancellationToken ct = default);

    // Stock adjustments and movements (INV-03, INV-04)
    Task<(bool Success, string? Error, StockMovementDto? Movement)> AdjustStockAsync(int productId, int quantity, string reason, int? userId, CancellationToken ct = default);
    Task<IReadOnlyList<StockMovementDto>> GetProductMovementsAsync(int productId, CancellationToken ct = default);

    // Equipment (HW-1, SC-10, AF-9, AF-10)
    Task<IReadOnlyList<EquipmentDto>> GetEquipmentAsync(string? search = null, string? category = null, EquipmentStatus? status = null, EquipmentCondition? condition = null, CancellationToken ct = default);
    Task<EquipmentDto?> GetEquipmentByIdAsync(int id, CancellationToken ct = default);
    Task<EquipmentDto> CreateEquipmentAsync(CreateEquipmentRequest request, CancellationToken ct = default);
    Task<EquipmentDto?> UpdateEquipmentAsync(int id, UpdateEquipmentRequest request, CancellationToken ct = default);
    Task<bool> RetireEquipmentAsync(int id, string? notes = null, CancellationToken ct = default);
}

