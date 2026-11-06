using System.Net;
using System.Net.Http.Json;
using BarberBook.Data;
using BarberBook.Data.Entities;
using BarberBook.Data.Enums;
using BarberBook.Data.Seeding;
using BarberBook.Web.Common;
using BarberBook.Web.Models.Admin;
using BarberBook.Web.Models.Auth;
using BarberBook.Web.Models.Inventory;
using BarberBook.Web.Models.Reports;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Xunit;

namespace BarberBook.Tests;

public class AdminPurchasesAndSalesTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private const string AdminEmail = "admin@barberbook.local";
    private const string AdminPassword = "Admin@BarberBook2026!";
    private const string RamonEmail = "ramon@barberbook.local";
    private const string CustomerEmail = "juan@barberbook.local";
    private const string DemoPassword = "Demo@BarberBook2026!";

    public AdminPurchasesAndSalesTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private async Task EnsureSeededAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await DatabaseSeeder.SeedAsync(scope.ServiceProvider);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string email, string password)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginModel
        {
            Email = email,
            Password = password
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return client;
    }

    private HttpClient CreateAnonymousClient()
    {
        return _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task T18_HW2_INV03_PurchaseRaisesStock_SaleLowersStock()
    {
        // Arrange
        await EnsureSeededAsync();
        var adminClient = await CreateAuthenticatedClientAsync(AdminEmail, AdminPassword);

        // 1. Create a distinct test product with 0 stock
        var sku = $"TEST-WAX-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        var createProdResp = await adminClient.PostAsJsonAsync("/api/admin/inventory/products", new CreateProductRequest
        {
            Sku = sku,
            Name = "Matte Clay Styling Wax",
            Category = "Hair Care",
            Unit = "jar",
            SellPrice = 450.00m,
            CostPrice = 220.00m,
            ReorderLevel = 5
        });
        createProdResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdProduct = await createProdResp.Content.ReadFromJsonAsync<ProductDto>();
        createdProduct.Should().NotBeNull();
        var productId = createdProduct!.Id;
        createdProduct.CurrentStock.Should().Be(0);

        // 2. Buy (Purchases) 10 units -> stock increases by 10 (INV-03)
        var purchasePayload = new CreatePurchaseRequest
        {
            SupplierName = "Grooming Wholesaler PH",
            PurchasedAt = "2026-11-06",
            Notes = "Wholesale bulk restock batch",
            UpdateCostPrice = true,
            Items = new List<CreatePurchaseItemRequest>
            {
                new()
                {
                    ProductId = productId,
                    Quantity = 10,
                    UnitCost = 210.00m
                }
            }
        };

        var purchaseResp = await adminClient.PostAsJsonAsync("/api/admin/purchases", purchasePayload);
        purchaseResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var purchaseData = await purchaseResp.Content.ReadFromJsonAsync<PurchaseDto>();
        purchaseData.Should().NotBeNull();
        purchaseData!.TotalCost.Should().Be(2100.00m);
        purchaseData.Items.Should().HaveCount(1);

        // Verify stock is now 10
        var getProdAfterPurchase = await adminClient.GetFromJsonAsync<ProductDto>($"/api/admin/inventory/products/{productId}");
        getProdAfterPurchase!.CurrentStock.Should().Be(10);
        getProdAfterPurchase.CostPrice.Should().Be(210.00m); // cost price updated

        // 3. Sell 3 units -> stock decreases to 7 (INV-03)
        var salePayload = new CreateProductSaleRequest
        {
            SoldAt = "2026-11-06",
            Notes = "Walk-in retail counter sale",
            Items = new List<CreateProductSaleItemRequest>
            {
                new()
                {
                    ProductId = productId,
                    Quantity = 3
                }
            }
        };

        var saleResp = await adminClient.PostAsJsonAsync("/api/admin/product-sales", salePayload);
        saleResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var saleData = await saleResp.Content.ReadFromJsonAsync<ProductSaleDto>();
        saleData.Should().NotBeNull();
        saleData!.TotalAmount.Should().Be(1350.00m); // 3 * 450.00m
        saleData.Items.Should().HaveCount(1);
        saleData.Items[0].Quantity.Should().Be(3);
        saleData.Items[0].UnitPrice.Should().Be(450.00m);
        saleData.Items[0].LineTotal.Should().Be(1350.00m);

        // Verify stock is now 7
        var getProdAfterSale = await adminClient.GetFromJsonAsync<ProductDto>($"/api/admin/inventory/products/{productId}");
        getProdAfterSale!.CurrentStock.Should().Be(7);
    }

    [Fact]
    public async Task T18_HW2_INV03_OversellRefused_With409InsufficientStock_AndNothingPartiallyWritten()
    {
        // Arrange
        await EnsureSeededAsync();
        var adminClient = await CreateAuthenticatedClientAsync(AdminEmail, AdminPassword);

        // Create product with 5 initial units
        var sku = $"TEST-OVER-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        var createProdResp = await adminClient.PostAsJsonAsync("/api/admin/inventory/products", new CreateProductRequest
        {
            Sku = sku,
            Name = "Limited Beard Balm",
            Category = "Beard Care",
            Unit = "tin",
            SellPrice = 300.00m,
            CostPrice = 150.00m,
            ReorderLevel = 2
        });
        var prod = (await createProdResp.Content.ReadFromJsonAsync<ProductDto>())!;

        // Restock 5 units
        await adminClient.PostAsJsonAsync("/api/admin/purchases", new CreatePurchaseRequest
        {
            SupplierName = "Barber Supply Manila",
            Items = new List<CreatePurchaseItemRequest>
            {
                new() { ProductId = prod.Id, Quantity = 5, UnitCost = 150.00m }
            }
        });

        // Current stock is 5
        var currentProd = await adminClient.GetFromJsonAsync<ProductDto>($"/api/admin/inventory/products/{prod.Id}");
        currentProd!.CurrentStock.Should().Be(5);

        // Act: Attempt to sell 20 units (exceeds stock of 5)
        var oversellResp = await adminClient.PostAsJsonAsync("/api/admin/product-sales", new CreateProductSaleRequest
        {
            Notes = "Greedy purchase attempt",
            Items = new List<CreateProductSaleItemRequest>
            {
                new() { ProductId = prod.Id, Quantity = 20 }
            }
        });

        // Assert: 409 Conflict with INSUFFICIENT_STOCK
        oversellResp.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var errBody = await oversellResp.Content.ReadAsStringAsync();
        errBody.Should().Contain("INSUFFICIENT_STOCK");

        // Verify nothing was written: stock is still 5
        var checkProd = await adminClient.GetFromJsonAsync<ProductDto>($"/api/admin/inventory/products/{prod.Id}");
        checkProd!.CurrentStock.Should().Be(5);

        // Verify database has no sale records for this product
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
        var saleItems = await db.ProductSaleItems.Where(i => i.ProductId == prod.Id).ToListAsync();
        saleItems.Should().BeEmpty();
    }

    [Fact]
    public async Task T18_HW2_INV05_LowStockAlertAppearsAtOrBelowReorderLevel()
    {
        // Arrange
        await EnsureSeededAsync();
        var adminClient = await CreateAuthenticatedClientAsync(AdminEmail, AdminPassword);

        // Create product with reorder level 5
        var sku = $"TEST-ALERT-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        var createProdResp = await adminClient.PostAsJsonAsync("/api/admin/inventory/products", new CreateProductRequest
        {
            Sku = sku,
            Name = "Precision Shave Gel",
            Category = "Shave",
            Unit = "bottle",
            SellPrice = 320.00m,
            CostPrice = 160.00m,
            ReorderLevel = 5
        });
        var prod = (await createProdResp.Content.ReadFromJsonAsync<ProductDto>())!;

        // Restock 6 units (above reorder level 5)
        await adminClient.PostAsJsonAsync("/api/admin/purchases", new CreatePurchaseRequest
        {
            SupplierName = "Shave Labs",
            Items = new List<CreatePurchaseItemRequest>
            {
                new() { ProductId = prod.Id, Quantity = 6, UnitCost = 160.00m }
            }
        });

        var prodCheck1 = await adminClient.GetFromJsonAsync<ProductDto>($"/api/admin/inventory/products/{prod.Id}");
        prodCheck1!.CurrentStock.Should().Be(6);
        prodCheck1.IsLowStock.Should().BeFalse();

        // Act: Sell 2 units -> stock drops to 4 (4 <= 5 reorder level)
        var saleResp = await adminClient.PostAsJsonAsync("/api/admin/product-sales", new CreateProductSaleRequest
        {
            Items = new List<CreateProductSaleItemRequest>
            {
                new() { ProductId = prod.Id, Quantity = 2 }
            }
        });
        saleResp.StatusCode.Should().Be(HttpStatusCode.Created);

        // Assert: Low stock flag is now true (INV-05)
        var prodCheck2 = await adminClient.GetFromJsonAsync<ProductDto>($"/api/admin/inventory/products/{prod.Id}");
        prodCheck2!.CurrentStock.Should().Be(4);
        prodCheck2.IsLowStock.Should().BeTrue();

        // Check Admin Dashboard API returns this low stock alert
        var dashResp = await adminClient.GetAsync("/api/admin/dashboard");
        dashResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var dashData = await dashResp.Content.ReadFromJsonAsync<AdminDashboardDto>();
        dashData.Should().NotBeNull();
        dashData!.LowStockProductsCount.Should().BeGreaterThan(0);
        dashData.LowStockProducts.Should().Contain(p => p.Id == prod.Id && p.CurrentStock == 4);
    }

    [Fact]
    public async Task T19_HW2_INV04_PriceSnapshotUnaffectedByLaterEdits()
    {
        // Arrange
        await EnsureSeededAsync();
        var adminClient = await CreateAuthenticatedClientAsync(AdminEmail, AdminPassword);

        // Create product with sell price ₱500.00
        var sku = $"TEST-SNAP-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        var createProdResp = await adminClient.PostAsJsonAsync("/api/admin/inventory/products", new CreateProductRequest
        {
            Sku = sku,
            Name = "Vintage Matte Pomade",
            Category = "Hair Care",
            Unit = "can",
            SellPrice = 500.00m,
            CostPrice = 250.00m,
            ReorderLevel = 2
        });
        var prod = (await createProdResp.Content.ReadFromJsonAsync<ProductDto>())!;

        // Restock 10 units
        await adminClient.PostAsJsonAsync("/api/admin/purchases", new CreatePurchaseRequest
        {
            SupplierName = "Pomade Masters",
            Items = new List<CreatePurchaseItemRequest>
            {
                new() { ProductId = prod.Id, Quantity = 10, UnitCost = 250.00m }
            }
        });

        // Act 1: Sell 2 units at current price ₱500.00
        var saleResp = await adminClient.PostAsJsonAsync("/api/admin/product-sales", new CreateProductSaleRequest
        {
            Items = new List<CreateProductSaleItemRequest>
            {
                new() { ProductId = prod.Id, Quantity = 2 }
            }
        });
        saleResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var sale = (await saleResp.Content.ReadFromJsonAsync<ProductSaleDto>())!;
        sale.TotalAmount.Should().Be(1000.00m);
        sale.Items[0].UnitPrice.Should().Be(500.00m);

        // Act 2: Later, update product sell price to ₱650.00
        var updateProdResp = await adminClient.PutAsJsonAsync($"/api/admin/inventory/products/{prod.Id}", new UpdateProductRequest
        {
            Name = "Vintage Matte Pomade (Updated Formula)",
            Category = "Hair Care",
            Unit = "can",
            SellPrice = 650.00m,
            CostPrice = 280.00m,
            ReorderLevel = 2
        });
        updateProdResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert: Re-fetch the previously recorded sale; unit price snapshot remains ₱500.00 (INV-04)
        var fetchedSale = await adminClient.GetFromJsonAsync<ProductSaleDto>($"/api/admin/product-sales/{sale.Id}");
        fetchedSale.Should().NotBeNull();
        fetchedSale!.TotalAmount.Should().Be(1000.00m);
        fetchedSale.Items[0].UnitPrice.Should().Be(500.00m);
        fetchedSale.Items[0].LineTotal.Should().Be(1000.00m);
    }

    [Fact]
    public async Task T19_HW2_INV06_ServiceTotalFor20260914Stays1340_ProductSalesSeparate_GrandTotalMatches()
    {
        // Arrange
        await EnsureSeededAsync();
        var adminClient = await CreateAuthenticatedClientAsync(AdminEmail, AdminPassword);

        // 1. Check initial daily sales for 2026-09-14: service total must be ₱1,340.00
        var dailyInitial = await adminClient.GetFromJsonAsync<DailySalesReportDto>("/api/admin/reports/daily?date=2026-09-14");
        dailyInitial.Should().NotBeNull();
        dailyInitial!.TotalSales.Should().Be(1340.00m); // appointment service sales
        var initialProductSales = dailyInitial.ProductSales?.Total ?? 0m;

        // 2. Create product and restock
        var sku = $"TEST-REP-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        var createProdResp = await adminClient.PostAsJsonAsync("/api/admin/inventory/products", new CreateProductRequest
        {
            Sku = sku,
            Name = "Beard Conditioning Oil",
            Category = "Beard Care",
            Unit = "dropper",
            SellPrice = 350.00m,
            CostPrice = 175.00m,
            ReorderLevel = 3
        });
        var prod = (await createProdResp.Content.ReadFromJsonAsync<ProductDto>())!;

        await adminClient.PostAsJsonAsync("/api/admin/purchases", new CreatePurchaseRequest
        {
            SupplierName = "Oils Direct",
            PurchasedAt = "2026-09-14",
            Items = new List<CreatePurchaseItemRequest>
            {
                new() { ProductId = prod.Id, Quantity = 5, UnitCost = 175.00m }
            }
        });

        // 3. Record a product sale of 1 unit (₱350.00) on 2026-09-14
        var saleResp = await adminClient.PostAsJsonAsync("/api/admin/product-sales", new CreateProductSaleRequest
        {
            SoldAt = "2026-09-14",
            Notes = "Product sale paired with haircut",
            Items = new List<CreateProductSaleItemRequest>
            {
                new() { ProductId = prod.Id, Quantity = 1 }
            }
        });
        saleResp.StatusCode.Should().Be(HttpStatusCode.Created);

        // 4. Assert Daily Sales on 2026-09-14:
        // Service Total stays exactly ₱1,340.00 (INV-06)
        // Product Sales total increases by ₱350.00
        // Grand Total is ₱1,340.00 + Product Sales
        var expectedProductSales = initialProductSales + 350.00m;
        var dailyAfter = await adminClient.GetFromJsonAsync<DailySalesReportDto>("/api/admin/reports/daily?date=2026-09-14");
        dailyAfter.Should().NotBeNull();
        dailyAfter!.TotalSales.Should().Be(1340.00m); // Unchanged!
        dailyAfter.ProductSales.Should().NotBeNull();
        dailyAfter.ProductSales.Total.Should().Be(expectedProductSales);
        dailyAfter.GrandTotal.Should().Be(1340.00m + expectedProductSales);

        // 5. Assert Monthly Sales for 2026-09:
        var monthlyResp = await adminClient.GetFromJsonAsync<MonthlySalesReportDto>("/api/admin/reports/monthly?year=2026&month=9");
        monthlyResp.Should().NotBeNull();
        var day14 = monthlyResp!.DailyTotals.First(d => d.Day == 14);
        day14.TotalSales.Should().Be(1340.00m);
        day14.ProductSales.Should().Be(expectedProductSales);
        day14.GrandTotal.Should().Be(1340.00m + expectedProductSales);

        monthlyResp.GrandTotal.Should().Be(monthlyResp.TotalSales + monthlyResp.ProductSales);
    }

    [Fact]
    public async Task RoleGuards_NonAdminCannotAccessPurchasesOrProductSales()
    {
        // Arrange
        await EnsureSeededAsync();
        var customerClient = await CreateAuthenticatedClientAsync(CustomerEmail, DemoPassword);
        var barberClient = await CreateAuthenticatedClientAsync(RamonEmail, DemoPassword);
        var anonClient = CreateAnonymousClient();

        // 1. Customer cannot POST or GET purchases
        var custPostPurch = await customerClient.PostAsJsonAsync("/api/admin/purchases", new CreatePurchaseRequest());
        custPostPurch.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var custGetPurch = await customerClient.GetAsync("/api/admin/purchases");
        custGetPurch.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 2. Customer cannot POST or GET product sales
        var custPostSale = await customerClient.PostAsJsonAsync("/api/admin/product-sales", new CreateProductSaleRequest());
        custPostSale.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var custGetSale = await customerClient.GetAsync("/api/admin/product-sales");
        custGetSale.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 3. Barber cannot POST or GET purchases
        var barbPostPurch = await barberClient.PostAsJsonAsync("/api/admin/purchases", new CreatePurchaseRequest());
        barbPostPurch.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var barbGetPurch = await barberClient.GetAsync("/api/admin/purchases");
        barbGetPurch.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 4. Barber cannot POST or GET product sales
        var barbPostSale = await barberClient.PostAsJsonAsync("/api/admin/product-sales", new CreateProductSaleRequest());
        barbPostSale.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var barbGetSale = await barberClient.GetAsync("/api/admin/product-sales");
        barbGetSale.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 5. Unauthenticated client redirected or 401
        var anonGetPurch = await anonClient.GetAsync("/api/admin/purchases");
        anonGetPurch.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Redirect);

        var anonGetSale = await anonClient.GetAsync("/api/admin/product-sales");
        anonGetSale.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Redirect);
    }
}
