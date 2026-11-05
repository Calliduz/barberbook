using System.Data;
using System.Globalization;
using System.Security.Claims;
using BarberBook.Data;
using BarberBook.Data.Entities;
using BarberBook.Data.Enums;
using BarberBook.Web.Common;
using BarberBook.Web.Models.Appointment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using NodaTime.Text;

namespace BarberBook.Web.Controllers;

[ApiController]
[Route("api/appointments")]
public class AppointmentsApiController : ControllerBase
{
    private readonly BarberBookDbContext _dbContext;
    private readonly IClock _clock;
    private readonly DateTimeZone _zone;

    public AppointmentsApiController(
        BarberBookDbContext dbContext,
        IClock clock,
        DateTimeZone? zone = null)
    {
        _dbContext = dbContext;
        _clock = clock;
        _zone = zone ?? DateTimeZoneProviders.Tzdb["Asia/Manila"];
    }

    [Authorize(Roles = "Customer")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAppointmentRequest request, CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var customerId))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.StartsAt))
        {
            return BadRequest(new ApiErrorResponse("VALIDATION_ERROR", "A valid startsAt date string is required."));
        }

        // Parse StartsAt (ISO-8601 string)
        Instant startsAtInstant;
        if (InstantPattern.ExtendedIso.Parse(request.StartsAt).TryGetValue(Instant.MinValue, out var parsedInstant))
        {
            startsAtInstant = parsedInstant;
        }
        else if (DateTimeOffset.TryParse(request.StartsAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
        {
            startsAtInstant = Instant.FromDateTimeOffset(dto);
        }
        else
        {
            return BadRequest(new ApiErrorResponse("INVALID_DATE_FORMAT", "StartsAt must be a valid ISO-8601 date string."));
        }

        var now = _clock.GetCurrentInstant();

        // 1. Validate startsAt is in the future (BR-03)
        if (startsAtInstant <= now)
        {
            return BadRequest(new ApiErrorResponse("PAST_TIME", "Appointments cannot be booked in the past."));
        }

        // 2. Validate startsAt is on a 30-minute grid in Asia/Manila (BR-05)
        var startsAtManila = startsAtInstant.InZone(_zone);
        var timeOfDay = startsAtManila.TimeOfDay;
        if (timeOfDay.Minute % 30 != 0 || timeOfDay.Second != 0 || timeOfDay.Millisecond != 0)
        {
            return BadRequest(new ApiErrorResponse("OFF_GRID_TIME", "Appointment start time must align to a 30-minute grid."));
        }

        // 3. Validate customer account is active
        var customerUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == customerId, cancellationToken);
        if (customerUser == null || !customerUser.IsActive)
        {
            return BadRequest(new ApiErrorResponse("INACTIVE_USER", "Your account is not active."));
        }

        // 4. Validate service exists and is active
        var service = await _dbContext.Services.FirstOrDefaultAsync(s => s.Id == request.ServiceId, cancellationToken);
        if (service == null || !service.IsActive)
        {
            return BadRequest(new ApiErrorResponse("INACTIVE_SERVICE", "The selected service is not active or does not exist."));
        }

        // 5. Begin transaction with row-level lock on the barber
        await using var tx = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        try
        {
            // Acquire row-level exclusive lock on the barber row
            await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT id FROM barbers WHERE id = {request.BarberId} FOR UPDATE", cancellationToken);

            var barber = await _dbContext.Barbers
                .Include(b => b.User)
                .FirstOrDefaultAsync(b => b.Id == request.BarberId, cancellationToken);

            if (barber == null || !barber.IsActive || !barber.User.IsActive)
            {
                await tx.RollbackAsync(cancellationToken);
                return BadRequest(new ApiErrorResponse("INACTIVE_BARBER", "The selected barber is not active or does not exist."));
            }

            var appointmentDate = startsAtManila.Date;

            // 6. Check if appointment date is a day off (BR-03)
            var isDayOff = await _dbContext.DaysOff.AnyAsync(
                d => d.BarberId == request.BarberId && d.Date == appointmentDate, cancellationToken);
            if (isDayOff)
            {
                await tx.RollbackAsync(cancellationToken);
                return BadRequest(new ApiErrorResponse("DAY_OFF", "The selected barber is not available on this date (day off)."));
            }

            // 7. Check if whole appointment duration fits inside working hours (BR-03)
            int dow = appointmentDate.DayOfWeek == IsoDayOfWeek.Sunday ? 0 : (int)appointmentDate.DayOfWeek;
            var workingRanges = await _dbContext.WorkingHours
                .Where(w => w.BarberId == request.BarberId && w.DayOfWeek == dow)
                .ToListAsync(cancellationToken);

            int startMin = timeOfDay.Hour * 60 + timeOfDay.Minute;
            int endMin = startMin + service.DurationMinutes;

            bool fitsInsideRange = workingRanges.Any(range =>
            {
                int rStart = range.StartTime.Hour * 60 + range.StartTime.Minute;
                int rEnd = (range.EndTime.Hour == 0 && range.EndTime.Minute == 0 && rStart > 0)
                    ? 24 * 60
                    : range.EndTime.Hour * 60 + range.EndTime.Minute;

                return startMin >= rStart && endMin <= rEnd;
            });

            if (!fitsInsideRange)
            {
                await tx.RollbackAsync(cancellationToken);
                return BadRequest(new ApiErrorResponse("OUTSIDE_WORKING_HOURS", "The requested appointment duration falls outside the barber's working hours."));
            }

            // 8. Re-run slot availability against existing bookings
            var endsAt = startsAtInstant.Plus(Duration.FromMinutes(service.DurationMinutes));

            var slotTaken = await _dbContext.Appointments.AnyAsync(
                a => a.BarberId == request.BarberId &&
                     a.Status != AppointmentStatus.Cancelled &&
                     a.Status != AppointmentStatus.Rejected &&
                     startsAtInstant < a.EndsAt &&
                     endsAt > a.StartsAt,
                cancellationToken);

            if (slotTaken)
            {
                await tx.RollbackAsync(cancellationToken);
                return Conflict(new ApiErrorResponse("SLOT_TAKEN", "The selected time slot is already booked. Please choose another time."));
            }

            // 9. Generate unique reference code: BB-YYYYMMDD-####
            var datePrefix = startsAtManila.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            var prefix = $"BB-{datePrefix}-";
            var countToday = await _dbContext.Appointments
                .CountAsync(a => a.ReferenceCode.StartsWith(prefix), cancellationToken);

            int seq = countToday + 1;
            string refCode;
            do
            {
                refCode = $"{prefix}{seq:D4}";
                seq++;
            } while (await _dbContext.Appointments.AnyAsync(a => a.ReferenceCode == refCode, cancellationToken));

            // 10. Create appointment with price and service_name snapshots (BR-08)
            var appointment = new Appointment
            {
                ReferenceCode = refCode,
                CustomerId = customerId,
                BarberId = request.BarberId,
                ServiceId = request.ServiceId,
                StartsAt = startsAtInstant,
                EndsAt = endsAt,
                Status = AppointmentStatus.Pending,
                Price = service.Price, // Snapshot
                ServiceName = service.Name, // Snapshot
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                CreatedAt = now,
                UpdatedAt = now
            };

            _dbContext.Appointments.Add(appointment);
            await _dbContext.SaveChangesAsync(cancellationToken);

            // 11. Write initial status history row
            var statusHistory = new StatusHistory
            {
                AppointmentId = appointment.Id,
                FromStatus = null,
                ToStatus = AppointmentStatus.Pending,
                ChangedById = customerId,
                ChangedByRole = "Customer",
                Note = "Appointment booked online",
                ChangedAt = now
            };

            _dbContext.StatusHistories.Add(statusHistory);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await tx.CommitAsync(cancellationToken);

            var endsAtManila = endsAt.InZone(_zone);
            var responseDto = new AppointmentDetailDto
            {
                Id = appointment.Id,
                ReferenceCode = appointment.ReferenceCode,
                CustomerId = customerId,
                CustomerName = customerUser.FullName,
                CustomerPhone = customerUser.PhoneNumber ?? string.Empty,
                CustomerEmail = customerUser.Email ?? string.Empty,
                BarberId = barber.Id,
                BarberName = barber.DisplayName,
                ServiceId = service.Id,
                ServiceName = appointment.ServiceName,
                Price = appointment.Price,
                FormattedPrice = FormattingHelpers.FormatPeso(appointment.Price),
                StartsAt = appointment.StartsAt.ToString(),
                EndsAt = appointment.EndsAt.ToString(),
                DurationMinutes = service.DurationMinutes,
                FormattedDate = FormattingHelpers.FormatDate(appointment.StartsAt),
                FormattedTime = $"{FormattingHelpers.FormatTime12h(startsAtManila.TimeOfDay)} – {FormattingHelpers.FormatTime12h(endsAtManila.TimeOfDay)}",
                Status = appointment.Status.ToString().ToLowerInvariant(),
                StatusDisplay = GetStatusDisplay(appointment.Status),
                Notes = appointment.Notes,
                CreatedAt = appointment.CreatedAt.ToString()
            };

            return CreatedAtAction(nameof(GetById), new { id = appointment.Id }, responseDto);
        }
        catch (DbUpdateException ex) when (IsExclusionViolation(ex))
        {
            await tx.RollbackAsync(cancellationToken);
            return Conflict(new ApiErrorResponse("SLOT_TAKEN", "The selected time slot is already booked. Please choose another time."));
        }
    }

    [Authorize]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var app = await _dbContext.Appointments
            .Include(a => a.Customer)
            .Include(a => a.Barber).ThenInclude(b => b.User)
            .Include(a => a.Service)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (app == null)
        {
            return NotFound(new ApiErrorResponse("NOT_FOUND", "Appointment not found."));
        }

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var currentUserId))
        {
            return Unauthorized();
        }

        bool isAdmin = User.IsInRole("Admin");
        bool isAssignedBarber = User.IsInRole("Barber") && app.Barber.UserId == currentUserId;
        bool isOwnerCustomer = User.IsInRole("Customer") && app.CustomerId == currentUserId;

        if (!isAdmin && !isAssignedBarber && !isOwnerCustomer)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ApiErrorResponse("FORBIDDEN", "You are not authorized to view this appointment."));
        }

        var startsAtManila = app.StartsAt.InZone(_zone);
        var endsAtManila = app.EndsAt.InZone(_zone);

        var dto = new AppointmentDetailDto
        {
            Id = app.Id,
            ReferenceCode = app.ReferenceCode,
            CustomerId = app.CustomerId,
            CustomerName = app.Customer.FullName,
            CustomerPhone = app.Customer.PhoneNumber ?? string.Empty,
            CustomerEmail = app.Customer.Email ?? string.Empty,
            BarberId = app.BarberId,
            BarberName = app.Barber.DisplayName,
            ServiceId = app.ServiceId,
            ServiceName = app.ServiceName,
            Price = app.Price,
            FormattedPrice = FormattingHelpers.FormatPeso(app.Price),
            StartsAt = app.StartsAt.ToString(),
            EndsAt = app.EndsAt.ToString(),
            DurationMinutes = (int)(app.EndsAt - app.StartsAt).TotalMinutes,
            FormattedDate = FormattingHelpers.FormatDate(app.StartsAt),
            FormattedTime = $"{FormattingHelpers.FormatTime12h(startsAtManila.TimeOfDay)} – {FormattingHelpers.FormatTime12h(endsAtManila.TimeOfDay)}",
            Status = app.Status.ToString().ToLowerInvariant(),
            StatusDisplay = GetStatusDisplay(app.Status),
            Notes = app.Notes,
            CreatedAt = app.CreatedAt.ToString()
        };

        return Ok(dto);
    }

    [Authorize]
    [HttpGet("by-ref/{referenceCode}")]
    public async Task<IActionResult> GetByReferenceCode(string referenceCode, CancellationToken cancellationToken)
    {
        var app = await _dbContext.Appointments
            .Include(a => a.Customer)
            .Include(a => a.Barber).ThenInclude(b => b.User)
            .Include(a => a.Service)
            .FirstOrDefaultAsync(a => a.ReferenceCode == referenceCode, cancellationToken);

        if (app == null)
        {
            return NotFound(new ApiErrorResponse("NOT_FOUND", "Appointment not found."));
        }

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var currentUserId))
        {
            return Unauthorized();
        }

        bool isAdmin = User.IsInRole("Admin");
        bool isAssignedBarber = User.IsInRole("Barber") && app.Barber.UserId == currentUserId;
        bool isOwnerCustomer = User.IsInRole("Customer") && app.CustomerId == currentUserId;

        if (!isAdmin && !isAssignedBarber && !isOwnerCustomer)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ApiErrorResponse("FORBIDDEN", "You are not authorized to view this appointment."));
        }

        var startsAtManila = app.StartsAt.InZone(_zone);
        var endsAtManila = app.EndsAt.InZone(_zone);

        var dto = new AppointmentDetailDto
        {
            Id = app.Id,
            ReferenceCode = app.ReferenceCode,
            CustomerId = app.CustomerId,
            CustomerName = app.Customer.FullName,
            CustomerPhone = app.Customer.PhoneNumber ?? string.Empty,
            CustomerEmail = app.Customer.Email ?? string.Empty,
            BarberId = app.BarberId,
            BarberName = app.Barber.DisplayName,
            ServiceId = app.ServiceId,
            ServiceName = app.ServiceName,
            Price = app.Price,
            FormattedPrice = FormattingHelpers.FormatPeso(app.Price),
            StartsAt = app.StartsAt.ToString(),
            EndsAt = app.EndsAt.ToString(),
            DurationMinutes = (int)(app.EndsAt - app.StartsAt).TotalMinutes,
            FormattedDate = FormattingHelpers.FormatDate(app.StartsAt),
            FormattedTime = $"{FormattingHelpers.FormatTime12h(startsAtManila.TimeOfDay)} – {FormattingHelpers.FormatTime12h(endsAtManila.TimeOfDay)}",
            Status = app.Status.ToString().ToLowerInvariant(),
            StatusDisplay = GetStatusDisplay(app.Status),
            Notes = app.Notes,
            CreatedAt = app.CreatedAt.ToString()
        };

        return Ok(dto);
    }

    private static bool IsExclusionViolation(DbUpdateException ex)
    {
        if (ex.InnerException is Npgsql.PostgresException pEx)
        {
            return pEx.SqlState == "23P01" ||
                   pEx.ConstraintName == "exclude_overlapping_appointments" ||
                   pEx.Message.Contains("exclude_overlapping_appointments");
        }
        return false;
    }

    private static string GetStatusDisplay(AppointmentStatus status) => status switch
    {
        AppointmentStatus.Pending => "Pending — awaiting confirmation by the shop",
        AppointmentStatus.Confirmed => "Confirmed",
        AppointmentStatus.Completed => "Completed",
        AppointmentStatus.Cancelled => "Cancelled",
        AppointmentStatus.Rejected => "Rejected",
        _ => status.ToString()
    };
}
