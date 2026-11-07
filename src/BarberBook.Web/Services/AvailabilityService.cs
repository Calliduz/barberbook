using BarberBook.Data;
using BarberBook.Data.Enums;
using BarberBook.Web.Common;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace BarberBook.Web.Services;

public class AvailabilityService : IAvailabilityService
{
    private readonly BarberBookDbContext _dbContext;
    private readonly IClock _clock;
    private readonly DateTimeZone _zone;

    public AvailabilityService(BarberBookDbContext dbContext, IClock clock, DateTimeZone? zone = null)
    {
        _dbContext = dbContext;
        _clock = clock;
        _zone = zone ?? DateTimeZoneProviders.Tzdb["Asia/Manila"];
    }

    public async Task<List<LocalTime>> GetAvailableSlotsAsync(int barberId, int serviceId, LocalDate date, CancellationToken cancellationToken = default)
    {
        var details = await GetAvailableSlotDetailsAsync(barberId, serviceId, date, cancellationToken);
        return details.Select(d => d.Time).ToList();
    }

    public async Task<List<AvailableSlotDto>> GetAvailableSlotDetailsAsync(int barberId, int serviceId, LocalDate date, CancellationToken cancellationToken = default)
    {
        // 1. Check if barber exists and is active
        var barber = await _dbContext.Barbers
            .Include(b => b.User)
            .FirstOrDefaultAsync(b => b.Id == barberId, cancellationToken);

        if (barber == null || !barber.IsActive || !barber.User.IsActive)
        {
            return new List<AvailableSlotDto>();
        }

        // 2. Check if service exists and is active
        var service = await _dbContext.Services
            .FirstOrDefaultAsync(s => s.Id == serviceId, cancellationToken);

        if (service == null || !service.IsActive)
        {
            return new List<AvailableSlotDto>();
        }

        // 3. Check if date is marked as a day off (BR-03)
        var isDayOff = await _dbContext.DaysOff
            .AnyAsync(d => d.BarberId == barberId && d.Date == date, cancellationToken);

        if (isDayOff)
        {
            return new List<AvailableSlotDto>();
        }

        // 4. Query working hours for this weekday (0 = Sunday, 1 = Monday, ..., 6 = Saturday)
        int dow = date.DayOfWeek == IsoDayOfWeek.Sunday ? 0 : (int)date.DayOfWeek;
        var workingRanges = await _dbContext.WorkingHours
            .Where(w => w.BarberId == barberId && w.DayOfWeek == dow)
            .OrderBy(w => w.StartTime)
            .ToListAsync(cancellationToken);

        if (!workingRanges.Any())
        {
            return new List<AvailableSlotDto>();
        }

        // 5. Query active appointments for this barber on that date
        var dayStartUtc = date.AtStartOfDayInZone(_zone).ToInstant();
        var dayEndUtc = date.PlusDays(1).AtStartOfDayInZone(_zone).ToInstant();

        var appointments = await _dbContext.Appointments
            .Where(a => a.BarberId == barberId &&
                        a.Status != AppointmentStatus.Cancelled &&
                        a.Status != AppointmentStatus.Rejected &&
                        a.Status != AppointmentStatus.NoShow &&
                        a.EndsAt > dayStartUtc &&
                        a.StartsAt < dayEndUtc)
            .ToListAsync(cancellationToken);

        var now = _clock.GetCurrentInstant();
        var availableSlots = new List<AvailableSlotDto>();

        // 6. Generate candidate slots on a 30-minute grid
        foreach (var range in workingRanges)
        {
            int rangeStartMinutes = range.StartTime.Hour * 60 + range.StartTime.Minute;
            // Handle edge case of midnight closing
            int rangeEndMinutes = (range.EndTime.Hour == 0 && range.EndTime.Minute == 0 && rangeStartMinutes > 0)
                ? 24 * 60
                : range.EndTime.Hour * 60 + range.EndTime.Minute;

            // Align start time to 30-minute grid
            int remainder = rangeStartMinutes % 30;
            int firstSlotMinutes = remainder == 0 ? rangeStartMinutes : rangeStartMinutes + (30 - remainder);

            for (int slotStartMinutes = firstSlotMinutes; slotStartMinutes < rangeEndMinutes; slotStartMinutes += 30)
            {
                int slotEndMinutes = slotStartMinutes + service.DurationMinutes;

                // Rule BR-03: The whole service duration must fit inside one working-hours range
                if (slotEndMinutes > rangeEndMinutes)
                {
                    continue;
                }

                int slotHour = slotStartMinutes / 60;
                int slotMin = slotStartMinutes % 60;
                var slotLocalTime = new LocalTime(slotHour, slotMin);

                // Convert candidate start and end to UTC Instant
                var slotStartUtc = date.At(slotLocalTime).InZoneStrictly(_zone).ToInstant();
                var slotEndUtc = slotStartUtc.Plus(Duration.FromMinutes(service.DurationMinutes));

                // Rule BR-03: Slot must be in the future
                if (slotStartUtc <= now)
                {
                    continue;
                }

                // Rule BR-03: Slot must not overlap any pending, confirmed, or completed appointment
                bool overlaps = appointments.Any(a => slotStartUtc < a.EndsAt && slotEndUtc > a.StartsAt);
                if (overlaps)
                {
                    continue;
                }

                availableSlots.Add(new AvailableSlotDto
                {
                    Time = slotLocalTime,
                    FormattedTime = FormattingHelpers.FormatTime12h(slotStartUtc),
                    StartsAtUtc = slotStartUtc
                });
            }
        }

        return availableSlots;
    }

    public async Task<bool> IsSlotAvailableAsync(int barberId, int serviceId, Instant startsAt, int? ignoreAppointmentId = null, CancellationToken cancellationToken = default)
    {
        var now = _clock.GetCurrentInstant();

        // Must be in the future
        if (startsAt <= now)
        {
            return false;
        }

        var startsAtManila = startsAt.InZone(_zone);
        var date = startsAtManila.Date;
        var time = startsAtManila.TimeOfDay;

        // Must be on a 30-minute grid
        if (time.Minute % 30 != 0 || time.Second != 0 || time.Millisecond != 0)
        {
            return false;
        }

        // Check barber
        var barber = await _dbContext.Barbers
            .Include(b => b.User)
            .FirstOrDefaultAsync(b => b.Id == barberId, cancellationToken);

        if (barber == null || !barber.IsActive || !barber.User.IsActive)
        {
            return false;
        }

        // Check service
        var service = await _dbContext.Services
            .FirstOrDefaultAsync(s => s.Id == serviceId, cancellationToken);

        if (service == null || !service.IsActive)
        {
            return false;
        }

        // Check day off
        var isDayOff = await _dbContext.DaysOff
            .AnyAsync(d => d.BarberId == barberId && d.Date == date, cancellationToken);

        if (isDayOff)
        {
            return false;
        }

        // Check working hours
        int dow = date.DayOfWeek == IsoDayOfWeek.Sunday ? 0 : (int)date.DayOfWeek;
        var workingRanges = await _dbContext.WorkingHours
            .Where(w => w.BarberId == barberId && w.DayOfWeek == dow)
            .ToListAsync(cancellationToken);

        int startMinutes = time.Hour * 60 + time.Minute;
        int endMinutes = startMinutes + service.DurationMinutes;

        bool fitsInsideRange = workingRanges.Any(range =>
        {
            int rStart = range.StartTime.Hour * 60 + range.StartTime.Minute;
            int rEnd = (range.EndTime.Hour == 0 && range.EndTime.Minute == 0 && rStart > 0)
                ? 24 * 60
                : range.EndTime.Hour * 60 + range.EndTime.Minute;

            return startMinutes >= rStart && endMinutes <= rEnd;
        });

        if (!fitsInsideRange)
        {
            return false;
        }

        // Check appointment overlap
        var endsAtUtc = startsAt.Plus(Duration.FromMinutes(service.DurationMinutes));

        var hasOverlap = await _dbContext.Appointments
            .AnyAsync(a => a.BarberId == barberId &&
                           a.Status != AppointmentStatus.Cancelled &&
                           a.Status != AppointmentStatus.Rejected &&
                           a.Status != AppointmentStatus.NoShow &&
                           (!ignoreAppointmentId.HasValue || a.Id != ignoreAppointmentId.Value) &&
                           startsAt < a.EndsAt &&
                           endsAtUtc > a.StartsAt,
                      cancellationToken);

        return !hasOverlap;
    }
}

