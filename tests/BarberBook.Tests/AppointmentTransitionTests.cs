using BarberBook.Data;
using BarberBook.Data.Entities;
using BarberBook.Data.Enums;
using BarberBook.Data.Seeding;
using BarberBook.Web.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Xunit;

namespace BarberBook.Tests;

public class AppointmentTransitionTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly DateTimeZone _zone = DateTimeZoneProviders.Tzdb["Asia/Manila"];

    private static int _slotCounter = 0;

    public AppointmentTransitionTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private async Task EnsureSeededAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await DatabaseSeeder.SeedAsync(scope.ServiceProvider);
    }

    private async Task<Appointment> CreateTestAppointmentAsync(AppointmentStatus status, int barberId = 1, int customerId = 1)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var now = clock.GetCurrentInstant();

        // Create an isolated appointment with unique slot and reference code to satisfy GiST exclusion constraint
        int slot = Interlocked.Increment(ref _slotCounter);
        var slotDate = new LocalDate(2027, 3, 1).PlusDays(slot / 16);
        var slotTime = new LocalTime(9, 0).PlusMinutes((slot % 16) * 30);
        var startsAt = slotDate.At(slotTime).InZoneStrictly(_zone).ToInstant();
        var endsAt = startsAt.Plus(Duration.FromMinutes(30));

        // Clean up any existing appointments in this slot from prior runs
        var existing = await db.Appointments
            .Where(a => a.BarberId == barberId && a.StartsAt < endsAt && a.EndsAt > startsAt)
            .ToListAsync();
        if (existing.Count > 0)
        {
            db.Appointments.RemoveRange(existing);
            await db.SaveChangesAsync();
        }

        var uniqueRef = $"TRANS-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

        var app = new Appointment
        {
            ReferenceCode = uniqueRef,
            CustomerId = customerId,
            BarberId = barberId,
            ServiceId = 1,
            ServiceName = "Haircut",
            Price = 250.00m,
            StartsAt = startsAt,
            EndsAt = endsAt,
            Status = status,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Appointments.Add(app);
        await db.SaveChangesAsync();

        // Initial status history
        db.StatusHistories.Add(new StatusHistory
        {
            AppointmentId = app.Id,
            FromStatus = null,
            ToStatus = status,
            ChangedById = customerId,
            ChangedByRole = "Customer",
            Note = "Initial test booking",
            ChangedAt = now
        });
        await db.SaveChangesAsync();

        return app;
    }

    public static IEnumerable<object[]> TransitionMatrixData()
    {
        // Format: fromStatus, toStatus, role, isOwn, expectedStatusCode
        // Roles: "Admin", "Barber", "Customer"

        // 1. Transitions from PENDING
        // Pending -> Confirmed
        yield return new object[] { AppointmentStatus.Pending, AppointmentStatus.Confirmed, "Admin", true, 200 };
        yield return new object[] { AppointmentStatus.Pending, AppointmentStatus.Confirmed, "Barber", true, 200 };
        yield return new object[] { AppointmentStatus.Pending, AppointmentStatus.Confirmed, "Barber", false, 403 };
        yield return new object[] { AppointmentStatus.Pending, AppointmentStatus.Confirmed, "Customer", true, 403 };

        // Pending -> Rejected
        yield return new object[] { AppointmentStatus.Pending, AppointmentStatus.Rejected, "Admin", true, 200 };
        yield return new object[] { AppointmentStatus.Pending, AppointmentStatus.Rejected, "Barber", true, 200 };
        yield return new object[] { AppointmentStatus.Pending, AppointmentStatus.Rejected, "Barber", false, 403 };
        yield return new object[] { AppointmentStatus.Pending, AppointmentStatus.Rejected, "Customer", true, 403 };

        // Pending -> Cancelled
        yield return new object[] { AppointmentStatus.Pending, AppointmentStatus.Cancelled, "Admin", true, 200 };
        yield return new object[] { AppointmentStatus.Pending, AppointmentStatus.Cancelled, "Barber", true, 200 };
        yield return new object[] { AppointmentStatus.Pending, AppointmentStatus.Cancelled, "Barber", false, 403 };
        yield return new object[] { AppointmentStatus.Pending, AppointmentStatus.Cancelled, "Customer", true, 200 };
        yield return new object[] { AppointmentStatus.Pending, AppointmentStatus.Cancelled, "Customer", false, 403 };

        // Pending -> Pending (Reschedule)
        yield return new object[] { AppointmentStatus.Pending, AppointmentStatus.Pending, "Customer", true, 200 };
        yield return new object[] { AppointmentStatus.Pending, AppointmentStatus.Pending, "Customer", false, 403 };
        yield return new object[] { AppointmentStatus.Pending, AppointmentStatus.Pending, "Barber", true, 403 };
        yield return new object[] { AppointmentStatus.Pending, AppointmentStatus.Pending, "Admin", true, 403 };

        // Pending -> Completed (Invalid: cannot complete directly from pending)
        yield return new object[] { AppointmentStatus.Pending, AppointmentStatus.Completed, "Admin", true, 409 };
        yield return new object[] { AppointmentStatus.Pending, AppointmentStatus.Completed, "Barber", true, 409 };
        yield return new object[] { AppointmentStatus.Pending, AppointmentStatus.Completed, "Customer", true, 409 };

        // 2. Transitions from CONFIRMED
        // Confirmed -> Completed
        yield return new object[] { AppointmentStatus.Confirmed, AppointmentStatus.Completed, "Admin", true, 200 };
        yield return new object[] { AppointmentStatus.Confirmed, AppointmentStatus.Completed, "Barber", true, 200 };
        yield return new object[] { AppointmentStatus.Confirmed, AppointmentStatus.Completed, "Barber", false, 403 };
        yield return new object[] { AppointmentStatus.Confirmed, AppointmentStatus.Completed, "Customer", true, 403 };

        // Confirmed -> Cancelled
        yield return new object[] { AppointmentStatus.Confirmed, AppointmentStatus.Cancelled, "Admin", true, 200 };
        yield return new object[] { AppointmentStatus.Confirmed, AppointmentStatus.Cancelled, "Barber", true, 200 };
        yield return new object[] { AppointmentStatus.Confirmed, AppointmentStatus.Cancelled, "Barber", false, 403 };
        yield return new object[] { AppointmentStatus.Confirmed, AppointmentStatus.Cancelled, "Customer", true, 200 };
        yield return new object[] { AppointmentStatus.Confirmed, AppointmentStatus.Cancelled, "Customer", false, 403 };

        // Confirmed -> Pending (Reschedule)
        yield return new object[] { AppointmentStatus.Confirmed, AppointmentStatus.Pending, "Customer", true, 200 };
        yield return new object[] { AppointmentStatus.Confirmed, AppointmentStatus.Pending, "Customer", false, 403 };
        yield return new object[] { AppointmentStatus.Confirmed, AppointmentStatus.Pending, "Barber", true, 403 };
        yield return new object[] { AppointmentStatus.Confirmed, AppointmentStatus.Pending, "Admin", true, 403 };

        // Confirmed -> Confirmed (Invalid)
        yield return new object[] { AppointmentStatus.Confirmed, AppointmentStatus.Confirmed, "Admin", true, 409 };
        yield return new object[] { AppointmentStatus.Confirmed, AppointmentStatus.Confirmed, "Barber", true, 409 };

        // Confirmed -> Rejected (Invalid: rejection only applies to pending)
        yield return new object[] { AppointmentStatus.Confirmed, AppointmentStatus.Rejected, "Admin", true, 409 };
        yield return new object[] { AppointmentStatus.Confirmed, AppointmentStatus.Rejected, "Barber", true, 409 };

        // Confirmed -> NoShow (Admin and assigned Barber can mark No-Show)
        yield return new object[] { AppointmentStatus.Confirmed, AppointmentStatus.NoShow, "Admin", true, 200 };
        yield return new object[] { AppointmentStatus.Confirmed, AppointmentStatus.NoShow, "Barber", true, 200 };
        yield return new object[] { AppointmentStatus.Confirmed, AppointmentStatus.NoShow, "Barber", false, 403 };
        yield return new object[] { AppointmentStatus.Confirmed, AppointmentStatus.NoShow, "Customer", true, 403 };

        // Pending -> NoShow (Invalid: only confirmed appointments can be no-show)
        yield return new object[] { AppointmentStatus.Pending, AppointmentStatus.NoShow, "Admin", true, 409 };
        yield return new object[] { AppointmentStatus.Pending, AppointmentStatus.NoShow, "Barber", true, 409 };
        yield return new object[] { AppointmentStatus.Pending, AppointmentStatus.NoShow, "Customer", true, 409 };
    }

    [Theory]
    [MemberData(nameof(TransitionMatrixData))]
    public async Task Section9_TransitionMatrix_EnforcesStatusAndActorRules(
        AppointmentStatus fromStatus,
        AppointmentStatus toStatus,
        string role,
        bool isOwn,
        int expectedStatusCode)
    {
        await EnsureSeededAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
        var transitionService = scope.ServiceProvider.GetRequiredService<IAppointmentTransitionService>();

        // Ramon (Barber 1, UserId 2) and Dennis (Barber 2, UserId 3)
        // Juan (Customer 1, UserId 5) and Mark (Customer 2, UserId 6)
        // Admin (UserId 1)
        var ramon = await db.Barbers.Include(b => b.User).FirstAsync(b => b.DisplayName == "Ramon");
        var dennis = await db.Barbers.Include(b => b.User).FirstAsync(b => b.DisplayName == "Dennis");
        var juan = await db.Users.FirstAsync(u => u.Email == "juan@barberbook.local");
        var mark = await db.Users.FirstAsync(u => u.Email == "mark@barberbook.local");
        var admin = await db.Users.FirstAsync(u => u.Email == "admin@barberbook.local");

        // Create appointment assigned to Ramon and Juan
        var app = await CreateTestAppointmentAsync(fromStatus, barberId: ramon.Id, customerId: juan.Id);

        // Determine actor based on test parameters
        TransitionActor actor;
        if (role == "Admin")
        {
            actor = new TransitionActor(admin.Id, "Admin");
        }
        else if (role == "Barber")
        {
            var barberUser = isOwn ? ramon.User : dennis.User;
            actor = new TransitionActor(barberUser.Id, "Barber", BarberId: isOwn ? ramon.Id : dennis.Id);
        }
        else // Customer
        {
            var customerUser = isOwn ? juan : mark;
            actor = new TransitionActor(customerUser.Id, "Customer", CustomerId: customerUser.Id);
        }

        var note = $"Matrix test {fromStatus}->{toStatus} by {role} (isOwn={isOwn})";

        // Act
        var result = await transitionService.TransitionAsync(app.Id, toStatus, actor, note);

        // Assert
        result.StatusCode.Should().Be(expectedStatusCode);

        if (expectedStatusCode == 200)
        {
            result.Success.Should().BeTrue();
            result.Appointment!.Status.Should().Be(toStatus);

            // Verify status_history row was appended in the database
            var historyRecord = await db.StatusHistories
                .OrderByDescending(h => h.Id)
                .FirstOrDefaultAsync(h => h.AppointmentId == app.Id && h.ToStatus == toStatus);

            historyRecord.Should().NotBeNull();
            historyRecord!.FromStatus.Should().Be(fromStatus);
            historyRecord.ToStatus.Should().Be(toStatus);
            historyRecord.ChangedById.Should().Be(actor.UserId);
            historyRecord.ChangedByRole.Should().Be(role);
            historyRecord.Note.Should().Be(note);
        }
        else
        {
            result.Success.Should().BeFalse();
            // Verify appointment status did not change in DB
            var dbApp = await db.Appointments.AsNoTracking().FirstAsync(a => a.Id == app.Id);
            dbApp.Status.Should().Be(fromStatus);
        }
    }

    [Theory]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.Rejected)]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.NoShow)]
    public async Task Section9_TerminalStatusImmutability_CannotTransitionFromTerminalStatuses(AppointmentStatus terminalStatus)
    {
        // Terminal statuses: completed, rejected, cancelled, noshow. No role can move them.
        await EnsureSeededAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BarberBookDbContext>();
        var transitionService = scope.ServiceProvider.GetRequiredService<IAppointmentTransitionService>();
        var admin = await db.Users.FirstAsync(u => u.Email == "admin@barberbook.local");

        var app = await CreateTestAppointmentAsync(terminalStatus);

        var allStatuses = new[]
        {
            AppointmentStatus.Pending,
            AppointmentStatus.Confirmed,
            AppointmentStatus.Completed,
            AppointmentStatus.Cancelled,
            AppointmentStatus.Rejected,
            AppointmentStatus.NoShow
        };

        var actor = new TransitionActor(admin.Id, "Admin");

        foreach (var target in allStatuses)
        {
            var result = await transitionService.TransitionAsync(app.Id, target, actor, "Attempt terminal change");
            result.StatusCode.Should().Be(409, $"Transitioning from terminal status '{terminalStatus}' to '{target}' must be rejected with 409");
            result.ErrorCode.Should().Be("INVALID_TRANSITION");
        }

        // Verify status remains unchanged
        var dbApp = await db.Appointments.AsNoTracking().FirstAsync(a => a.Id == app.Id);
        dbApp.Status.Should().Be(terminalStatus);
    }
}
