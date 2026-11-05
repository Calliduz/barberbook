using System.Security.Claims;
using BarberBook.Data;
using BarberBook.Data.Enums;
using BarberBook.Web.Common;
using BarberBook.Web.Models.Admin;
using BarberBook.Web.Models.Appointment;
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
    private readonly DateTimeZone _zone;

    public AdminAppointmentsApiController(
        BarberBookDbContext dbContext,
        IAppointmentTransitionService transitionService,
        DateTimeZone? zone = null)
    {
        _dbContext = dbContext;
        _transitionService = transitionService;
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
            .Include(a => a.Service)
            .Include(a => a.StatusHistories)
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

        if (!Enum.TryParse<AppointmentStatus>(request.To, true, out var targetStatus))
        {
            return BadRequest(new ApiErrorResponse("VALIDATION_ERROR",
                $"Invalid status '{request.To}'. Allowed values: confirmed, rejected, completed, cancelled."));
        }

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var adminUserId))
        {
            return Unauthorized();
        }

        var actor = new TransitionActor(adminUserId, "Admin");

        var result = await _transitionService.TransitionAsync(
            id,
            targetStatus,
            actor,
            request.Note,
            cancellationToken: cancellationToken);

        if (!result.Success)
        {
            return StatusCode(result.StatusCode, new ApiErrorResponse(result.ErrorCode!, result.ErrorMessage!));
        }

        var app = result.Appointment!;
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

