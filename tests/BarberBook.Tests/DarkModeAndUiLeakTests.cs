using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace BarberBook.Tests;

public class DarkModeAndUiLeakTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public DarkModeAndUiLeakTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public void CF1_CF2_DarkMode_Layouts_ContainDarkBgOnRootHtml()
    {
        var basePath = AppContext.BaseDirectory;
        // Search upward for src directory
        var currentDir = new DirectoryInfo(basePath);
        while (currentDir != null && !Directory.Exists(Path.Combine(currentDir.FullName, "src")))
        {
            currentDir = currentDir.Parent;
        }

        currentDir.Should().NotBeNull("Must find repository root containing src");
        var root = currentDir!.FullName;

        var customerLayout = File.ReadAllText(Path.Combine(root, "src", "BarberBook.Web", "Views", "Shared", "_LayoutCustomer.cshtml"));
        var adminLayout = File.ReadAllText(Path.Combine(root, "src", "BarberBook.Web", "Views", "Shared", "_LayoutAdmin.cshtml"));
        var barberLayout = File.ReadAllText(Path.Combine(root, "src", "BarberBook.Web", "Views", "Shared", "_LayoutBarber.cshtml"));

        customerLayout.Should().Contain("dark:bg-[#090e17]");
        adminLayout.Should().Contain("dark:bg-[#090e17]");
        barberLayout.Should().Contain("dark:bg-[#090e17]");
    }

    [Fact]
    public void CF4_DarkMode_SiteCss_ContainsFormSurfaceAndContrastRules()
    {
        var basePath = AppContext.BaseDirectory;
        var currentDir = new DirectoryInfo(basePath);
        while (currentDir != null && !Directory.Exists(Path.Combine(currentDir.FullName, "src")))
        {
            currentDir = currentDir.Parent;
        }

        var root = currentDir!.FullName;
        var siteCss = File.ReadAllText(Path.Combine(root, "src", "BarberBook.Web", "wwwroot", "css", "site.css"));

        // Universal dark form controls
        siteCss.Should().Contain("html.dark input[type=\"text\"]");
        siteCss.Should().Contain("html.dark select");
        // Translucent card surface support
        siteCss.Should().Contain("html.dark .bg-white\\/95");
        // CTA contrast lock (amber/brand buttons must never turn into white text on gold background)
        siteCss.Should().Contain("html.dark .bg-amber-500");
        siteCss.Should().Contain("color: #020617 !important;");
    }

    [Fact]
    public void CF1_CF2_DarkMode_AuthViews_ContainDarkCardAndTextClasses()
    {
        var basePath = AppContext.BaseDirectory;
        var currentDir = new DirectoryInfo(basePath);
        while (currentDir != null && !Directory.Exists(Path.Combine(currentDir.FullName, "src")))
        {
            currentDir = currentDir.Parent;
        }

        var root = currentDir!.FullName;
        var loginView = File.ReadAllText(Path.Combine(root, "src", "BarberBook.Web", "Views", "Account", "Login.cshtml"));
        var registerView = File.ReadAllText(Path.Combine(root, "src", "BarberBook.Web", "Views", "Account", "Register.cshtml"));

        loginView.Should().Contain("dark:bg-slate-900/95");
        loginView.Should().Contain("dark:border-slate-800");
        loginView.Should().Contain("dark:text-white");

        registerView.Should().Contain("dark:bg-slate-900/95");
        registerView.Should().Contain("dark:border-slate-800");
        registerView.Should().Contain("dark:text-white");
    }

    [Fact]
    public void CF4_DarkMode_BookingAndAppointments_ContainDarkClasses()
    {
        var basePath = AppContext.BaseDirectory;
        var currentDir = new DirectoryInfo(basePath);
        while (currentDir != null && !Directory.Exists(Path.Combine(currentDir.FullName, "src")))
        {
            currentDir = currentDir.Parent;
        }

        var root = currentDir!.FullName;
        var bookingView = File.ReadAllText(Path.Combine(root, "src", "BarberBook.Web", "Views", "Booking", "Index.cshtml"));
        var appointmentsView = File.ReadAllText(Path.Combine(root, "src", "BarberBook.Web", "Views", "Home", "MyAppointments.cshtml"));

        // Booking dark mode slot classes
        bookingView.Should().Contain("dark:border-slate-700");
        bookingView.Should().Contain("dark:bg-slate-800");
        bookingView.Should().Contain("dark:text-slate-200");
        // Appointments dark mode container and dynamic card classes
        appointmentsView.Should().Contain("dark:bg-slate-900");
        appointmentsView.Should().Contain("dark:border-slate-800");
        appointmentsView.Should().Contain("reschedule-slot-btn py-2.5 px-2 text-center rounded-xl bg-slate-50 dark:bg-slate-800");
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/services")]
    [InlineData("/products")]
    [InlineData("/barbers")]
    [InlineData("/login")]
    [InlineData("/register")]
    public async Task CF1_DarkMode_RenderedPages_ReturnHtmlWithDarkInitScript(string path)
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");

        var html = await response.Content.ReadAsStringAsync();
        // Check for anti-FOUC script
        html.Should().Contain("barberbook_theme");
        html.Should().Contain("document.documentElement.classList.add('dark')");
    }

    [Fact]
    public void EO4_EmptyPlaceholder_SharedComponents_ContainDarkMode()
    {
        var basePath = AppContext.BaseDirectory;
        var currentDir = new DirectoryInfo(basePath);
        while (currentDir != null && !Directory.Exists(Path.Combine(currentDir.FullName, "src")))
        {
            currentDir = currentDir.Parent;
        }

        var root = currentDir!.FullName;
        var skeleton = File.ReadAllText(Path.Combine(root, "src", "BarberBook.Web", "Views", "Shared", "_Skeleton.cshtml"));
        var emptyState = File.ReadAllText(Path.Combine(root, "src", "BarberBook.Web", "Views", "Shared", "_EmptyState.cshtml"));
        var errorState = File.ReadAllText(Path.Combine(root, "src", "BarberBook.Web", "Views", "Shared", "_ErrorState.cshtml"));

        skeleton.Should().Contain("dark:bg-slate-900");
        skeleton.Should().Contain("dark:border-slate-800");
        skeleton.Should().Contain("dark:bg-slate-800");

        emptyState.Should().Contain("dark:bg-slate-900");
        emptyState.Should().Contain("dark:border-slate-800");

        errorState.Should().Contain("dark:bg-rose-950/40");
        errorState.Should().Contain("dark:border-rose-900/60");
    }

    [Fact]
    public void CF1_FormViews_DoNotContainEmptyPlaceholders()
    {
        var basePath = AppContext.BaseDirectory;
        var currentDir = new DirectoryInfo(basePath);
        while (currentDir != null && !Directory.Exists(Path.Combine(currentDir.FullName, "src")))
        {
            currentDir = currentDir.Parent;
        }

        var root = currentDir!.FullName;
        var files = Directory.GetFiles(Path.Combine(root, "src", "BarberBook.Web"), "*.cshtml", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            text.Should().NotContain("placeholder=\"\"", $"View {Path.GetFileName(file)} must not contain empty placeholder attributes.");
        }
    }
}

