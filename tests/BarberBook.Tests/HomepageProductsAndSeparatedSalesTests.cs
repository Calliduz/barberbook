using System.Net;
using System.Net.Http.Json;
using BarberBook.Data.Entities;
using BarberBook.Data.Enums;
using BarberBook.Data.Seeding;
using BarberBook.Web.Models.Auth;
using BarberBook.Web.Models.Inventory;
using BarberBook.Web.Models.Reports;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Xunit;

namespace BarberBook.Tests;

public class HomepageProductsAndSeparatedSalesTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HomepageProductsAndSeparatedSalesTests(WebApplicationFactory<Program> factory)
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
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginModel
        {
            Email = email,
            Password = password
        });
        loginRes.StatusCode.Should().Be(HttpStatusCode.OK);

        return client;
    }

    [Fact]
    public async Task Homepage_RendersProductsShowcase_WithStockAndPrice()
    {
        await EnsureSeededAsync();
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Artisan Grooming Products");
        html.Should().Contain("Classic High-Shine Pomade");
        html.Should().Contain("450.00");
        html.Should().Contain("In Stock");
    }

    [Fact]
    public async Task PublicProductsPage_ListsProducts_AndSupportsCategoryFilter()
    {
        await EnsureSeededAsync();
        var client = _factory.CreateClient();

        // 1. All products
        var response = await client.GetAsync("/products");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Artisan Grooming Products");
        html.Should().Contain("Classic High-Shine Pomade");
        html.Should().Contain("Artisanal Cedarwood Beard Oil");

        // 2. Filtered by Styling
        var filteredRes = await client.GetAsync("/products?category=Styling");
        filteredRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var filteredHtml = await filteredRes.Content.ReadAsStringAsync();
        filteredHtml.Should().Contain("Classic High-Shine Pomade");
    }

    [Fact]
    public async Task PublicProductsApi_ReturnsJsonProducts()
    {
        await EnsureSeededAsync();
        var client = _factory.CreateClient();

        var products = await client.GetFromJsonAsync<List<ProductDto>>("/api/products");
        products.Should().NotBeNull();
        products!.Count.Should().BeGreaterThan(0);
        products.Should().Contain(p => p.Name.Contains("Pomade"));
    }

    [Fact]
    public async Task AdminInventory_CanCreateAndEditProduct_AndAdjustStock()
    {
        await EnsureSeededAsync();
        var adminClient = await CreateAuthenticatedClientAsync("admin@barberbook.local", "Admin@BarberBook2026!");

        var uniqueSku = $"SKU-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        // 1. Create product via Form POST with AntiForgery token
        var getPage = await adminClient.GetAsync("/admin/inventory");
        getPage.StatusCode.Should().Be(HttpStatusCode.OK);
        var pageHtml = await getPage.Content.ReadAsStringAsync();
        var match = System.Text.RegularExpressions.Regex.Match(pageHtml, @"<input\s+[^>]*name=""__RequestVerificationToken""\s+[^>]*value=""([^""]+)""");
        var token = match.Groups[1].Value;

        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["sku"] = uniqueSku,
            ["name"] = "Luxury Sandalwood Aftershave Balm",
            ["category"] = "Aftercare",
            ["unit"] = "btl",
            ["costPrice"] = "180.00",
            ["sellPrice"] = "360.00",
            ["reorderLevel"] = "5",
            ["initialStock"] = "15"
        };

        var createRes = await adminClient.PostAsync("/admin/inventory/products/create", new FormUrlEncodedContent(form));
        createRes.StatusCode.Should().Be(HttpStatusCode.Redirect);

        // Verify product was created
        var allProducts = await adminClient.GetFromJsonAsync<List<ProductDto>>("/api/admin/inventory/products");
        var created = allProducts!.FirstOrDefault(p => p.Sku == uniqueSku);
        created.Should().NotBeNull();
        created!.Name.Should().Be("Luxury Sandalwood Aftershave Balm");
        created.CurrentStock.Should().Be(15);
        created.SellPrice.Should().Be(360.00m);

        // 2. Edit product via Form POST
        var editForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["name"] = "Luxury Sandalwood Aftershave Balm (Updated)",
            ["category"] = "Aftercare",
            ["unit"] = "btl",
            ["costPrice"] = "200.00",
            ["sellPrice"] = "395.00",
            ["reorderLevel"] = "8"
        };

        var editRes = await adminClient.PostAsync($"/admin/inventory/products/{created.Id}/edit", new FormUrlEncodedContent(editForm));
        editRes.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var updated = await adminClient.GetFromJsonAsync<ProductDto>($"/api/admin/inventory/products/{created.Id}");
        updated.Should().NotBeNull();
        updated!.Name.Should().Be("Luxury Sandalwood Aftershave Balm (Updated)");
        updated.SellPrice.Should().Be(395.00m);
        updated.ReorderLevel.Should().Be(8);

        // 3. Adjust stock via Form POST
        var adjustForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["quantity"] = "10",
            ["reason"] = "Restock from direct supplier"
        };

        var adjustRes = await adminClient.PostAsync($"/admin/inventory/products/{created.Id}/adjust", new FormUrlEncodedContent(adjustForm));
        adjustRes.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var adjusted = await adminClient.GetFromJsonAsync<ProductDto>($"/api/admin/inventory/products/{created.Id}");
        adjusted!.CurrentStock.Should().Be(25); // 15 + 10
    }

    [Fact]
    public async Task DailySalesReport_SeparatesServiceSalesAndProductSales_WithSubtotals()
    {
        await EnsureSeededAsync();
        var adminClient = await CreateAuthenticatedClientAsync("admin@barberbook.local", "Admin@BarberBook2026!");

        // Query daily report for the benchmark date Sep 14, 2026
        var response = await adminClient.GetAsync("/admin/reports?tab=daily&date=2026-09-14");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var html = await response.Content.ReadAsStringAsync();

        // Must show Service Sales separately from Product Sales
        html.Should().Contain("Service Sales (Completed)");
        html.Should().Contain("Product Sales (Retail)");
        html.Should().Contain("Grand Total Revenue");

        // Must contain Type filter tabs
        html.Should().Contain("filterBtnService");
        html.Should().Contain("filterBtnProduct");
        html.Should().Contain("filterSalesJournal");

        // Must contain separate breakdown cards
        html.Should().Contain("Breakdown by Master Barber");
        html.Should().Contain("Breakdown by Service Menu");
        html.Should().Contain("Breakdown by Retail Product");
    }

    [Fact]
    public async Task MonthlySalesReport_ShowsProductSalesBreakdown_BesideServices()
    {
        await EnsureSeededAsync();
        var adminClient = await CreateAuthenticatedClientAsync("admin@barberbook.local", "Admin@BarberBook2026!");

        var response = await adminClient.GetAsync("/admin/reports?tab=monthly&month=9&year=2026");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Monthly Breakdown by Barber");
        html.Should().Contain("Monthly Breakdown by Service");
        html.Should().Contain("Monthly Breakdown by Product");
    }
}

