using BarberBook.Web.Models.Inventory;
using NodaTime;

namespace BarberBook.Web.Services;

public interface IPurchaseSaleService
{
    Task<(bool Success, string? Error, PurchaseDto? Purchase)> CreatePurchaseAsync(
        CreatePurchaseRequest request, int? userId, CancellationToken ct = default);

    Task<IReadOnlyList<PurchaseDto>> GetPurchasesAsync(
        LocalDate? startDate = null, LocalDate? endDate = null, CancellationToken ct = default);

    Task<PurchaseDto?> GetPurchaseByIdAsync(int id, CancellationToken ct = default);

    Task<(bool Success, string? Error, string? ErrorCode, ProductSaleDto? Sale)> CreateProductSaleAsync(
        CreateProductSaleRequest request, int? userId, CancellationToken ct = default);

    Task<IReadOnlyList<ProductSaleDto>> GetProductSalesAsync(
        LocalDate? startDate = null, LocalDate? endDate = null, int? productId = null, CancellationToken ct = default);

    Task<ProductSaleDto?> GetProductSaleByIdAsync(int id, CancellationToken ct = default);

    Task<List<ProductSalesSummaryItemDto>> GetProductSalesSummaryAsync(
        LocalDate? startDate = null, LocalDate? endDate = null, CancellationToken ct = default);
}
