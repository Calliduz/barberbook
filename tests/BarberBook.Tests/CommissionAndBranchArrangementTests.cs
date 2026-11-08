using System.Net;
using System.Net.Http.Json;
using BarberBook.Data;
using BarberBook.Data.Entities;
using BarberBook.Data.Enums;
using BarberBook.Web.Common;
using BarberBook.Web.Models.Admin;
using BarberBook.Web.Models.Appointment;
using BarberBook.Web.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Xunit;

namespace BarberBook.Tests;

public class CommissionAndBranchArrangementTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CommissionAndBranchArrangementTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string email, string password)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password
        });
        loginRes.StatusCode.Should().Be(HttpStatusCode.OK);

        return client;
    }

    [Fact]
    public async Task Availability_SerializesStartsAtUtc_AsValidIsoString()
    {
        var client = _factory.CreateClient();

        // Query availability for Ramon on upcoming Wednesday
        var res = await client.GetAsync("/api/barbers/1/availability?serviceId=1&date=2026-11-18");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await res.Content.ReadAsStringAsync();
        content.Should().Contain("startsAtIso");
        content.Should().Contain("2026-11-18T");
    }

    [Fact]
    public async Task Customer_CannotDoubleBook_WhenBarberHasAnotherCustomerAtThatHour()
    {
        var juanClient = await CreateAuthenticatedClientAsync("juan@barberbook.local", "Demo@BarberBook2026!");
        var markClient = await CreateAuthenticatedClientAsync("mark@barberbook.local", "Demo@BarberBook2026!");

        // Book slot for Juan: 2026-11-18 10:00 AM Manila (02:00:00Z)
        var startsAt = "2026-11-18T02:00:00Z";

        var juanBooking = await juanClient.PostAsJsonAsync("/api/appointments", new
        {
            serviceId = 1,
            barberId = 1,
            startsAt = startsAt,
            notes = "Juan regular cut"
        });

        // Juan booking should succeed or already be booked
        juanBooking.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.Conflict);

        // Mark tries to book Ramon at the exact same hour
        var markBooking = await markClient.PostAsJsonAsync("/api/appointments", new
        {
            serviceId = 1,
            barberId = 1,
            startsAt = startsAt,
            notes = "Mark overlapping attempt"
        });

        markBooking.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var err = await markBooking.Content.ReadFromJsonAsync<ApiErrorEnvelope>();
        err!.Error.Code.Should().Be("SLOT_TAKEN");
    }

    [Fact]
    public async Task Customer_CannotBookTwoBarbers_AtSameHourAndDay()
    {
        var juanClient = await CreateAuthenticatedClientAsync("juan@barberbook.local", "Demo@BarberBook2026!");

        var startsAt = "2026-11-18T03:00:00Z"; // 11:00 AM Manila

        // Juan books Ramon at 11:00 AM
        var first = await juanClient.PostAsJsonAsync("/api/appointments", new
        {
            serviceId = 1,
            barberId = 1,
            startsAt = startsAt,
            notes = "First booking"
        });
        first.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.Conflict);

        // Juan tries to book Dennis (barber 2) at the exact same time
        var second = await juanClient.PostAsJsonAsync("/api/appointments", new
        {
            serviceId = 1,
            barberId = 2,
            startsAt = startsAt,
            notes = "Simultaneous booking attempt with another barber"
        });

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var err = await second.Content.ReadFromJsonAsync<ApiErrorEnvelope>();
        err!.Error.Code.Should().BeOneOf("CUSTOMER_BUSY", "SLOT_TAKEN");
    }

    [Fact]
    public async Task ReportingService_ComputesCustomBarberCommission_AndOwnerShare()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
        var reportingService = scope.ServiceProvider.GetRequiredService<IReportingService>();

        // Set Dennis (barber 2) commission to 65% (0.65m), meaning owner retains 35%
        var dennis = await db.Barbers.FirstAsync(b => b.Id == 2);
        dennis.CommissionRate = 0.65m;
        await db.SaveChangesAsync();

        var report = await reportingService.GetBarberTransactionsAsync(dennis.Id);

        report.CommissionRate.Should().Be(0.65m);
        report.TotalCommission.Should().Be(report.TotalSales * 0.65m);
        report.ShopNetShare.Should().Be(report.TotalSales - report.TotalCommission);
    }

    [Fact]
    public async Task BranchBarberArrangement_AdminCanAssignAndArrangeBarbersForShop()
    {
        var adminClient = await CreateAuthenticatedClientAsync("admin@barberbook.local", "Admin@BarberBook2026!");

        // 1. Get current branch barbers for branch 1
        var getRes = await adminClient.GetAsync("/api/admin/branches/1/barbers");
        getRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 2. Arrange barbers: assign barber 1 and barber 3 to branch 1
        var assignRes = await adminClient.PostAsJsonAsync("/api/admin/branches/1/barbers", new AssignBranchBarbersRequest
        {
            BarberIds = new List<int> { 1, 3 }
        });
        assignRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify in DB that barber 1 and 3 are in branch 1
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
        var b1 = await db.Barbers.FindAsync(1);
        var b3 = await db.Barbers.FindAsync(3);

        b1!.BranchId.Should().Be(1);
        b3!.BranchId.Should().Be(1);
    }

    private class ApiErrorEnvelope
    {
        public ApiErrorDetail Error { get; set; } = new();
    }

    private class ApiErrorDetail
    {
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
