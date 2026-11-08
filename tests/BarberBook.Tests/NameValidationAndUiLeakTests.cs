using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BarberBook.Data.Entities;
using BarberBook.Data.Seeding;
using BarberBook.Web.Common;
using BarberBook.Web.Models.Admin;
using BarberBook.Web.Models.Auth;
using BarberBook.Web.Models.Customer;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BarberBook.Tests;

public class NameValidationAndUiLeakTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public NameValidationAndUiLeakTests(WebApplicationFactory<Program> factory)
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
            AllowAutoRedirect = false
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
    public async Task CF1_Register_RequiresFirstNameAndLastName_WhenEmpty()
    {
        await EnsureSeededAsync();
        var client = _factory.CreateClient();

        var emptyModel = new RegisterModel
        {
            FirstName = "",
            LastName = "",
            Email = "newbie@test.local",
            Password = "Password123!",
            ConfirmPassword = "Password123!"
        };

        var response = await client.PostAsJsonAsync("/api/auth/register", emptyModel);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("First name is required.");
    }

    [Fact]
    public async Task CF1_Register_AcceptsSplitFirstAndLastName()
    {
        await EnsureSeededAsync();
        var client = _factory.CreateClient();
        var uniqueEmail = $"split_{Guid.NewGuid():N}@test.local";

        var payload = new RegisterModel
        {
            FirstName = "Carlos",
            LastName = "Santana",
            Email = uniqueEmail,
            Password = "Password123!",
            ConfirmPassword = "Password123!"
        };

        var response = await client.PostAsJsonAsync("/api/auth/register", payload);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(uniqueEmail);

        user.Should().NotBeNull();
        user!.FullName.Should().Be("Carlos Santana");
    }

    [Fact]
    public async Task CF1_CustomerProfile_ValidatesFirstAndLastName()
    {
        await EnsureSeededAsync();
        var client = await CreateAuthenticatedClientAsync("juan@barberbook.local", "Demo@BarberBook2026!");

        var invalidPayload = new UpdateProfileRequest
        {
            FirstName = "",
            LastName = "",
            Email = "juan@barberbook.local"
        };

        var response = await client.PatchAsJsonAsync("/api/me/profile", invalidPayload);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("First name is required.");
    }

    [Fact]
    public async Task Admin_CustomerEdit_ValidatesFirstAndLastName()
    {
        await EnsureSeededAsync();
        var adminClient = await CreateAuthenticatedClientAsync("admin@barberbook.local", "Admin@BarberBook2026!");

        var invalidPayload = new AdminCustomerEditProfileModel
        {
            FirstName = "",
            LastName = "",
            Email = "juan@barberbook.local"
        };

        var response = await adminClient.PutAsJsonAsync("/api/admin/customers/3", invalidPayload);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("First name is required.");
    }

    [Fact]
    public async Task UiLeak_BrowserRequest_ReturnsHtmlErrorPage_NotRawJson()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Simulating a browser navigation requesting text/html
        var request = new HttpRequestMessage(HttpMethod.Get, "/Home/StatusCodePage?code=500");
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");

        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("BARBERBOOK SYSTEM NOTICE");
        html.Should().NotContain("{\"error\":");
    }
}

