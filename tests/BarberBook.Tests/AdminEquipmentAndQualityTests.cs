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
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Xunit;

namespace BarberBook.Tests;

public class AdminEquipmentAndQualityTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private const string AdminEmail = "admin@barberbook.local";
    private const string AdminPassword = "Admin@BarberBook2026!";
    private const string RamonEmail = "ramon@barberbook.local";
    private const string DennisEmail = "dennis@barberbook.local";
    private const string CustomerEmail = "juan@barberbook.local";
    private const string DemoPassword = "Demo@BarberBook2026!";

    public AdminEquipmentAndQualityTests(WebApplicationFactory<Program> factory)
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
    public async Task T16_HW1_INV01_AssignEquipment_ClosesPreviousActiveAssignmentInSameTransaction()
    {
        // Arrange
        await EnsureSeededAsync();
        var adminClient = await CreateAuthenticatedClientAsync(AdminEmail, AdminPassword);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();

        var ramon = await db.Barbers.FirstAsync(b => b.DisplayName == "Ramon");
        var dennis = await db.Barbers.FirstAsync(b => b.DisplayName == "Dennis");

        // Create dedicated test equipment to test re-assignment
        var tag = $"EQ-TST-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var eq = new Equipment
        {
            AssetTag = tag,
            Name = "Reassignment Test Clipper",
            Category = "Clippers",
            Condition = EquipmentCondition.Good,
            Status = EquipmentStatus.InService,
            InspectionIntervalDays = 30
        };
        db.Equipment.Add(eq);
        await db.SaveChangesAsync();

        // 1. First assignment: Assign to Ramon
        var assign1 = await adminClient.PostAsJsonAsync($"/api/admin/equipment/{eq.Id}/assign", new AssignEquipmentRequest
        {
            BarberId = ramon.Id,
            Notes = "Initial assignment to Ramon"
        });
        assign1.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify active assignment to Ramon
        using (var verifyScope1 = _factory.Services.CreateScope())
        {
            var verifyDb = verifyScope1.ServiceProvider.GetRequiredService<BarberBookDbContext>();
            var active = await verifyDb.EquipmentAssignments
                .FirstOrDefaultAsync(a => a.EquipmentId == eq.Id && a.ReturnedAt == null);
            active.Should().NotBeNull();
            active!.BarberId.Should().Be(ramon.Id);
        }

        // 2. Re-assignment: Assign same equipment to Dennis (HW-1, INV-01, T-16)
        var assign2 = await adminClient.PostAsJsonAsync($"/api/admin/equipment/{eq.Id}/assign", new AssignEquipmentRequest
        {
            BarberId = dennis.Id,
            Notes = "Transferred to Dennis station"
        });
        assign2.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert: previous assignment to Ramon is closed (returned_at != null), Dennis is now the active assignment
        using (var verifyScope2 = _factory.Services.CreateScope())
        {
            var verifyDb = verifyScope2.ServiceProvider.GetRequiredService<BarberBookDbContext>();
            var allAssignments = await verifyDb.EquipmentAssignments
                .Where(a => a.EquipmentId == eq.Id)
                .OrderBy(a => a.AssignedAt)
                .ToListAsync();

            allAssignments.Count.Should().Be(2);

            var ramonAssignment = allAssignments.First(a => a.BarberId == ramon.Id);
            ramonAssignment.ReturnedAt.Should().NotBeNull("Previous assignment must be closed when reassigned (INV-01)");

            var dennisAssignment = allAssignments.First(a => a.BarberId == dennis.Id);
            dennisAssignment.ReturnedAt.Should().BeNull("New assignment must be active");
        }
    }

    [Fact]
    public async Task T16_BF6_INV07_BarberCanOnlySeeAndInspectOwnAssignedEquipment()
    {
        // Arrange
        await EnsureSeededAsync();
        var ramonClient = await CreateAuthenticatedClientAsync(RamonEmail, DemoPassword);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();

        var ramon = await db.Barbers.FirstAsync(b => b.DisplayName == "Ramon");
        var dennis = await db.Barbers.FirstAsync(b => b.DisplayName == "Dennis");

        // Tool 1: assigned to Ramon
        var ramonTool = new Equipment
        {
            AssetTag = $"EQ-RMN-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            Name = "Ramon Private Trimmer",
            Category = "Trimmers",
            Condition = EquipmentCondition.Good,
            Status = EquipmentStatus.InService
        };
        db.Equipment.Add(ramonTool);
        await db.SaveChangesAsync();

        db.EquipmentAssignments.Add(new EquipmentAssignment
        {
            EquipmentId = ramonTool.Id,
            BarberId = ramon.Id,
            AssignedAt = SystemClock.Instance.GetCurrentInstant()
        });

        // Tool 2: assigned to Dennis
        var dennisTool = new Equipment
        {
            AssetTag = $"EQ-DNS-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            Name = "Dennis Dedicated Shears",
            Category = "Shears",
            Condition = EquipmentCondition.Excellent,
            Status = EquipmentStatus.InService
        };
        db.Equipment.Add(dennisTool);
        await db.SaveChangesAsync();

        db.EquipmentAssignments.Add(new EquipmentAssignment
        {
            EquipmentId = dennisTool.Id,
            BarberId = dennis.Id,
            AssignedAt = SystemClock.Instance.GetCurrentInstant()
        });

        // Tool 3: unassigned shop tool
        var shopTool = new Equipment
        {
            AssetTag = $"EQ-SHP-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            Name = "Shop Shared Clipper",
            Category = "Clippers",
            Condition = EquipmentCondition.Good,
            Status = EquipmentStatus.InService
        };
        db.Equipment.Add(shopTool);
        await db.SaveChangesAsync();

        // Act 1: Ramon fetches /api/barber/equipment
        var myEqResp = await ramonClient.GetAsync("/api/barber/equipment");
        myEqResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var myEquipment = await myEqResp.Content.ReadFromJsonAsync<List<EquipmentMonitoringItemDto>>();

        // Assert: Ramon sees only his assigned tool, not Dennis's or shop's
        myEquipment.Should().NotBeNull();
        myEquipment!.Should().Contain(e => e.Id == ramonTool.Id);
        myEquipment.Should().NotContain(e => e.Id == dennisTool.Id, "Barber must not see another barber's equipment (INV-07)");
        myEquipment.Should().NotContain(e => e.Id == shopTool.Id, "Barber must not see unassigned equipment (INV-07)");

        // Act 2: Ramon attempts to log an inspection on Dennis's tool -> 403 Forbidden
        var forbiddenResp1 = await ramonClient.PostAsJsonAsync($"/api/barber/equipment/{dennisTool.Id}/inspect", new LogEquipmentCheckRequest
        {
            Condition = EquipmentCondition.Good,
            Notes = "Illegal inspection attempt on another barber's tool"
        });
        forbiddenResp1.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Act 3: Ramon attempts to log an inspection on unassigned shop tool -> 403 Forbidden
        var forbiddenResp2 = await ramonClient.PostAsJsonAsync($"/api/barber/equipment/{shopTool.Id}/inspect", new LogEquipmentCheckRequest
        {
            Condition = EquipmentCondition.Good,
            Notes = "Illegal inspection attempt on unassigned shop tool"
        });
        forbiddenResp2.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Act 4: Ramon logs inspection on his OWN tool -> 200 OK
        var allowedResp = await ramonClient.PostAsJsonAsync($"/api/barber/equipment/{ramonTool.Id}/inspect", new LogEquipmentCheckRequest
        {
            Condition = EquipmentCondition.Fair,
            Notes = "Blades sanitized and tension verified by Ramon"
        });
        allowedResp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task T17_HW3_INV02_PoorConditionAndOverdueInspections_AreFlaggedOnMonitoring()
    {
        // Arrange
        await EnsureSeededAsync();
        var adminClient = await CreateAuthenticatedClientAsync(AdminEmail, AdminPassword);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
        var now = SystemClock.Instance.GetCurrentInstant();
        var zone = DateTimeZoneProviders.Tzdb["Asia/Manila"];
        var today = now.InZone(zone).Date;

        // 1. Tool that passes due date (Overdue item)
        var overdueTool = new Equipment
        {
            AssetTag = $"EQ-OVD-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            Name = "Overdue Inspection Dryer",
            Category = "Electrical",
            Condition = EquipmentCondition.Good,
            Status = EquipmentStatus.InService,
            InspectionIntervalDays = 30
        };
        db.Equipment.Add(overdueTool);
        await db.SaveChangesAsync();

        // Add an inspection with next_check_due 5 days in the past
        db.EquipmentChecks.Add(new EquipmentCheck
        {
            EquipmentId = overdueTool.Id,
            CheckedAt = now.Minus(Duration.FromDays(35)),
            Condition = EquipmentCondition.Good,
            NextCheckDue = today.Minus(Period.FromDays(5)),
            Notes = "Past inspection"
        });
        await db.SaveChangesAsync();

        // 2. Tool inspected with "Poor" condition
        var poorTool = new Equipment
        {
            AssetTag = $"EQ-POR-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            Name = "Damaged Blade Clipper",
            Category = "Clippers",
            Condition = EquipmentCondition.Good, // starts as Good
            Status = EquipmentStatus.InService,
            InspectionIntervalDays = 30
        };
        db.Equipment.Add(poorTool);
        await db.SaveChangesAsync();

        // Act: Admin logs an inspection recording "Poor"
        var inspectResp = await adminClient.PostAsJsonAsync($"/api/admin/equipment/{poorTool.Id}/inspect", new LogEquipmentCheckRequest
        {
            Condition = EquipmentCondition.Poor,
            Notes = "Chipped blade, dangerous for client use (T-17)"
        });
        inspectResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Fetch monitoring response
        var monResp = await adminClient.GetAsync("/api/admin/equipment/monitoring");
        monResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var monitoring = await monResp.Content.ReadFromJsonAsync<EquipmentMonitoringResponse>();

        // Assert:
        monitoring.Should().NotBeNull();

        // 1. Poor tool is in PoorCondition group and marked flagged
        var poorItem = monitoring!.PoorCondition.FirstOrDefault(i => i.Id == poorTool.Id);
        poorItem.Should().NotBeNull("Poor inspection must appear in PoorCondition group (HW-3, INV-02, T-17)");
        poorItem!.IsFlagged.Should().BeTrue();
        poorItem.Condition.Should().Be("poor");

        // 2. Overdue tool is in OverdueForInspection group and marked flagged
        var overdueItem = monitoring.OverdueForInspection.FirstOrDefault(i => i.Id == overdueTool.Id);
        overdueItem.Should().NotBeNull("Item with past next_check_due must appear in Overdue group (HW-3, INV-02, T-17)");
        overdueItem!.IsFlagged.Should().BeTrue();
        overdueItem.DaysOverdue.Should().BeGreaterThanOrEqualTo(5);

        // 3. Equipment entity condition followed the latest inspection
        using (var verifyScope = _factory.Services.CreateScope())
        {
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
            var updatedPoorTool = await verifyDb.Equipment.FindAsync(poorTool.Id);
            updatedPoorTool!.Condition.Should().Be(EquipmentCondition.Poor, "Equipment.Condition must follow latest inspection (INV-02)");
        }
    }

    [Fact]
    public async Task T17_AdminDashboard_ReflectsFlaggedEquipmentInAlertsAndMetrics()
    {
        // Arrange
        await EnsureSeededAsync();
        var adminClient = await CreateAuthenticatedClientAsync(AdminEmail, AdminPassword);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();

        // Ensure at least one flagged item exists (Status UnderMaintenance)
        var maintTool = new Equipment
        {
            AssetTag = $"EQ-MNT-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            Name = "Hydraulic Maintenance Chair",
            Category = "Furniture",
            Condition = EquipmentCondition.Fair,
            Status = EquipmentStatus.UnderMaintenance,
            Notes = "Pump seal leak"
        };
        db.Equipment.Add(maintTool);
        await db.SaveChangesAsync();

        // Act: GET /api/admin/dashboard
        var dashResp = await adminClient.GetAsync("/api/admin/dashboard");
        dashResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var dashboard = await dashResp.Content.ReadFromJsonAsync<AdminDashboardDto>();

        // Assert: Flagged equipment count > 0 and flagged alerts list includes the maintenance item
        dashboard.Should().NotBeNull();
        dashboard!.FlaggedEquipmentCount.Should().BeGreaterThan(0);
        dashboard.FlaggedEquipmentAlerts.Should().Contain(a => a.Id == maintTool.Id);

        // Also assert the MVC landing page renders 200 OK
        var mvcResp = await adminClient.GetAsync("/admin");
        mvcResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await mvcResp.Content.ReadAsStringAsync();
        html.Should().Contain("Equipment Quality & Maintenance Alert");
    }

    [Fact]
    public async Task AF09_ReturnEquipment_ClosesActiveAssignment()
    {
        // Arrange
        await EnsureSeededAsync();
        var adminClient = await CreateAuthenticatedClientAsync(AdminEmail, AdminPassword);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
        var barber = await db.Barbers.FirstAsync(b => b.DisplayName == "Ramon");

        var eq = new Equipment
        {
            AssetTag = $"EQ-RET-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            Name = "Returnable Trimmer",
            Category = "Trimmers",
            Condition = EquipmentCondition.Good,
            Status = EquipmentStatus.InService
        };
        db.Equipment.Add(eq);
        await db.SaveChangesAsync();

        // Assign to barber
        await adminClient.PostAsJsonAsync($"/api/admin/equipment/{eq.Id}/assign", new AssignEquipmentRequest
        {
            BarberId = barber.Id
        });

        // Act: Return equipment
        var returnResp = await adminClient.PostAsJsonAsync($"/api/admin/equipment/{eq.Id}/return", new ReturnEquipmentRequest
        {
            Notes = "Cleaned and returned to central supply"
        });
        returnResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert: Assignment is closed
        using (var verifyScope = _factory.Services.CreateScope())
        {
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
            var assignment = await verifyDb.EquipmentAssignments
                .FirstOrDefaultAsync(a => a.EquipmentId == eq.Id && a.ReturnedAt == null);
            assignment.Should().BeNull("Equipment must have no active assignment after return");

            var past = await verifyDb.EquipmentAssignments
                .FirstOrDefaultAsync(a => a.EquipmentId == eq.Id);
            past.Should().NotBeNull();
            past!.ReturnedAt.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task AF09_AF13_AdminOnlyGuards_DeniesCustomerAndBarberAccessToAdminEndpoints()
    {
        // Arrange
        await EnsureSeededAsync();
        var customerClient = await CreateAuthenticatedClientAsync(CustomerEmail, DemoPassword);
        var barberClient = await CreateAuthenticatedClientAsync(RamonEmail, DemoPassword);
        var anonClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // 1. Anonymous calls to monitoring API -> 401 Unauthorized
        var anonResp = await anonClient.GetAsync("/api/admin/equipment/monitoring");
        anonResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 2. Customer calls to monitoring API -> 403 Forbidden
        var custApiResp = await customerClient.GetAsync("/api/admin/equipment/monitoring");
        custApiResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 3. Customer calls to assign API -> 403 Forbidden
        var custAssignResp = await customerClient.PostAsJsonAsync("/api/admin/equipment/1/assign", new AssignEquipmentRequest
        {
            BarberId = 1
        });
        custAssignResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 4. Barber calls to monitoring API -> 403 Forbidden
        var barbApiResp = await barberClient.GetAsync("/api/admin/equipment/monitoring");
        barbApiResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 5. Barber calls to assign API -> 403 Forbidden
        var barbAssignResp = await barberClient.PostAsJsonAsync("/api/admin/equipment/1/assign", new AssignEquipmentRequest
        {
            BarberId = 1
        });
        barbAssignResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 6. Barber calls to /admin/equipment/quality UI -> redirect to AccessDenied
        var barbUiResp = await barberClient.GetAsync("/admin/equipment/quality");
        barbUiResp.StatusCode.Should().Be(HttpStatusCode.Redirect);
        barbUiResp.Headers.Location!.ToString().Should().Contain("AccessDenied");
    }
}

