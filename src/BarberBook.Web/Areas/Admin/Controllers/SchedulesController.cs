using System.Globalization;
using BarberBook.Data;
using BarberBook.Data.Entities;
using BarberBook.Data.Enums;
using BarberBook.Web.Common;
using BarberBook.Web.Models.Admin;
using BarberBook.Web.Models.Schedule;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using NodaTime.Text;

namespace BarberBook.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class SchedulesController : Controller
{
    private readonly BarberBookDbContext _dbContext;
    private readonly IValidator<SetWorkingHoursRequest> _workingHoursValidator;
    private readonly IValidator<CreateDayOffRequest> _dayOffValidator;
    private readonly DateTimeZone _zone;

    public SchedulesController(
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

    [HttpGet("admin/schedules")]
    public async Task<IActionResult> Index([FromQuery] int? barberId = null)
    {
        var barbers = await _dbContext.Barbers
            .Include(b => b.User)
            .OrderBy(b => b.DisplayName)
            .Select(b => new AdminBarberItemDto
            {
                Id = b.Id,
                DisplayName = b.DisplayName,
                FullName = b.User.FullName,
                Email = b.User.Email ?? string.Empty,
                IsActive = b.IsActive && b.User.IsActive
            })
            .ToListAsync();

        int selectedId = barberId ?? barbers.FirstOrDefault()?.Id ?? 0;
        var selectedBarber = barbers.FirstOrDefault(b => b.Id == selectedId);

        var viewModel = new AdminBarberScheduleViewModel
        {
            Barbers = barbers,
            SelectedBarberId = selectedId,
            SelectedBarber = selectedBarber
        };

        if (selectedId > 0)
        {
            // Working hours
            var workingHours = await _dbContext.WorkingHours
                .Where(w => w.BarberId == selectedId)
                .OrderBy(w => w.DayOfWeek)
                .ThenBy(w => w.StartTime)
                .ToListAsync();

            for (int i = 0; i <= 6; i++)
            {
                viewModel.WeeklyHours[i] = workingHours
                    .Where(w => w.DayOfWeek == i)
                    .Select(w => new WorkingHoursItemDto
                    {
                        Id = w.Id,
                        DayOfWeek = w.DayOfWeek,
                        DayName = GetDayName(w.DayOfWeek),
                        StartTime = w.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture),
                        EndTime = w.EndTime.ToString("HH:mm", CultureInfo.InvariantCulture)
                    })
                    .ToList();
            }

            // Days off (upcoming & recent)
            var today = SystemClock.Instance.GetCurrentInstant().InZone(_zone).Date;
            var daysOff = await _dbContext.DaysOff
                .Where(d => d.BarberId == selectedId && d.Date >= today.PlusDays(-7))
                .OrderBy(d => d.Date)
                .ToListAsync();

            viewModel.DaysOff = daysOff.Select(d => new DayOffItemDto
            {
                Id = d.Id,
                Date = d.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                FormattedDate = d.Date.ToString("MMMM dd, yyyy", CultureInfo.InvariantCulture),
                Reason = d.Reason
            }).ToList();
        }

        return View(viewModel);
    }

    [HttpGet("api/admin/barbers/{barberId:int}/working-hours")]
    public async Task<IActionResult> GetWorkingHours(int barberId)
    {
        var barber = await _dbContext.Barbers.FindAsync(barberId);
        if (barber == null)
        {
            return NotFound(new ApiErrorResponse("NOT_FOUND", $"Barber with ID {barberId} not found."));
        }

        var hours = await _dbContext.WorkingHours
            .Where(w => w.BarberId == barberId)
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

    [HttpPut("api/admin/barbers/{barberId:int}/working-hours")]
    public async Task<IActionResult> SetWorkingHours(int barberId, [FromBody] SetWorkingHoursRequest request)
    {
        var validationResult = await _workingHoursValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.ToDictionary(e => e.PropertyName, e => e.ErrorMessage);
            return BadRequest(new ApiErrorResponse("VALIDATION_ERROR", "Validation failed.", errors));
        }

        var barber = await _dbContext.Barbers.FindAsync(barberId);
        if (barber == null)
        {
            return NotFound(new ApiErrorResponse("NOT_FOUND", $"Barber with ID {barberId} not found."));
        }

        // Replace all existing working hours for this barber
        var existing = await _dbContext.WorkingHours
            .Where(w => w.BarberId == barberId)
            .ToListAsync();

        _dbContext.WorkingHours.RemoveRange(existing);

        var newEntities = new List<WorkingHours>();
        foreach (var range in request.Ranges)
        {
            var startTime = LocalTimePattern.CreateWithInvariantCulture("HH:mm").Parse(range.StartTime).Value;
            var endTime = LocalTimePattern.CreateWithInvariantCulture("HH:mm").Parse(range.EndTime).Value;

            newEntities.Add(new WorkingHours
            {
                BarberId = barberId,
                DayOfWeek = range.DayOfWeek,
                StartTime = startTime,
                EndTime = endTime
            });
        }

        _dbContext.WorkingHours.AddRange(newEntities);
        await _dbContext.SaveChangesAsync();

        return Ok(new { success = true, count = newEntities.Count, message = "Working hours updated successfully." });
    }

    [HttpDelete("api/admin/barbers/{barberId:int}/working-hours/{id:int}")]
    public async Task<IActionResult> DeleteWorkingHourRange(int barberId, int id)
    {
        var range = await _dbContext.WorkingHours
            .FirstOrDefaultAsync(w => w.Id == id && w.BarberId == barberId);

        if (range == null)
        {
            return NotFound(new ApiErrorResponse("NOT_FOUND", "Working hour range not found."));
        }

        _dbContext.WorkingHours.Remove(range);
        await _dbContext.SaveChangesAsync();

        return Ok(new { success = true, message = "Working hour range removed." });
    }

    [HttpGet("api/admin/barbers/{barberId:int}/days-off")]
    public async Task<IActionResult> GetDaysOff(int barberId)
    {
        var barber = await _dbContext.Barbers.FindAsync(barberId);
        if (barber == null)
        {
            return NotFound(new ApiErrorResponse("NOT_FOUND", $"Barber with ID {barberId} not found."));
        }

        var daysOff = await _dbContext.DaysOff
            .Where(d => d.BarberId == barberId)
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

    [HttpPost("api/admin/barbers/{barberId:int}/days-off")]
    public async Task<IActionResult> AddDayOff(int barberId, [FromBody] CreateDayOffRequest request)
    {
        var validationResult = await _dayOffValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.ToDictionary(e => e.PropertyName, e => e.ErrorMessage);
            return BadRequest(new ApiErrorResponse("VALIDATION_ERROR", "Validation failed.", errors));
        }

        var barber = await _dbContext.Barbers.FindAsync(barberId);
        if (barber == null)
        {
            return NotFound(new ApiErrorResponse("NOT_FOUND", $"Barber with ID {barberId} not found."));
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

        // 1. Check for conflicts with existing pending/confirmed appointments (WARN, NEVER AUTO-CANCEL)
        var rangeStartUtc = startDate.AtStartOfDayInZone(_zone).ToInstant();
        var rangeEndUtc = endDate.PlusDays(1).AtStartOfDayInZone(_zone).ToInstant();

        var conflictingAppointments = await _dbContext.Appointments
            .Include(a => a.Customer)
            .Include(a => a.Service)
            .Where(a => a.BarberId == barberId &&
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

        // 2. Add days off for dates that don't already exist
        var existingDates = await _dbContext.DaysOff
            .Where(d => d.BarberId == barberId && d.Date >= startDate && d.Date <= endDate)
            .Select(d => d.Date)
            .ToListAsync();

        var addedList = new List<string>();
        foreach (var date in datesToAdd)
        {
            if (!existingDates.Contains(date))
            {
                _dbContext.DaysOff.Add(new DayOff
                {
                    BarberId = barberId,
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

    [HttpDelete("api/admin/barbers/{barberId:int}/days-off/{id:int}")]
    public async Task<IActionResult> DeleteDayOff(int barberId, int id)
    {
        var dayOff = await _dbContext.DaysOff
            .FirstOrDefaultAsync(d => d.Id == id && d.BarberId == barberId);

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
