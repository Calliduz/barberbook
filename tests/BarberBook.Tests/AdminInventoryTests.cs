using System.Net;
using System.Net.Http.Json;
using BarberBook.Data.Entities;
using BarberBook.Data.Enums;
using BarberBook.Data.Seeding;
using BarberBook.Web.Common;
using BarberBook.Web.Models.Auth;
using BarberBook.Web.Models.Inventory;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BarberBook.Tests;

public class AdminInventoryTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private const string AdminEmail = "admin@barberbook.local";
    private const string AdminPassword = "Admin@BarberBook2026!";
    private const string BarberEmail = "ramon@barberbook.local";
    private const string CustomerEmail = "juan@barberbook.local";
    private const string DemoPassword = "Demo@BarberBook2026!";

    public AdminInventoryTests(WebApplicationFactory<Program> factory)
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

    [Fact]
    public async Task INV03_StockDerivationFromMovements_CalculatesCorrectSum()
    {
        // Arrange
        await EnsureSeededAsync();
        var adminClient = await CreateAuthenticatedClientAsync(AdminEmail, AdminPassword);

        var sku = $"TEST-STK-{Guid.NewGuid():N}"[..14].ToUpperInvariant();
        var createRequest = new CreateProductRequest
        {
            Sku = sku,
            Name = "Precision Derivation Gel",
            Category = "Styling",
            Unit = "pcs",
            CostPrice = 150m,
            SellPrice = 300m,
            ReorderLevel = 5,
            InitialStock = 10
        };

        // Act: Create product with initial stock 10
        var createResponse = await adminClient.PostAsJsonAsync("/api/admin/inventory/products", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<ProductDto>();
        created.Should().NotBeNull();
        created!.CurrentStock.Should().Be(10);

        // Adjust stock: +5
        var adj1 = await adminClient.PostAsJsonAsync($"/api/admin/inventory/products/{created.Id}/adjust", new AdjustStockRequest
        {
            Quantity = 5,
            Reason = "Mid-week supplier delivery"
        });
        adj1.StatusCode.Should().Be(HttpStatusCode.OK);

        // Adjust stock: -3
        var adj2 = await adminClient.PostAsJsonAsync($"/api/admin/inventory/products/{created.Id}/adjust", new AdjustStockRequest
        {
            Quantity = -3,
            Reason = "In-shop demonstration usage"
        });
        adj2.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert: Derived stock is exactly 10 + 5 - 3 = 12
        var getResponse = await adminClient.GetAsync($"/api/admin/inventory/products/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await getResponse.Content.ReadFromJsonAsync<ProductDto>();
        fetched.Should().NotBeNull();
        fetched!.CurrentStock.Should().Be(12);
    }

    [Fact]
    public async Task INV03_AdjustStock_RefusesNegativeStock_ReturnsBadRequest()
    {
        // Arrange
        await EnsureSeededAsync();
        var adminClient = await CreateAuthenticatedClientAsync(AdminEmail, AdminPassword);

        var sku = $"TEST-NEG-{Guid.NewGuid():N}"[..14].ToUpperInvariant();
        var createRequest = new CreateProductRequest
        {
            Sku = sku,
            Name = "Negative Stock Protection Balm",
            Category = "Beard Care",
            Unit = "btl",
            CostPrice = 100m,
            SellPrice = 250m,
            ReorderLevel = 4,
            InitialStock = 5
        };

        var createResponse = await adminClient.PostAsJsonAsync("/api/admin/inventory/products", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var product = await createResponse.Content.ReadFromJsonAsync<ProductDto>();
        product.Should().NotBeNull();

        // Act: Attempt to adjust stock by -10 when current is 5 (would result in -5)
        var adjustResponse = await adminClient.PostAsJsonAsync($"/api/admin/inventory/products/{product!.Id}/adjust", new AdjustStockRequest
        {
            Quantity = -10,
            Reason = "Excessive loss reported"
        });

        // Assert: 400 Bad Request and error response
        adjustResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var err = await adjustResponse.Content.ReadFromJsonAsync<ApiErrorResponse>();
        err.Should().NotBeNull();
        err!.Error.Code.Should().Be("STOCK_CANNOT_BE_NEGATIVE");

        // Verify stock is unchanged (still 5)
        var verifyResp = await adminClient.GetAsync($"/api/admin/inventory/products/{product.Id}");
        var verified = await verifyResp.Content.ReadFromJsonAsync<ProductDto>();
        verified!.CurrentStock.Should().Be(5);
    }

    [Fact]
    public async Task INV03_AdjustStock_RequiresMandatoryReason()
    {
        // Arrange
        await EnsureSeededAsync();
        var adminClient = await CreateAuthenticatedClientAsync(AdminEmail, AdminPassword);

        var sku = $"TEST-RSN-{Guid.NewGuid():N}"[..14].ToUpperInvariant();
        var createResponse = await adminClient.PostAsJsonAsync("/api/admin/inventory/products", new CreateProductRequest
        {
            Sku = sku,
            Name = "Reason Test Wax",
            Category = "Styling",
            Unit = "tub",
            CostPrice = 80m,
            SellPrice = 200m,
            ReorderLevel = 2,
            InitialStock = 10
        });
        var product = await createResponse.Content.ReadFromJsonAsync<ProductDto>();

        // Act: Adjust stock with empty reason
        var adjustResponse = await adminClient.PostAsJsonAsync($"/api/admin/inventory/products/{product!.Id}/adjust", new AdjustStockRequest
        {
            Quantity = -2,
            Reason = "   "
        });

        // Assert: 400 Bad Request
        adjustResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task INV05_LowStockFlag_TriggersWhenStockAtOrEqualToReorderLevel()
    {
        // Arrange
        await EnsureSeededAsync();
        var adminClient = await CreateAuthenticatedClientAsync(AdminEmail, AdminPassword);

        var sku = $"TEST-LOW-{Guid.NewGuid():N}"[..14].ToUpperInvariant();
        var createResponse = await adminClient.PostAsJsonAsync("/api/admin/inventory/products", new CreateProductRequest
        {
            Sku = sku,
            Name = "Low Stock Detection Spray",
            Category = "Aftercare",
            Unit = "btl",
            CostPrice = 120m,
            SellPrice = 260m,
            ReorderLevel = 5,
            InitialStock = 6
        });
        var product = await createResponse.Content.ReadFromJsonAsync<ProductDto>();
        product!.CurrentStock.Should().Be(6);
        product.IsLowStock.Should().BeFalse("stock (6) > reorderLevel (5)");

        // Act 1: Deduct 1 unit -> stock becomes 5 (== reorderLevel)
        var adjResp1 = await adminClient.PostAsJsonAsync($"/api/admin/inventory/products/{product.Id}/adjust", new AdjustStockRequest
        {
            Quantity = -1,
            Reason = "Sold one unit"
        });
        adjResp1.StatusCode.Should().Be(HttpStatusCode.OK);

        var check1 = await adminClient.GetAsync($"/api/admin/inventory/products/{product.Id}");
        var prod1 = await check1.Content.ReadFromJsonAsync<ProductDto>();
        prod1!.CurrentStock.Should().Be(5);
        prod1.IsLowStock.Should().BeTrue("stock (5) == reorderLevel (5) triggers low-stock alert");

        // Act 2: Deduct another unit -> stock becomes 4 (< reorderLevel)
        var adjResp2 = await adminClient.PostAsJsonAsync($"/api/admin/inventory/products/{product.Id}/adjust", new AdjustStockRequest
        {
            Quantity = -1,
            Reason = "Sold another unit"
        });
        adjResp2.StatusCode.Should().Be(HttpStatusCode.OK);

        var check2 = await adminClient.GetAsync($"/api/admin/inventory/products/{product.Id}");
        var prod2 = await check2.Content.ReadFromJsonAsync<ProductDto>();
        prod2!.CurrentStock.Should().Be(4);
        prod2.IsLowStock.Should().BeTrue("stock (4) < reorderLevel (5) triggers low-stock alert");
    }

    [Fact]
    public async Task AF10_UniqueSku_RefusesDuplicateSkuWithConflict()
    {
        // Arrange
        await EnsureSeededAsync();
        var adminClient = await CreateAuthenticatedClientAsync(AdminEmail, AdminPassword);

        var duplicateSku = $"TEST-DUP-{Guid.NewGuid():N}"[..14].ToUpperInvariant();
        var req1 = new CreateProductRequest
        {
            Sku = duplicateSku,
            Name = "Original SKU Product",
            Category = "Styling",
            Unit = "pcs",
            CostPrice = 100m,
            SellPrice = 200m,
            ReorderLevel = 3,
            InitialStock = 5
        };

        var resp1 = await adminClient.PostAsJsonAsync("/api/admin/inventory/products", req1);
        resp1.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act: Attempt to create product with the same SKU
        var req2 = new CreateProductRequest
        {
            Sku = duplicateSku,
            Name = "Conflicting SKU Product",
            Category = "Styling",
            Unit = "pcs",
            CostPrice = 120m,
            SellPrice = 250m,
            ReorderLevel = 3,
            InitialStock = 2
        };

        var resp2 = await adminClient.PostAsJsonAsync("/api/admin/inventory/products", req2);

        // Assert: 409 Conflict
        resp2.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var err = await resp2.Content.ReadFromJsonAsync<ApiErrorResponse>();
        err!.Error.Code.Should().Be("DUPLICATE_SKU");
    }

    [Fact]
    public async Task AF09_SC10_UniqueAssetTag_RefusesDuplicateAssetTagWithConflict()
    {
        // Arrange
        await EnsureSeededAsync();
        var adminClient = await CreateAuthenticatedClientAsync(AdminEmail, AdminPassword);

        var duplicateTag = $"EQ-DUP-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var req1 = new CreateEquipmentRequest
        {
            AssetTag = duplicateTag,
            Name = "Alpha Clipper Station",
            Category = "Clippers",
            BrandModel = "Wahl Professional",
            SerialNo = "SN-ALPHA-01",
            PurchaseCost = 5000m,
            Condition = EquipmentCondition.Good,
            Status = EquipmentStatus.InService,
            InspectionIntervalDays = 30
        };

        var resp1 = await adminClient.PostAsJsonAsync("/api/admin/inventory/equipment", req1);
        resp1.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act: Attempt to create equipment with identical tag
        var req2 = new CreateEquipmentRequest
        {
            AssetTag = duplicateTag,
            Name = "Duplicate Tag Clipper",
            Category = "Clippers",
            BrandModel = "Wahl Other",
            SerialNo = "SN-ALPHA-02",
            PurchaseCost = 5500m,
            Condition = EquipmentCondition.Fair,
            Status = EquipmentStatus.InService,
            InspectionIntervalDays = 30
        };

        var resp2 = await adminClient.PostAsJsonAsync("/api/admin/inventory/equipment", req2);

        // Assert: 409 Conflict
        resp2.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var err = await resp2.Content.ReadFromJsonAsync<ApiErrorResponse>();
        err!.Error.Code.Should().Be("DUPLICATE_ASSET_TAG");
    }

    [Fact]
    public async Task BR13_AF09_AF10_SoftDelete_DeactivatesProductAndRetiresEquipment()
    {
        // Arrange
        await EnsureSeededAsync();
        var adminClient = await CreateAuthenticatedClientAsync(AdminEmail, AdminPassword);

        // 1. Soft-deactivate product
        var sku = $"TEST-ACT-{Guid.NewGuid():N}"[..14].ToUpperInvariant();
        var prodResp = await adminClient.PostAsJsonAsync("/api/admin/inventory/products", new CreateProductRequest
        {
            Sku = sku,
            Name = "Deactivatable Pomade",
            Category = "Styling",
            Unit = "pcs",
            CostPrice = 100m,
            SellPrice = 200m,
            ReorderLevel = 2,
            InitialStock = 5
        });
        var product = await prodResp.Content.ReadFromJsonAsync<ProductDto>();

        var deactResp = await adminClient.DeleteAsync($"/api/admin/inventory/products/{product!.Id}");
        deactResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var verifyProd = await adminClient.GetAsync($"/api/admin/inventory/products/{product.Id}");
        var prodAfter = await verifyProd.Content.ReadFromJsonAsync<ProductDto>();
        prodAfter!.IsActive.Should().BeFalse("BR-13: product is deactivated, never hard-deleted");

        // 2. Retire equipment
        var tag = $"EQ-RET-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var eqResp = await adminClient.PostAsJsonAsync("/api/admin/inventory/equipment", new CreateEquipmentRequest
        {
            AssetTag = tag,
            Name = "Retirable Trimmer",
            Category = "Trimmers",
            BrandModel = "Andis Slimline",
            SerialNo = "SN-TRIM-01",
            PurchaseCost = 3500m,
            Condition = EquipmentCondition.Poor,
            Status = EquipmentStatus.OutOfService,
            InspectionIntervalDays = 30
        });
        var equipment = await eqResp.Content.ReadFromJsonAsync<EquipmentDto>();

        var retireResp = await adminClient.DeleteAsync($"/api/admin/inventory/equipment/{equipment!.Id}");
        retireResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var verifyEq = await adminClient.GetAsync($"/api/admin/inventory/equipment/{equipment.Id}");
        var eqAfter = await verifyEq.Content.ReadFromJsonAsync<EquipmentDto>();
        eqAfter!.Status.Should().Be("retired", "BR-13: equipment is retired, never hard-deleted");
    }

    [Fact]
    public async Task AF09_AF10_AdminOnlyAccess_DeniesUnauthenticatedCustomerAndBarber()
    {
        // Arrange
        await EnsureSeededAsync();
        var anonClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var customerClient = await CreateAuthenticatedClientAsync(CustomerEmail, DemoPassword);
        var barberClient = await CreateAuthenticatedClientAsync(BarberEmail, DemoPassword);
        var adminClient = await CreateAuthenticatedClientAsync(AdminEmail, AdminPassword);

        // 1. Anonymous access to API returns 401 Unauthorized
        var anonResp = await anonClient.GetAsync("/api/admin/inventory/products");
        anonResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 2. Customer access to API returns 403 Forbidden
        var custApiResp = await customerClient.GetAsync("/api/admin/inventory/products");
        custApiResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var custAdjResp = await customerClient.PostAsJsonAsync("/api/admin/inventory/products/1/adjust", new AdjustStockRequest
        {
            Quantity = 1,
            Reason = "Unauthorized adjustment attempt"
        });
        custAdjResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 3. Barber access to API returns 403 Forbidden
        var barbApiResp = await barberClient.GetAsync("/api/admin/inventory/products");
        barbApiResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var barbEqResp = await barberClient.GetAsync("/api/admin/inventory/equipment");
        barbEqResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 4. Customer access to /admin/inventory redirects to AccessDenied
        var custMvcResp = await customerClient.GetAsync("/admin/inventory");
        custMvcResp.StatusCode.Should().Be(HttpStatusCode.Redirect);
        custMvcResp.Headers.Location!.ToString().Should().Contain("AccessDenied");

        // 5. Admin access succeeds
        var adminMvcResp = await adminClient.GetAsync("/admin/inventory");
        adminMvcResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var adminApiResp = await adminClient.GetAsync("/api/admin/inventory/products");
        adminApiResp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task INV04_MovementHistory_ListsAuditedMovementsForProduct()
    {
        // Arrange
        await EnsureSeededAsync();
        var adminClient = await CreateAuthenticatedClientAsync(AdminEmail, AdminPassword);

        var sku = $"TEST-HIST-{Guid.NewGuid():N}"[..14].ToUpperInvariant();
        var createResponse = await adminClient.PostAsJsonAsync("/api/admin/inventory/products", new CreateProductRequest
        {
            Sku = sku,
            Name = "History Audit Pomade",
            Category = "Styling",
            Unit = "pcs",
            CostPrice = 200m,
            SellPrice = 400m,
            ReorderLevel = 5,
            InitialStock = 20
        });
        var product = await createResponse.Content.ReadFromJsonAsync<ProductDto>();

        // Make an adjustment
        await adminClient.PostAsJsonAsync($"/api/admin/inventory/products/{product!.Id}/adjust", new AdjustStockRequest
        {
            Quantity = -2,
            Reason = "Damaged during handling"
        });

        // Act: Fetch movement history
        var historyResp = await adminClient.GetAsync($"/api/admin/inventory/products/{product.Id}/movements");
        historyResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var movements = await historyResp.Content.ReadFromJsonAsync<List<StockMovementDto>>();
        movements.Should().NotBeNull();
        movements!.Count.Should().Be(2); // Initial stock movement + adjustment movement
        movements.Should().Contain(m => m.Quantity == 20 && m.Reason == "Initial inventory stock count");
        movements.Should().Contain(m => m.Quantity == -2 && m.Reason == "Damaged during handling");
    }
}

