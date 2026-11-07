using System.Net;
using System.Net.Http.Json;
using BarberBook.Data;
using BarberBook.Data.Seeding;
using BarberBook.Web.Models.Admin;
using BarberBook.Web.Models.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BarberBook.Tests;

public class BranchFeatureTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private const string AdminEmail = "admin@barberbook.local";
    private const string AdminPassword = "Admin@BarberBook2026!";
    private const string DemoPassword = "Demo@BarberBook2026!";

    public BranchFeatureTests(WebApplicationFactory<Program> factory)
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
    public async Task BRN_01_Admin_CanListBranches_AndSeededBranchesExist()
    {
        await EnsureSeededAsync();
        var adminClient = await CreateAuthenticatedClientAsync(AdminEmail, AdminPassword);

        // 1. Web HTML view
        var htmlResponse = await adminClient.GetAsync("/admin/branches");
        htmlResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await htmlResponse.Content.ReadAsStringAsync();
        html.Should().Contain("Branches Management");
        html.Should().Contain("Manila Main Branch");
        html.Should().Contain("Makati Poblacion Branch");

        // 2. JSON API endpoint
        var apiResponse = await adminClient.GetAsync("/api/admin/branches");
        apiResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await apiResponse.Content.ReadFromJsonAsync<BranchListApiResponse>();
        json.Should().NotBeNull();
        json!.Items.Should().Contain(b => b.Name == "Manila Main Branch");
        json.Items.Should().Contain(b => b.Name == "Makati Poblacion Branch");
    }

    [Fact]
    public async Task BRN_02_Admin_CanCreateNewBranch_AndToggleStatus()
    {
        await EnsureSeededAsync();
        var adminClient = await CreateAuthenticatedClientAsync(AdminEmail, AdminPassword);

        var newBranch = new CreateBranchRequest
        {
            Name = "Alabang Town Center Branch",
            Address = "Commerce Ave., Alabang, Muntinlupa",
            PhoneNumber = "0917-888-9999",
            OpeningTime = "10:00",
            ClosingTime = "19:00"
        };

        var createResponse = await adminClient.PostAsJsonAsync("/api/admin/branches", newBranch);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<AdminBranchItemDto>();
        created.Should().NotBeNull();
        created!.Name.Should().Be(newBranch.Name);
        created.Address.Should().Be(newBranch.Address);
        created.IsActive.Should().BeTrue();

        // Toggle status (deactivate)
        var toggleResponse = await adminClient.PostAsync($"/api/admin/branches/{created.Id}/toggle-status", null);
        toggleResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify deactivated in database
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
        var updated = await db.Branches.FindAsync(created.Id);
        updated.Should().NotBeNull();
        updated!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task BRN_03_Barbers_AreLinkedToBranches_AndVisibleInAdminBarbers()
    {
        await EnsureSeededAsync();
        var adminClient = await CreateAuthenticatedClientAsync(AdminEmail, AdminPassword);

        var response = await adminClient.GetAsync("/admin/barbers");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();

        // Ramon & Dennis at Manila Main Branch, Miguel at Makati Poblacion Branch
        html.Should().Contain("Manila Main Branch");
        html.Should().Contain("Makati Poblacion Branch");
    }

    [Fact]
    public async Task BRN_04_UnauthorizedUser_CannotAccessBranchManagement()
    {
        await EnsureSeededAsync();
        var anonClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // Anonymous user should be redirected to login
        var anonResponse = await anonClient.GetAsync("/admin/branches");
        anonResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        anonResponse.Headers.Location?.ToString().Should().ContainEquivalentOf("/login");

        // Customer user should be denied access (302 redirect to AccessDenied or 403 Forbidden)
        var customerClient = await CreateAuthenticatedClientAsync("juan@barberbook.local", DemoPassword);
        var custResponse = await customerClient.GetAsync("/admin/branches");
        if (custResponse.StatusCode == HttpStatusCode.Redirect)
        {
            custResponse.Headers.Location!.ToString().Should().Contain("AccessDenied");
        }
        else
        {
            custResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        var custApiResponse = await customerClient.GetAsync("/api/admin/branches");
        custApiResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private class BranchListApiResponse
    {
        public List<AdminBranchItemDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
    }
}
