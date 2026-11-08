using System.Globalization;
using System.Security.Claims;
using BarberBook.Data;
using BarberBook.Data.Entities;
using BarberBook.Data.Enums;
using BarberBook.Web.Common;
using BarberBook.Web.Models.Admin;
using BarberBook.Web.Models.Appointment;
using BarberBook.Web.Models.Inventory;
using BarberBook.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using NodaTime.Text;

namespace BarberBook.Web.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/appointments")]
public class AdminAppointmentsApiController : ControllerBase
{
    private readonly BarberBookDbContext _dbContext;
    private readonly IAppointmentTransitionService _transitionService;
    private readonly IClock _clock;
    private readonly IPurchaseSaleService _purchaseSaleService;
    private readonly DateTimeZone _zone;

    public AdminAppointmentsApiController(
        BarberBookDbContext dbContext,
        IAppointmentTransitionService transitionService,
        IClock clock,
        IPurchaseSaleService purchaseSaleService,
        DateTimeZone? zone = null)
    {
        _dbContext = dbContext;
        _transitionService = transitionService;
        _clock = clock;
        _purchaseSaleService = purchaseSaleService;
        _zone = zone ?? DateTimeZoneProviders.Tzdb["Asia/Manila"];
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] AdminAppointmentListQuery query,
        CancellationToken cancellationToken)
    {
        var dbQuery = _dbContext.Appointments
            .Include(a => a.Customer)
            .Include(a => a.Barber).ThenInclude(b => b.User)
            .Include(a => a.Barber).ThenInclude(b => b.Branch)
            .Include(a => a.Service)
            .Include(a => a.StatusHistories)
            .Include(a => a.Branch)
            .AsQueryable();

        // 1. Date Range Filter
        if (!string.IsNullOrWhiteSpace(query.StartDate))
        {
            var parseStart = LocalDatePattern.CreateWithInvariantCulture("yyyy-MM-dd").Parse(query.StartDate);
            if (parseStart.Success)
            {
                var startInstant = parseStart.Value.AtStartOfDayInZone(_zone).ToInstant();
                dbQuery = dbQuery.Where(a => a.StartsAt >= startInstant);
            }
        }

        if (!string.IsNullOrWhiteSpace(query.EndDate))
        {
            var parseEnd = LocalDatePattern.CreateWithInvariantCulture("yyyy-MM-dd").Parse(query.EndDate);
            if (parseEnd.Success)
            {
                var endInstant = parseEnd.Value.PlusDays(1).AtStartOfDayInZone(_zone).ToInstant();
                dbQuery = dbQuery.Where(a => a.StartsAt < endInstant);
            }
        }

        // 2. Status Filter
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (Enum.TryParse<AppointmentStatus>(query.Status, true, out var parsedStatus))
            {
                dbQuery = dbQuery.Where(a => a.Status == parsedStatus);
            }
        }

        // 3. Barber Filter
        if (query.BarberId.HasValue && query.BarberId.Value > 0)
        {
            dbQuery = dbQuery.Where(a => a.BarberId == query.BarberId.Value);
        }

        // 4. Customer Filter
        if (query.CustomerId.HasValue && query.CustomerId.Value > 0)
        {
            dbQuery = dbQuery.Where(a => a.CustomerId == query.CustomerId.Value);
        }

        // 5. Service Filter
        if (query.ServiceId.HasValue && query.ServiceId.Value > 0)
        {
            dbQuery = dbQuery.Where(a => a.ServiceId == query.ServiceId.Value);
        }

        // 5b. Branch Filter
        if (query.BranchId.HasValue && query.BranchId.Value > 0)
        {
            dbQuery = dbQuery.Where(a => a.BranchId == query.BranchId.Value || a.Barber.BranchId == query.BranchId.Value);
        }

        // 6. Text Search (Reference Code or Customer Name/Email/Phone)
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            dbQuery = dbQuery.Where(a =>
                a.ReferenceCode.ToLower().Contains(term) ||
                a.Customer.FullName.ToLower().Contains(term) ||
                (a.Customer.Email != null && a.Customer.Email.ToLower().Contains(term)) ||
                (a.Customer.PhoneNumber != null && a.Customer.PhoneNumber.Contains(term)));
        }

        var totalCount = await dbQuery.CountAsync(cancellationToken);

        // 7. Sorting
        bool isAsc = string.Equals(query.SortOrder, "asc", StringComparison.OrdinalIgnoreCase);
        dbQuery = (query.SortBy?.ToLower()) switch
        {
            "price" => isAsc ? dbQuery.OrderBy(a => a.Price) : dbQuery.OrderByDescending(a => a.Price),
            "status" => isAsc ? dbQuery.OrderBy(a => a.Status) : dbQuery.OrderByDescending(a => a.Status),
            "customer" => isAsc ? dbQuery.OrderBy(a => a.Customer.FullName) : dbQuery.OrderByDescending(a => a.Customer.FullName),
            _ => isAsc ? dbQuery.OrderBy(a => a.StartsAt) : dbQuery.OrderByDescending(a => a.StartsAt)
        };

        // 8. Pagination
        int page = Math.Max(1, query.Page);
        int pageSize = query.PageSize <= 0 ? 20 : Math.Min(query.PageSize, 100);

        var items = await dbQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var itemDtos = items.Select(app =>
        {
            var startsAtManila = app.StartsAt.InZone(_zone);
            var endsAtManila = app.EndsAt.InZone(_zone);

            var timeline = app.StatusHistories
                .OrderBy(h => h.ChangedAt)
                .Select(h => new StatusTimelineItemDto
                {
                    Id = h.Id,
                    FromStatus = h.FromStatus?.ToString().ToLowerInvariant(),
                    ToStatus = h.ToStatus.ToString().ToLowerInvariant(),
                    ChangedByRole = h.ChangedByRole,
                    Note = h.Note,
                    ChangedAt = h.ChangedAt.ToString(),
                    FormattedChangedAt = FormattingHelpers.FormatDateTime(h.ChangedAt)
                }).ToList();

            return new AdminAppointmentItemDto
            {
                Id = app.Id,
                ReferenceCode = app.ReferenceCode,
                CustomerId = app.CustomerId,
                CustomerName = app.Customer.FullName,
                CustomerEmail = app.Customer.Email ?? string.Empty,
                CustomerPhone = app.Customer.PhoneNumber ?? string.Empty,
                BarberId = app.BarberId,
                BarberName = app.Barber.DisplayName,
                BranchId = app.BranchId ?? app.Barber.BranchId,
                BranchName = app.Branch?.Name ?? app.Barber?.Branch?.Name,
                ServiceId = app.ServiceId,
                ServiceName = app.ServiceName,
                Price = app.Price,
                FormattedPrice = FormattingHelpers.FormatPeso(app.Price),
                StartsAt = app.StartsAt.ToString(),
                EndsAt = app.EndsAt.ToString(),
                DurationMinutes = (int)(app.EndsAt - app.StartsAt).TotalMinutes,
                FormattedDate = FormattingHelpers.FormatDate(app.StartsAt),
                FormattedTime = $"{FormattingHelpers.FormatTime12h(startsAtManila.TimeOfDay)} – {FormattingHelpers.FormatTime12h(endsAtManila.TimeOfDay)}",
                ScheduledDateString = startsAtManila.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                ScheduledTimeString = startsAtManila.TimeOfDay.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture),
                Status = app.Status.ToString().ToLowerInvariant(),
                StatusDisplay = GetStatusDisplay(app.Status),
                Notes = app.Notes,
                CreatedAt = app.CreatedAt.ToString(),
                Timeline = timeline
            };
        }).ToList();

        var response = new AdminAppointmentPagedResultDto
        {
            Items = itemDtos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };

        return Ok(response);
    }

    [HttpPost("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(
        [FromRoute] int id,
        [FromBody] UpdateAppointmentStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.To))
        {
            return BadRequest(new ApiErrorResponse("VALIDATION_ERROR", "The 'to' target status is required."));
        }

        var normalizedTo = request.To.Replace("-", "").Replace("_", "").Trim();
        if (!Enum.TryParse<AppointmentStatus>(normalizedTo, true, out var targetStatus))
        {
            return BadRequest(new ApiErrorResponse("VALIDATION_ERROR",
                $"Invalid status '{request.To}'. Allowed values: confirmed, rejected, completed, cancelled, noshow."));
        }

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var adminUserId))
        {
            return Unauthorized();
        }

        var actor = new TransitionActor(adminUserId, "Admin");

        var transitionNote = request.Note;
        if (!string.IsNullOrWhiteSpace(request.PaymentMethod))
        {
            var pMethod = request.PaymentMethod.Trim();
            transitionNote = string.IsNullOrWhiteSpace(transitionNote)
                ? $"Settled via {pMethod}"
                : $"[Payment: {pMethod}] {transitionNote.Trim()}";
        }

        var result = await _transitionService.TransitionAsync(
            id,
            targetStatus,
            actor,
            transitionNote,
            cancellationToken: cancellationToken);

        if (!result.Success)
        {
            return StatusCode(result.StatusCode, new ApiErrorResponse(result.ErrorCode!, result.ErrorMessage!));
        }

        var app = result.Appointment!;

        // If completed and retail add-ons are specified, record counter product sale attached to this appointment
        if (targetStatus == AppointmentStatus.Completed && request.AddOns != null && request.AddOns.Any())
        {
            try
            {
                var saleRequest = new CreateProductSaleRequest
                {
                    AppointmentId = app.Id,
                    CustomerId = app.CustomerId,
                    Notes = $"Add-on retail purchase at checkout (Appt #{app.ReferenceCode}, Settlement: {request.PaymentMethod ?? "Cash"})",
                    Items = request.AddOns.Select(a => new CreateProductSaleItemRequest
                    {
                        ProductId = a.ProductId,
                        Quantity = a.Quantity
                    }).ToList()
                };
                await _purchaseSaleService.CreateProductSaleAsync(saleRequest, adminUserId, cancellationToken);
            }
            catch
            {
                // Non-blocking for status transition
            }
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

    [HttpPost("bulk-status")]
    public async Task<IActionResult> BulkUpdateStatus(
        [FromBody] BulkUpdateStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (request.AppointmentIds == null || !request.AppointmentIds.Any())
        {
            return BadRequest(new ApiErrorResponse("VALIDATION_ERROR", "At least one appointment ID is required."));
        }

        var normalizedTo = request.To.Replace("-", "").Replace("_", "").Trim();
        if (!Enum.TryParse<AppointmentStatus>(normalizedTo, true, out var targetStatus))
        {
            return BadRequest(new ApiErrorResponse("VALIDATION_ERROR", $"Invalid status '{request.To}'. Allowed values: confirmed, rejected, completed, cancelled, noshow."));
        }

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int.TryParse(userIdClaim, out var adminUserId);
        var actor = new TransitionActor(adminUserId, "Admin");

        int successCount = 0;
        var errors = new List<string>();

        foreach (var id in request.AppointmentIds)
        {
            var result = await _transitionService.TransitionAsync(
                id,
                targetStatus,
                actor,
                request.Note ?? $"Bulk updated to {targetStatus}",
                cancellationToken: cancellationToken);

            if (result.Success) successCount++;
            else errors.Add($"ID {id}: {result.ErrorMessage}");
        }

        return Ok(new
        {
            successCount,
            failedCount = request.AppointmentIds.Count - successCount,
            errors
        });
    }

    [HttpPost("walk-in")]
    public async Task<IActionResult> CreateWalkIn(
        [FromBody] CreateWalkInAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int.TryParse(userIdClaim, out var adminUserId);

        var service = await _dbContext.Services.FirstOrDefaultAsync(s => s.Id == request.ServiceId, cancellationToken);
        if (service == null || !service.IsActive)
        {
            return BadRequest(new ApiErrorResponse("INVALID_SERVICE", "Selected service is invalid or inactive."));
        }

        var barber = await _dbContext.Barbers
            .Include(b => b.User)
            .FirstOrDefaultAsync(b => b.Id == request.BarberId, cancellationToken);
        if (barber == null || !barber.IsActive || !barber.User.IsActive)
        {
            return BadRequest(new ApiErrorResponse("INVALID_BARBER", "Selected barber is invalid or inactive."));
        }

        var now = _clock.GetCurrentInstant();
        Instant startsAtInstant;
        if (!string.IsNullOrWhiteSpace(request.StartsAt))
        {
            var parseResult = InstantPattern.ExtendedIso.Parse(request.StartsAt);
            if (parseResult.Success)
            {
                startsAtInstant = parseResult.Value;
            }
            else if (DateTimeOffset.TryParse(request.StartsAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
            {
                startsAtInstant = Instant.FromDateTimeOffset(dto);
            }
            else
            {
                var nowManila = now.InZone(_zone);
                int minute = (nowManila.Minute / 30) * 30;
                var alignedLocal = new LocalDateTime(nowManila.Year, nowManila.Month, nowManila.Day, nowManila.Hour, minute, 0);
                startsAtInstant = alignedLocal.InZoneLeniently(_zone).ToInstant();
            }
        }
        else
        {
            var nowManila = now.InZone(_zone);
            int minute = (nowManila.Minute / 30) * 30;
            var alignedLocal = new LocalDateTime(nowManila.Year, nowManila.Month, nowManila.Day, nowManila.Hour, minute, 0);
            startsAtInstant = alignedLocal.InZoneLeniently(_zone).ToInstant();
        }

        var endsAtInstant = startsAtInstant.Plus(Duration.FromMinutes(service.DurationMinutes));

        var slotTaken = await _dbContext.Appointments.AnyAsync(
            a => a.BarberId == request.BarberId &&
                 a.Status != AppointmentStatus.Cancelled &&
                 a.Status != AppointmentStatus.Rejected &&
                 startsAtInstant < a.EndsAt &&
                 endsAtInstant > a.StartsAt,
            cancellationToken);

        if (slotTaken)
        {
            return Conflict(new ApiErrorResponse("SLOT_TAKEN", "The selected barber already has a booking during this time slot."));
        }

        var clientName = string.IsNullOrWhiteSpace(request.ClientName) ? "Walk-In Customer" : request.ClientName.Trim();
        var clientPhone = string.IsNullOrWhiteSpace(request.ClientPhone) ? "09000000000" : request.ClientPhone.Trim();

        var customer = await _dbContext.Users.FirstOrDefaultAsync(u => u.Role == UserRole.Customer && u.PhoneNumber == clientPhone, cancellationToken);
        if (customer == null)
        {
            var randSuffix = Guid.NewGuid().ToString("N")[..6];
            customer = new ApplicationUser
            {
                UserName = $"walkin_{randSuffix}@barberbook.local",
                Email = $"walkin_{randSuffix}@barberbook.local",
                FullName = clientName,
                PhoneNumber = clientPhone,
                Role = UserRole.Customer,
                IsActive = true,
                CreatedAt = now
            };
            _dbContext.Users.Add(customer);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var datePrefix = startsAtInstant.InZone(_zone).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var prefix = $"BB-{datePrefix}-";
        var countToday = await _dbContext.Appointments.CountAsync(a => a.ReferenceCode.StartsWith(prefix), cancellationToken);
        int seq = countToday + 1;
        string refCode;
        do
        {
            refCode = $"{prefix}{seq:D4}";
            seq++;
        } while (await _dbContext.Appointments.AnyAsync(a => a.ReferenceCode == refCode, cancellationToken));

        var appointment = new Appointment
        {
            ReferenceCode = refCode,
            CustomerId = customer.Id,
            BarberId = barber.Id,
            BranchId = request.BranchId ?? barber.BranchId,
            ServiceId = service.Id,
            StartsAt = startsAtInstant,
            EndsAt = endsAtInstant,
            Status = AppointmentStatus.Confirmed,
            Price = service.Price,
            ServiceName = service.Name,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? "Express Walk-In Booking" : request.Notes.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.Appointments.Add(appointment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _dbContext.StatusHistories.Add(new StatusHistory
        {
            AppointmentId = appointment.Id,
            FromStatus = null,
            ToStatus = AppointmentStatus.Confirmed,
            ChangedById = adminUserId,
            ChangedByRole = "Admin",
            Note = "Express walk-in booking created and confirmed by front desk staff",
            ChangedAt = now
        });
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            id = appointment.Id,
            referenceCode = appointment.ReferenceCode,
            customerName = customer.FullName,
            barberName = barber.DisplayName,
            serviceName = service.Name,
            price = appointment.Price,
            formattedPrice = FormattingHelpers.FormatPeso(appointment.Price),
            status = "confirmed",
            startsAt = appointment.StartsAt.ToString()
        });
    }

    private static string GetStatusDisplay(AppointmentStatus status) => status switch
    {
        AppointmentStatus.Pending => "Pending",
        AppointmentStatus.Confirmed => "Confirmed",
        AppointmentStatus.Completed => "Completed",
        AppointmentStatus.Cancelled => "Cancelled",
        AppointmentStatus.Rejected => "Rejected",
        AppointmentStatus.NoShow => "No-Show",
        _ => status.ToString()
    };
}

