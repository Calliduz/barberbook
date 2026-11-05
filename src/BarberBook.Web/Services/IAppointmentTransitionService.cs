using BarberBook.Data.Entities;
using BarberBook.Data.Enums;

namespace BarberBook.Web.Services;

public record TransitionActor(int UserId, string Role, int? CustomerId = null, int? BarberId = null);

public class AppointmentTransitionResult
{
    public bool Success { get; init; }
    public Appointment? Appointment { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public int StatusCode { get; init; }

    public static AppointmentTransitionResult Ok(Appointment app) =>
        new() { Success = true, Appointment = app, StatusCode = 200 };

    public static AppointmentTransitionResult NotFound(string message = "Appointment not found.") =>
        new() { Success = false, ErrorCode = "NOT_FOUND", ErrorMessage = message, StatusCode = 404 };

    public static AppointmentTransitionResult Forbidden(string message = "You do not have permission to perform this status transition.") =>
        new() { Success = false, ErrorCode = "FORBIDDEN", ErrorMessage = message, StatusCode = 403 };

    public static AppointmentTransitionResult InvalidTransition(string message = "Invalid status transition.") =>
        new() { Success = false, ErrorCode = "INVALID_TRANSITION", ErrorMessage = message, StatusCode = 409 };
}

public interface IAppointmentTransitionService
{
    Task<AppointmentTransitionResult> TransitionAsync(
        int appointmentId,
        AppointmentStatus toStatus,
        TransitionActor actor,
        string? note = null,
        Action<Appointment>? updateAction = null,
        CancellationToken cancellationToken = default);
}

