using BarberBook.Data;
using BarberBook.Data.Entities;
using BarberBook.Data.Enums;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace BarberBook.Web.Services;

public class AppointmentTransitionService : IAppointmentTransitionService
{
    private readonly BarberBookDbContext _dbContext;
    private readonly IClock _clock;

    public AppointmentTransitionService(BarberBookDbContext dbContext, IClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public async Task<AppointmentTransitionResult> TransitionAsync(
        int appointmentId,
        AppointmentStatus toStatus,
        TransitionActor actor,
        string? note = null,
        Action<Appointment>? updateAction = null,
        CancellationToken cancellationToken = default)
    {
        var appointment = await _dbContext.Appointments
            .Include(a => a.Barber).ThenInclude(b => b.User)
            .Include(a => a.Customer)
            .Include(a => a.Service)
            .FirstOrDefaultAsync(a => a.Id == appointmentId, cancellationToken);

        if (appointment == null)
        {
            return AppointmentTransitionResult.NotFound();
        }

        var fromStatus = appointment.Status;

        // 1. Terminal status check (§9: completed, rejected, cancelled, noshow are terminal)
        if (fromStatus == AppointmentStatus.Completed ||
            fromStatus == AppointmentStatus.Rejected ||
            fromStatus == AppointmentStatus.Cancelled ||
            fromStatus == AppointmentStatus.NoShow)
        {
            return AppointmentTransitionResult.InvalidTransition(
                $"Cannot transition appointment from terminal status '{fromStatus.ToString().ToLowerInvariant()}'.");
        }

        // 2. State machine validity check (§9 State Diagram & Transition Table)
        bool isValidTransitionStructure = false;

        if (fromStatus == AppointmentStatus.Pending)
        {
            if (toStatus == AppointmentStatus.Confirmed ||
                toStatus == AppointmentStatus.Rejected ||
                toStatus == AppointmentStatus.Cancelled ||
                toStatus == AppointmentStatus.Pending) // Reschedule
            {
                isValidTransitionStructure = true;
            }
        }
        else if (fromStatus == AppointmentStatus.Confirmed)
        {
            if (toStatus == AppointmentStatus.Completed ||
                toStatus == AppointmentStatus.Cancelled ||
                toStatus == AppointmentStatus.Pending || // Reschedule
                toStatus == AppointmentStatus.NoShow) // Client failed to arrive
            {
                isValidTransitionStructure = true;
            }
        }

        if (!isValidTransitionStructure)
        {
            return AppointmentTransitionResult.InvalidTransition(
                $"Invalid status transition from '{fromStatus.ToString().ToLowerInvariant()}' to '{toStatus.ToString().ToLowerInvariant()}'.");
        }

        // 3. Role and ownership authorization check (§9 Transition Table)
        bool isActorAuthorized = false;

        if (string.Equals(actor.Role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            // Admin can confirm, reject, complete, cancel, or mark no-show any appointment.
            // Admin cannot reschedule a customer's appointment (customer-only).
            if (toStatus == AppointmentStatus.Confirmed ||
                toStatus == AppointmentStatus.Rejected ||
                toStatus == AppointmentStatus.Completed ||
                toStatus == AppointmentStatus.Cancelled ||
                toStatus == AppointmentStatus.NoShow)
            {
                isActorAuthorized = true;
            }
        }
        else if (string.Equals(actor.Role, "Barber", StringComparison.OrdinalIgnoreCase))
        {
            // Barber can only transition own appointments
            bool isOwnAppointment = appointment.Barber.UserId == actor.UserId;
            if (!isOwnAppointment)
            {
                return AppointmentTransitionResult.Forbidden(
                    "Barbers can only manage appointments assigned to them.");
            }

            // Barber can confirm, reject, complete, cancel, or mark no-show own appointments.
            if (toStatus == AppointmentStatus.Confirmed ||
                toStatus == AppointmentStatus.Rejected ||
                toStatus == AppointmentStatus.Completed ||
                toStatus == AppointmentStatus.Cancelled ||
                toStatus == AppointmentStatus.NoShow)
            {
                isActorAuthorized = true;
            }
        }
        else if (string.Equals(actor.Role, "Customer", StringComparison.OrdinalIgnoreCase))
        {
            // Customer can only transition own appointments
            bool isOwnAppointment = appointment.CustomerId == actor.UserId;
            if (!isOwnAppointment)
            {
                return AppointmentTransitionResult.Forbidden(
                    "Customers can only manage their own appointments.");
            }

            // Customer can cancel or reschedule (to Pending) own appointments.
            if (toStatus == AppointmentStatus.Cancelled ||
                toStatus == AppointmentStatus.Pending)
            {
                isActorAuthorized = true;
            }
        }

        if (!isActorAuthorized)
        {
            return AppointmentTransitionResult.Forbidden(
                $"Role '{actor.Role}' is not authorized to transition appointment to '{toStatus.ToString().ToLowerInvariant()}'.");
        }

        // 4. Apply transition
        var now = _clock.GetCurrentInstant();
        appointment.Status = toStatus;
        appointment.UpdatedAt = now;

        updateAction?.Invoke(appointment);

        // 5. Append status history record
        var statusHistory = new StatusHistory
        {
            AppointmentId = appointment.Id,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            ChangedById = actor.UserId,
            ChangedByRole = actor.Role,
            Note = string.IsNullOrWhiteSpace(note)
                ? $"Status changed from {fromStatus.ToString().ToLowerInvariant()} to {toStatus.ToString().ToLowerInvariant()}"
                : note.Trim(),
            ChangedAt = now
        };

        _dbContext.StatusHistories.Add(statusHistory);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return AppointmentTransitionResult.Ok(appointment);
    }
}

