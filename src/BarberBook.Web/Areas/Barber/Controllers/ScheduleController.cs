using System.Globalization;
using BarberBook.Data;
using BarberBook.Data.Entities;
using BarberBook.Data.Enums;
using BarberBook.Web.Common;
using BarberBook.Web.Models.Schedule;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using NodaTime.Text;

namespace BarberBook.Web.Areas.Barber.Controllers;

[Area("Barber")]
[Authorize(Roles = "Barber")]
public class ScheduleController : Controller
{
    private readonly BarberBookDbContext _dbContext;
    private readonly IValidator<SetWorkingHoursRequest> _workingHoursValidator;
    private readonly IValidator<CreateDayOffRequest> _dayOffValidator;
    private readonly DateTimeZone _zone;

    public ScheduleController(
        BarberBookDbContext dbContext,
        IValidator<SetWorkingHoursRequest> workingHoursValidator,
        IValidator<CreateDayOffRequest> dayOffValidator,
        DateTimeZone? zone = null)
    {
        _dbContext = dbContext;
        _workingHoursValidator = workingHoursValidator;
        _dayOffValidator = dayOffValidator;
        _zone = zone ?? DateTimeZoneProviders.Tzdb["Asia/Manila"];
    }

    private async Task<BarberBook.Data.Entities.Barber?> GetCurrentBarberAsync()
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return null;

        return await _dbContext.Barbers
            .FirstOrDefaultAsync(b => b.UserId == userId.Value);
    }

    [HttpGet("api/barber/working-hours")]
    public async Task<IActionResult> GetWorkingHours()
    {
        var barber = await GetCurrentBarberAsync();
        if (barber == null)
        {
            return Forbid();
        }

        var hours = await _dbContext.WorkingHours
            .Where(w => w.BarberId == barber.Id)
            .OrderBy(w => w.DayOfWeek)
            .ThenBy(w => w.StartTime)
            .Select(w => new WorkingHoursItemDto
            {
                Id = w.Id,
                DayOfWeek = w.DayOfWeek,
                DayName = GetDayName(w.DayOfWeek),
                StartTime = w.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture),
                EndTime = w.EndTime.ToString("HH:mm", CultureInfo.InvariantCulture)
            })
            .ToListAsync();

        return Ok(hours);
    }

    [HttpPut("api/barber/working-hours")]
    public async Task<IActionResult> SetWorkingHours([FromBody] SetWorkingHoursRequest request)
    {
        var barber = await GetCurrentBarberAsync();
        if (barber == null)
        {
            return Forbid();
        }

        var validationResult = await _workingHoursValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.ToDictionary(e => e.PropertyName, e => e.ErrorMessage);
            return BadRequest(new ApiErrorResponse("VALIDATION_ERROR", "Validation failed.", errors));
        }

        var existing = await _dbContext.WorkingHours
            .Where(w => w.BarberId == barber.Id)
            .ToListAsync();

        _dbContext.WorkingHours.RemoveRange(existing);

        var newEntities = new List<WorkingHours>();
        foreach (var range in request.Ranges)
        {
            var startTime = LocalTimePattern.CreateWithInvariantCulture("HH:mm").Parse(range.StartTime).Value;
            var endTime = LocalTimePattern.CreateWithInvariantCulture("HH:mm").Parse(range.EndTime).Value;

            newEntities.Add(new WorkingHours
            {
                BarberId = barber.Id,
                DayOfWeek = range.DayOfWeek,
                StartTime = startTime,
                EndTime = endTime
            });
        }

        _dbContext.WorkingHours.AddRange(newEntities);
        await _dbContext.SaveChangesAsync();

        return Ok(new { success = true, count = newEntities.Count, message = "Weekly hours updated successfully." });
    }

    [HttpDelete("api/barber/working-hours/{id:int}")]
    public async Task<IActionResult> DeleteWorkingHourRange(int id)
    {
        var barber = await GetCurrentBarberAsync();
        if (barber == null)
        {
            return Forbid();
        }

        var range = await _dbContext.WorkingHours
            .FirstOrDefaultAsync(w => w.Id == id && w.BarberId == barber.Id);

        if (range == null)
        {
            return NotFound(new ApiErrorResponse("NOT_FOUND", "Working hour range not found."));
        }

        _dbContext.WorkingHours.Remove(range);
        await _dbContext.SaveChangesAsync();

        return Ok(new { success = true, message = "Working hour range removed." });
    }

    [HttpGet("api/barber/days-off")]
    public async Task<IActionResult> GetDaysOff()
    {
        var barber = await GetCurrentBarberAsync();
        if (barber == null)
        {
            return Forbid();
        }

        var daysOff = await _dbContext.DaysOff
            .Where(d => d.BarberId == barber.Id)
            .OrderBy(d => d.Date)
            .Select(d => new DayOffItemDto
            {
                Id = d.Id,
                Date = d.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                FormattedDate = d.Date.ToString("MMMM dd, yyyy", CultureInfo.InvariantCulture),
                Reason = d.Reason
            })
            .ToListAsync();

        return Ok(daysOff);
    }

    [HttpPost("api/barber/days-off")]
    public async Task<IActionResult> AddDayOff([FromBody] CreateDayOffRequest request)
    {
        var barber = await GetCurrentBarberAsync();
        if (barber == null)
        {
            return Forbid();
        }

        var validationResult = await _dayOffValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.ToDictionary(e => e.PropertyName, e => e.ErrorMessage);
            return BadRequest(new ApiErrorResponse("VALIDATION_ERROR", "Validation failed.", errors));
        }

        var startDate = LocalDatePattern.CreateWithInvariantCulture("yyyy-MM-dd").Parse(request.StartDate).Value;
        var endDate = string.IsNullOrWhiteSpace(request.EndDate)
            ? startDate
            : LocalDatePattern.CreateWithInvariantCulture("yyyy-MM-dd").Parse(request.EndDate).Value;

        var datesToAdd = new List<LocalDate>();
        for (var d = startDate; d <= endDate; d = d.PlusDays(1))
        {
            datesToAdd.Add(d);
        }

        // Check for conflicts with existing pending/confirmed appointments (WARN, NEVER AUTO-CANCEL)
        var rangeStartUtc = startDate.AtStartOfDayInZone(_zone).ToInstant();
        var rangeEndUtc = endDate.PlusDays(1).AtStartOfDayInZone(_zone).ToInstant();

        var conflictingAppointments = await _dbContext.Appointments
            .Include(a => a.Customer)
            .Include(a => a.Service)
            .Where(a => a.BarberId == barber.Id &&
                        (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed) &&
                        a.StartsAt < rangeEndUtc &&
                        a.EndsAt > rangeStartUtc)
            .OrderBy(a => a.StartsAt)
            .ToListAsync();

        var conflicts = conflictingAppointments.Select(a =>
        {
            var localStart = a.StartsAt.InZone(_zone);
            return new DayOffConflictDto
            {
                AppointmentId = a.Id,
                ReferenceCode = a.ReferenceCode,
                CustomerName = a.Customer.FullName,
                ScheduledDate = localStart.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ScheduledTime = FormattingHelpers.FormatTime12h(a.StartsAt),
                ServiceName = a.ServiceName,
                Status = a.Status.ToString().ToLowerInvariant()
            };
        }).ToList();

        var existingDates = await _dbContext.DaysOff
            .Where(d => d.BarberId == barber.Id && d.Date >= startDate && d.Date <= endDate)
            .Select(d => d.Date)
            .ToListAsync();

        var addedList = new List<string>();
        foreach (var date in datesToAdd)
        {
            if (!existingDates.Contains(date))
            {
                _dbContext.DaysOff.Add(new DayOff
                {
                    BarberId = barber.Id,
                    Date = date,
                    Reason = request.Reason
                });
                addedList.Add(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            }
        }

        await _dbContext.SaveChangesAsync();

        var result = new CreateDayOffResultDto
        {
            CreatedCount = addedList.Count,
            DatesAdded = addedList,
            Conflicts = conflicts,
            Message = conflicts.Any()
                ? $"Added {addedList.Count} day(s) off. Warning: {conflicts.Count} existing appointment(s) conflict with this schedule (not auto-cancelled)."
                : $"Added {addedList.Count} day(s) off successfully."
        };

        return Ok(result);
    }

    [HttpDelete("api/barber/days-off/{id:int}")]
    public async Task<IActionResult> DeleteDayOff(int id)
    {
        var barber = await GetCurrentBarberAsync();
        if (barber == null)
        {
            return Forbid();
        }

        var dayOff = await _dbContext.DaysOff
            .FirstOrDefaultAsync(d => d.Id == id && d.BarberId == barber.Id);

        if (dayOff == null)
        {
            return NotFound(new ApiErrorResponse("NOT_FOUND", "Day off record not found."));
        }

        _dbContext.DaysOff.Remove(dayOff);
        await _dbContext.SaveChangesAsync();

        return Ok(new { success = true, message = "Day off record removed." });
    }

    private static string GetDayName(int dayOfWeek) => dayOfWeek switch
    {
        0 => "Sunday",
        1 => "Monday",
        2 => "Tuesday",
        3 => "Wednesday",
        4 => "Thursday",
        5 => "Friday",
        6 => "Saturday",
        _ => "Unknown"
    };
}

