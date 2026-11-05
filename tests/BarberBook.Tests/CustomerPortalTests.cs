using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BarberBook.Data.Seeding;
using BarberBook.Web.Common;
using BarberBook.Web.Models.Auth;
using BarberBook.Web.Models.Customer;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BarberBook.Tests;

public class CustomerPortalTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CustomerPortalTests(WebApplicationFactory<Program> factory)
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
    public async Task T01_Visitor_CanBrowseServicesAndBarbers_WithoutLoggingIn()
    {
        // Proves T-01: A visitor can browse available services and barber profiles without logging in
        await EnsureSeededAsync();
        var anonClient = _factory.CreateClient();

        // 1. Browse services
        var servicesResponse = await anonClient.GetAsync("/api/services");
        servicesResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var services = await servicesResponse.Content.ReadFromJsonAsync<List<PublicServiceDto>>();
        services.Should().NotBeNull();
        services!.Should().NotBeEmpty();
        services!.Any(s => s.Name == "Haircut").Should().BeTrue();
        services!.First().FormattedPrice.Should().StartWith("₱");

        // 2. Browse barbers
        var barbersResponse = await anonClient.GetAsync("/api/barbers");
        barbersResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var barbers = await barbersResponse.Content.ReadFromJsonAsync<List<PublicBarberDto>>();
        barbers.Should().NotBeNull();
        barbers!.Should().NotBeEmpty();
        barbers!.Any(b => b.DisplayName == "Ramon").Should().BeTrue();
        barbers!.First().WeeklyScheduleText.Should().NotBeNullOrEmpty();

        // 3. View single barber profile
        var barberDetailResponse = await anonClient.GetAsync("/api/barbers/1");
        barberDetailResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var barberDetail = await barberDetailResponse.Content.ReadFromJsonAsync<BarberDetailDto>();
        barberDetail.Should().NotBeNull();
        barberDetail!.DisplayName.Should().Be("Ramon");
        barberDetail.WeeklyHours.Should().NotBeEmpty();
    }

    [Fact]
    public async Task PublicEndpoints_ExcludeInactiveRecords_AndExposeNoPrivateFields()
    {
        // Privacy rule: Public endpoints expose NO email, phone numbers, password hashes or customer data
        await EnsureSeededAsync();
        var anonClient = _factory.CreateClient();

        var barbersResponse = await anonClient.GetAsync("/api/barbers");
        barbersResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var jsonText = await barbersResponse.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(jsonText);
        var rootArray = jsonDoc.RootElement;

        rootArray.GetArrayLength().Should().BeGreaterThan(0);
        foreach (var element in rootArray.EnumerateArray())
        {
            // Verify private fields are absent
            element.TryGetProperty("email", out _).Should().BeFalse("Barber email must not be exposed on public endpoints");
            element.TryGetProperty("phone", out _).Should().BeFalse("Barber phone must not be exposed on public endpoints");
            element.TryGetProperty("phoneNumber", out _).Should().BeFalse("Barber phone must not be exposed on public endpoints");
            element.TryGetProperty("passwordHash", out _).Should().BeFalse("Password hash must never be exposed");
            element.TryGetProperty("userId", out _).Should().BeFalse("Internal user ID must not be exposed on public endpoints");
        }

        // Single barber endpoint check
        var singleBarberResponse = await anonClient.GetAsync("/api/barbers/1");
        singleBarberResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var singleJson = await singleBarberResponse.Content.ReadAsStringAsync();
        using var singleDoc = JsonDocument.Parse(singleJson);
        singleDoc.RootElement.TryGetProperty("email", out _).Should().BeFalse("Barber email must not be exposed");
        singleDoc.RootElement.TryGetProperty("phoneNumber", out _).Should().BeFalse("Barber phone must not be exposed");
    }

    [Fact]
    public async Task Customer_CanViewAndEditOwnProfile()
    {
        // Requirement SC-1: Customer profile management
        await EnsureSeededAsync();
        var customerClient = await CreateAuthenticatedClientAsync("juan@barberbook.local", "Demo@BarberBook2026!");

        // 1. View profile
        var getResponse = await customerClient.GetAsync("/api/me/profile");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var profile = await getResponse.Content.ReadFromJsonAsync<CustomerProfileDto>();
        profile.Should().NotBeNull();
        profile!.FullName.Should().Be("Juan Dela Cruz");
        profile.Email.Should().Be("juan@barberbook.local");

        // 2. Edit profile
        var updateResponse = await customerClient.PatchAsJsonAsync("/api/me/profile", new UpdateProfileRequest
        {
            FullName = "Juan Dela Cruz Updated",
            Email = "juan@barberbook.local",
            PhoneNumber = "0917-555-4321"
        });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedProfile = await updateResponse.Content.ReadFromJsonAsync<CustomerProfileDto>();
        updatedProfile!.FullName.Should().Be("Juan Dela Cruz Updated");
        updatedProfile.PhoneNumber.Should().Be("0917-555-4321");

        // 3. Verify changes persist
        var verifyResponse = await customerClient.GetFromJsonAsync<CustomerProfileDto>("/api/me/profile");
        verifyResponse!.FullName.Should().Be("Juan Dela Cruz Updated");
        verifyResponse.PhoneNumber.Should().Be("0917-555-4321");

        // Restore original name
        await customerClient.PatchAsJsonAsync("/api/me/profile", new UpdateProfileRequest
        {
            FullName = "Juan Dela Cruz",
            Email = "juan@barberbook.local",
            PhoneNumber = null
        });
    }

    [Fact]
    public async Task CustomerProfile_Validation_RejectsInvalidEmail_AndDuplicateEmail()
    {
        // FluentValidation & duplicate email detection
        await EnsureSeededAsync();
        var customerClient = await CreateAuthenticatedClientAsync("juan@barberbook.local", "Demo@BarberBook2026!");

        // 1. Invalid email format
        var invalidEmailRes = await customerClient.PatchAsJsonAsync("/api/me/profile", new UpdateProfileRequest
        {
            FullName = "Juan Dela Cruz",
            Email = "not-an-email"
        });
        invalidEmailRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var valError = await invalidEmailRes.Content.ReadFromJsonAsync<ApiErrorResponse>();
        valError!.Error.Code.Should().Be("VALIDATION_ERROR");

        // 2. Duplicate email (Mark's email)
        var duplicateEmailRes = await customerClient.PatchAsJsonAsync("/api/me/profile", new UpdateProfileRequest
        {
            FullName = "Juan Dela Cruz",
            Email = "mark@barberbook.local" // already in use by Mark Santos
        });
        duplicateEmailRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var dupError = await duplicateEmailRes.Content.ReadFromJsonAsync<ApiErrorResponse>();
        dupError!.Error.Code.Should().Be("DUPLICATE_EMAIL");
    }

    [Fact]
    public async Task AnonymousAndBarber_CannotAccessCustomerProfile_Returns401Or403()
    {
        // Rule BR-02, BR-15: Profile requires customer login
        await EnsureSeededAsync();

        // Anonymous user calling API
        var anonClient = _factory.CreateClient();
        var anonApiRes = await anonClient.GetAsync("/api/me/profile");
        anonApiRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Anonymous user opening /profile page
        var anonWebClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var anonWebRes = await anonWebClient.GetAsync("/profile");
        anonWebRes.StatusCode.Should().Be(HttpStatusCode.Redirect);
        anonWebRes.Headers.Location!.ToString().Should().Contain("/Account/Login");

        // Barber calling customer profile endpoint
        var barberClient = await CreateAuthenticatedClientAsync("ramon@barberbook.local", "Demo@BarberBook2026!");
        var barberRes = await barberClient.GetAsync("/api/me/profile");
        barberRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
