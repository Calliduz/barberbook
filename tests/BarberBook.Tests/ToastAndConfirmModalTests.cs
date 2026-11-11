using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace BarberBook.Tests;

public class ToastAndConfirmModalTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ToastAndConfirmModalTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private static string GetRepositoryRoot()
    {
        var basePath = AppContext.BaseDirectory;
        var currentDir = new DirectoryInfo(basePath);
        while (currentDir != null && !Directory.Exists(Path.Combine(currentDir.FullName, "src")))
        {
            currentDir = currentDir.Parent;
        }

        currentDir.Should().NotBeNull("Must find repository root containing src");
        return currentDir!.FullName;
    }

    [Fact]
    public void EO4_P18_ToastComponent_ContainsAccessibleContainerAndMethods()
    {
        var root = GetRepositoryRoot();
        var toastPath = Path.Combine(root, "src", "BarberBook.Web", "Views", "Shared", "_Toast.cshtml");
        File.Exists(toastPath).Should().BeTrue();

        var content = File.ReadAllText(toastPath);

        // Accessibility & Container
        content.Should().Contain("id=\"barberbook-toast-container\"");
        content.Should().Contain("aria-live=\"polite\"");
        content.Should().Contain("aria-atomic=\"true\"");

        // Toast API
        content.Should().Contain("window.showToast = function");
        content.Should().Contain("window.toast = {");
        content.Should().Contain("toast.classList.add('bg-emerald-50'");
        content.Should().Contain("dark:bg-emerald-950/90");
        content.Should().Contain("dark:bg-rose-950/90");
        content.Should().Contain("dark:bg-amber-950/90");
        content.Should().Contain("dark:bg-slate-900/95");

        // Progress bar & pause on hover
        content.Should().Contain("toast-progress");
        content.Should().Contain("mouseenter");
        content.Should().Contain("mouseleave");

        // TempData integrations
        content.Should().Contain("TempData[\"SuccessMessage\"]");
        content.Should().Contain("TempData[\"ErrorMessage\"]");
        content.Should().Contain("TempData[\"WarningMessage\"]");
        content.Should().Contain("TempData[\"InfoMessage\"]");
    }

    [Fact]
    public void EO4_P18_ConfirmDialogComponent_ContainsAccessibleDialogAndDeclarativeBindings()
    {
        var root = GetRepositoryRoot();
        var confirmPath = Path.Combine(root, "src", "BarberBook.Web", "Views", "Shared", "_ConfirmDialog.cshtml");
        File.Exists(confirmPath).Should().BeTrue();

        var content = File.ReadAllText(confirmPath);

        // Accessible Modal semantics
        content.Should().Contain("id=\"barberbook-confirm-dialog\"");
        content.Should().Contain("role=\"dialog\"");
        content.Should().Contain("aria-modal=\"true\"");
        content.Should().Contain("aria-labelledby=\"confirm-dialog-title\"");
        content.Should().Contain("aria-describedby=\"confirm-dialog-desc\"");

        // Dark mode & Vintage Modern Styling
        content.Should().Contain("backdrop-blur-sm");
        content.Should().Contain("dark:bg-slate-900");
        content.Should().Contain("dark:border-slate-800");

        // Standard vs Destructive styling
        content.Should().Contain("confirm-dialog-icon-destructive");
        content.Should().Contain("bg-rose-600");
        content.Should().Contain("bg-amber-500");

        // Global APIs
        content.Should().Contain("window.showConfirmDialog = function");
        content.Should().Contain("window.confirmModal = window.showConfirmDialog");

        // Declarative data-confirm handlers
        content.Should().Contain("data-confirm");
        content.Should().Contain("Escape");
    }

    [Fact]
    public void EO4_AllLayouts_IncludeToastAndConfirmDialogPartials()
    {
        var root = GetRepositoryRoot();
        var customerLayout = File.ReadAllText(Path.Combine(root, "src", "BarberBook.Web", "Views", "Shared", "_LayoutCustomer.cshtml"));
        var adminLayout = File.ReadAllText(Path.Combine(root, "src", "BarberBook.Web", "Views", "Shared", "_LayoutAdmin.cshtml"));
        var barberLayout = File.ReadAllText(Path.Combine(root, "src", "BarberBook.Web", "Views", "Shared", "_LayoutBarber.cshtml"));

        customerLayout.Should().Contain("<partial name=\"_Toast\" />");
        customerLayout.Should().Contain("<partial name=\"_ConfirmDialog\" />");

        adminLayout.Should().Contain("<partial name=\"_Toast\" />");
        adminLayout.Should().Contain("<partial name=\"_ConfirmDialog\" />");

        barberLayout.Should().Contain("<partial name=\"_Toast\" />");
        barberLayout.Should().Contain("<partial name=\"_ConfirmDialog\" />");
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/services")]
    [InlineData("/products")]
    [InlineData("/barbers")]
    [InlineData("/login")]
    [InlineData("/register")]
    public async Task EO4_PublicPages_RenderToastAndConfirmModalContainers(string url)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("barberbook-toast-container", $"URL '{url}' must contain toast notification container");
        html.Should().Contain("barberbook-confirm-dialog", $"URL '{url}' must contain confirm modal dialog");
    }
}

