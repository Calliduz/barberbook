using NodaTime;

namespace BarberBook.Web.Services;

public class AvailableSlotDto
{
    public LocalTime Time { get; set; }
    public string FormattedTime { get; set; } = string.Empty;
    public Instant StartsAtUtc { get; set; }
    public string StartsAtIso => StartsAtUtc.ToString();
}

public class NextAvailableSlotDto
{
    public LocalDate Date { get; set; }
    public LocalTime Time { get; set; }
    public string FormattedTime { get; set; } = string.Empty;
    public string DisplayText { get; set; } = string.Empty;
    public Instant StartsAtUtc { get; set; }
}

public interface IAvailabilityService
{
    /// <summary>
    /// Computes live availability for a barber, service, and local date in Asia/Manila on a 30-minute grid.
    /// Returns the start times (LocalTime in Asia/Manila).
    /// </summary>
    Task<List<LocalTime>> GetAvailableSlotsAsync(int barberId, int serviceId, LocalDate date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes live availability and returns rich slot details including 12-hour formatting and UTC Instant.
    /// </summary>
    Task<List<AvailableSlotDto>> GetAvailableSlotDetailsAsync(int barberId, int serviceId, LocalDate date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes the earliest available slot for a barber across upcoming operating days (within 7 days).
    /// Used on barber directory cards and home page showcase (CF-3, BR-04).
    /// </summary>
    Task<NextAvailableSlotDto?> GetNextAvailableSlotAsync(int barberId, int? serviceId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies if a specific slot starting at startsAt (UTC Instant) is available for the given barber and service.
    /// Used by booking and rescheduling validations (BR-03).
    /// </summary>
    Task<bool> IsSlotAvailableAsync(int barberId, int serviceId, Instant startsAt, int? ignoreAppointmentId = null, CancellationToken cancellationToken = default);
}

