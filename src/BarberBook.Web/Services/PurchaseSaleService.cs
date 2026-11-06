using System.Globalization;
using BarberBook.Data;
using BarberBook.Data.Entities;
using BarberBook.Data.Enums;
using BarberBook.Web.Common;
using BarberBook.Web.Models.Inventory;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using NodaTime.Text;

namespace BarberBook.Web.Services;

public class PurchaseSaleService : IPurchaseSaleService
{
    private readonly BarberBookDbContext _dbContext;
    private readonly IClock _clock;
    private readonly DateTimeZone _zone;

    public PurchaseSaleService(BarberBookDbContext dbContext, IClock clock, DateTimeZone? zone = null)
    {
        _dbContext = dbContext;
        _clock = clock;
        _zone = zone ?? DateTimeZoneProviders.Tzdb["Asia/Manila"];
    }

    // ==========================================
    // 1. Purchases (Buy / Restock — HW-2, AF-11)
    // ==========================================

    public async Task<(bool Success, string? Error, PurchaseDto? Purchase)> CreatePurchaseAsync(
        CreatePurchaseRequest request, int? userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.SupplierName))
        {
            return (false, "Supplier name is required.", null);
        }

        if (request.Items == null || request.Items.Count == 0)
        {
            return (false, "At least one item is required in a purchase.", null);
        }

        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _dbContext.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        foreach (var item in request.Items)
        {
            if (!products.ContainsKey(item.ProductId))
            {
                return (false, $"Product with ID {item.ProductId} not found.", null);
            }
            if (item.Quantity <= 0)
            {
                return (false, $"Quantity for product '{products[item.ProductId].Name}' must be greater than zero.", null);
            }
            if (item.UnitCost < 0)
            {
                return (false, $"Unit cost for product '{products[item.ProductId].Name}' cannot be negative.", null);
            }
        }

        var now = _clock.GetCurrentInstant();
        var purchasedAt = ParseInstantOrNow(request.PurchasedAt, now);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            decimal totalCost = 0;
            var purchaseItems = new List<PurchaseItem>();

            foreach (var item in request.Items)
            {
                var product = products[item.ProductId];
                var lineTotal = item.Quantity * item.UnitCost;
                totalCost += lineTotal;

                purchaseItems.Add(new PurchaseItem
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitCost = item.UnitCost,
                    LineTotal = lineTotal
                });

                // Update product's cost_price if requested and cost > 0
                if (request.UpdateCostPrice && item.UnitCost > 0)
                {
                    product.CostPrice = item.UnitCost;
                }
            }

            var purchase = new Purchase
            {
                SupplierName = request.SupplierName.Trim(),
                PurchasedAt = purchasedAt,
                TotalCost = totalCost,
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                CreatedById = userId,
                CreatedAt = now,
                Items = purchaseItems
            };

            _dbContext.Purchases.Add(purchase);
            await _dbContext.SaveChangesAsync(ct);

            // Insert stock movements (Type = Purchase, +quantity)
            foreach (var item in purchase.Items)
            {
                var sm = new StockMovement
                {
                    ProductId = item.ProductId,
                    Type = StockMovementType.Purchase,
                    Quantity = item.Quantity,
                    UnitAmount = item.UnitCost,
                    RefType = "purchase",
                    RefId = purchase.Id,
                    Reason = $"Purchase from {purchase.SupplierName}",
                    CreatedById = userId,
                    CreatedAt = purchasedAt
                };
                _dbContext.StockMovements.Add(sm);
            }

            await _dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            var dto = await GetPurchaseByIdAsync(purchase.Id, ct);
            return (true, null, dto);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            return (false, $"Failed to record purchase: {ex.Message}", null);
        }
    }

    public async Task<IReadOnlyList<PurchaseDto>> GetPurchasesAsync(
        LocalDate? startDate = null, LocalDate? endDate = null, CancellationToken ct = default)
    {
        var query = _dbContext.Purchases
            .AsNoTracking()
            .Include(p => p.CreatedBy)
            .Include(p => p.Items)
                .ThenInclude(i => i.Product)
            .AsQueryable();

        if (startDate.HasValue)
        {
            var startUtc = startDate.Value.AtStartOfDayInZone(_zone).ToInstant();
            query = query.Where(p => p.PurchasedAt >= startUtc);
        }

        if (endDate.HasValue)
        {
            var endExclusiveUtc = endDate.Value.PlusDays(1).AtStartOfDayInZone(_zone).ToInstant();
            query = query.Where(p => p.PurchasedAt < endExclusiveUtc);
        }

        var list = await query
            .OrderByDescending(p => p.PurchasedAt)
            .ToListAsync(ct);

        return list.Select(MapPurchaseDto).ToList();
    }

    public async Task<PurchaseDto?> GetPurchaseByIdAsync(int id, CancellationToken ct = default)
    {
        var p = await _dbContext.Purchases
            .AsNoTracking()
            .Include(p => p.CreatedBy)
            .Include(p => p.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        return p != null ? MapPurchaseDto(p) : null;
    }

    // ==========================================
    // 2. Product Sales (Sell — HW-2, AF-12, INV-03, INV-04)
    // ==========================================

    public async Task<(bool Success, string? Error, string? ErrorCode, ProductSaleDto? Sale)> CreateProductSaleAsync(
        CreateProductSaleRequest request, int? userId, CancellationToken ct = default)
    {
        if (request.Items == null || request.Items.Count == 0)
        {
            return (false, "At least one item is required in a product sale.", "EMPTY_ITEMS", null);
        }

        var distinctProductIds = request.Items.Select(i => i.ProductId).Distinct().OrderBy(id => id).ToList();

        var now = _clock.GetCurrentInstant();
        var soldAt = ParseInstantOrNow(request.SoldAt, now);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            // Lock product rows in ascending order to prevent deadlocks and race conditions
            var productsDict = new Dictionary<int, Product>();
            foreach (var pid in distinctProductIds)
            {
                var lockedProduct = await _dbContext.Products
                    .FromSqlInterpolated($"SELECT * FROM products WHERE id = {pid} FOR UPDATE")
                    .FirstOrDefaultAsync(ct);

                if (lockedProduct == null)
                {
                    await transaction.RollbackAsync(ct);
                    return (false, $"Product with ID {pid} was not found.", "NOT_FOUND", null);
                }
                productsDict[pid] = lockedProduct;
            }

            // Aggregate requested quantities per product (in case user submitted multiple lines of same product)
            var requestedPerProduct = request.Items
                .GroupBy(i => i.ProductId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

            // Verify stock for each product
            foreach (var kvp in requestedPerProduct)
            {
                var pid = kvp.Key;
                var requestedQty = kvp.Value;
                var product = productsDict[pid];

                if (requestedQty <= 0)
                {
                    await transaction.RollbackAsync(ct);
                    return (false, $"Quantity for product '{product.Name}' must be greater than zero.", "INVALID_QUANTITY", null);
                }

                var currentStock = await _dbContext.StockMovements
                    .Where(sm => sm.ProductId == pid)
                    .SumAsync(sm => (int?)sm.Quantity, ct) ?? 0;

                // INV-03: Refuse the whole sale if any item exceeds the stock (409 INSUFFICIENT_STOCK)
                if (requestedQty > currentStock)
                {
                    await transaction.RollbackAsync(ct);
                    return (false, $"Insufficient stock for product '{product.Name}'. Current stock is {currentStock}, requested {requestedQty}.", "INSUFFICIENT_STOCK", null);
                }
            }

            // Build sale items with snapshot prices (INV-04: price snapshot unaffected by later edits)
            decimal totalAmount = 0;
            var saleItems = new List<ProductSaleItem>();

            foreach (var item in request.Items)
            {
                var product = productsDict[item.ProductId];
                var unitPriceSnapshot = product.SellPrice;
                var lineTotal = item.Quantity * unitPriceSnapshot;
                totalAmount += lineTotal;

                saleItems.Add(new ProductSaleItem
                {
                    ProductId = item.ProductId,
                    ProductName = product.Name,
                    Quantity = item.Quantity,
                    UnitPrice = unitPriceSnapshot,
                    LineTotal = lineTotal
                });
            }

            var sale = new ProductSale
            {
                SoldAt = soldAt,
                TotalAmount = totalAmount,
                CustomerId = request.CustomerId,
                AppointmentId = request.AppointmentId,
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                RecordedById = userId,
                CreatedAt = now,
                Items = saleItems
            };

            _dbContext.ProductSales.Add(sale);
            await _dbContext.SaveChangesAsync(ct);

            // Insert stock movements (Type = Sale, negative quantity: -item.Quantity)
            foreach (var item in sale.Items)
            {
                var sm = new StockMovement
                {
                    ProductId = item.ProductId,
                    Type = StockMovementType.Sale,
                    Quantity = -item.Quantity,
                    UnitAmount = item.UnitPrice,
                    RefType = "product_sale",
                    RefId = sale.Id,
                    Reason = $"Sold {item.Quantity} unit(s) at counter",
                    CreatedById = userId,
                    CreatedAt = soldAt
                };
                _dbContext.StockMovements.Add(sm);
            }

            await _dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            var dto = await GetProductSaleByIdAsync(sale.Id, ct);
            return (true, null, null, dto);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            return (false, $"Failed to record product sale: {ex.Message}", "SERVER_ERROR", null);
        }
    }

    public async Task<IReadOnlyList<ProductSaleDto>> GetProductSalesAsync(
        LocalDate? startDate = null, LocalDate? endDate = null, int? productId = null, CancellationToken ct = default)
    {
        var query = _dbContext.ProductSales
            .AsNoTracking()
            .Include(s => s.RecordedBy)
            .Include(s => s.Customer)
            .Include(s => s.Appointment)
            .Include(s => s.Items)
            .AsQueryable();

        if (startDate.HasValue)
        {
            var startUtc = startDate.Value.AtStartOfDayInZone(_zone).ToInstant();
            query = query.Where(s => s.SoldAt >= startUtc);
        }

        if (endDate.HasValue)
        {
            var endExclusiveUtc = endDate.Value.PlusDays(1).AtStartOfDayInZone(_zone).ToInstant();
            query = query.Where(s => s.SoldAt < endExclusiveUtc);
        }

        if (productId.HasValue)
        {
            query = query.Where(s => s.Items.Any(i => i.ProductId == productId.Value));
        }

        var list = await query
            .OrderByDescending(s => s.SoldAt)
            .ToListAsync(ct);

        return list.Select(MapProductSaleDto).ToList();
    }

    public async Task<ProductSaleDto?> GetProductSaleByIdAsync(int id, CancellationToken ct = default)
    {
        var s = await _dbContext.ProductSales
            .AsNoTracking()
            .Include(s => s.RecordedBy)
            .Include(s => s.Customer)
            .Include(s => s.Appointment)
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        return s != null ? MapProductSaleDto(s) : null;
    }

    public async Task<List<ProductSalesSummaryItemDto>> GetProductSalesSummaryAsync(
        LocalDate? startDate = null, LocalDate? endDate = null, CancellationToken ct = default)
    {
        var query = _dbContext.ProductSaleItems
            .AsNoTracking()
            .Include(i => i.ProductSale)
            .Include(i => i.Product)
            .AsQueryable();

        if (startDate.HasValue)
        {
            var startUtc = startDate.Value.AtStartOfDayInZone(_zone).ToInstant();
            query = query.Where(i => i.ProductSale.SoldAt >= startUtc);
        }

        if (endDate.HasValue)
        {
            var endExclusiveUtc = endDate.Value.PlusDays(1).AtStartOfDayInZone(_zone).ToInstant();
            query = query.Where(i => i.ProductSale.SoldAt < endExclusiveUtc);
        }

        var items = await query.ToListAsync(ct);

        return items
            .GroupBy(i => new { i.ProductId, i.ProductName })
            .Select(g => new ProductSalesSummaryItemDto
            {
                ProductId = g.Key.ProductId,
                ProductName = g.Key.ProductName,
                QuantitySold = g.Sum(x => x.Quantity),
                TotalRevenue = g.Sum(x => x.LineTotal),
                FormattedTotalRevenue = FormattingHelpers.FormatPeso(g.Sum(x => x.LineTotal))
            })
            .OrderByDescending(x => x.TotalRevenue)
            .ToList();
    }

    // ==========================================
    // Mapping Helpers
    // ==========================================

    private PurchaseDto MapPurchaseDto(Purchase p)
    {
        var zoned = p.PurchasedAt.InZone(_zone);
        return new PurchaseDto
        {
            Id = p.Id,
            SupplierName = p.SupplierName,
            PurchasedAt = zoned.ToString("yyyy-MM-dd h:mm tt", CultureInfo.InvariantCulture),
            TotalCost = p.TotalCost,
            FormattedTotalCost = FormattingHelpers.FormatPeso(p.TotalCost),
            Notes = p.Notes,
            CreatedById = p.CreatedById,
            CreatedByName = p.CreatedBy?.FullName ?? "Administrator",
            Items = p.Items.Select(i => new PurchaseItemDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                ProductName = i.Product?.Name ?? $"Product #{i.ProductId}",
                Sku = i.Product?.Sku ?? string.Empty,
                Quantity = i.Quantity,
                UnitCost = i.UnitCost,
                FormattedUnitCost = FormattingHelpers.FormatPeso(i.UnitCost),
                LineTotal = i.LineTotal,
                FormattedLineTotal = FormattingHelpers.FormatPeso(i.LineTotal)
            }).ToList()
        };
    }

    private ProductSaleDto MapProductSaleDto(ProductSale s)
    {
        var zoned = s.SoldAt.InZone(_zone);
        return new ProductSaleDto
        {
            Id = s.Id,
            SoldAt = zoned.ToString("yyyy-MM-dd h:mm tt", CultureInfo.InvariantCulture),
            TotalAmount = s.TotalAmount,
            FormattedTotalAmount = FormattingHelpers.FormatPeso(s.TotalAmount),
            CustomerId = s.CustomerId,
            CustomerName = s.Customer?.FullName,
            AppointmentId = s.AppointmentId,
            AppointmentReference = s.Appointment?.ReferenceCode,
            Notes = s.Notes,
            RecordedById = s.RecordedById,
            RecordedByName = s.RecordedBy?.FullName ?? "Administrator",
            Items = s.Items.Select(i => new ProductSaleItemDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                FormattedUnitPrice = FormattingHelpers.FormatPeso(i.UnitPrice),
                LineTotal = i.LineTotal,
                FormattedLineTotal = FormattingHelpers.FormatPeso(i.LineTotal)
            }).ToList()
        };
    }

    private Instant ParseInstantOrNow(string? dateStr, Instant fallback)
    {
        if (string.IsNullOrWhiteSpace(dateStr)) return fallback;

        // Try ISO LocalDate (yyyy-MM-dd)
        var dateParse = LocalDatePattern.Iso.Parse(dateStr.Trim());
        if (dateParse.Success)
        {
            // Convert to Asia/Manila start of day or midday
            var localTime = fallback.InZone(_zone).TimeOfDay;
            return dateParse.Value.At(localTime).InZoneStrictly(_zone).ToInstant();
        }

        // Try full ISO instant
        var instantParse = InstantPattern.ExtendedIso.Parse(dateStr.Trim());
        if (instantParse.Success)
        {
            return instantParse.Value;
        }

        return fallback;
    }
}

