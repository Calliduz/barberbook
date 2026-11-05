using System.Globalization;
using BarberBook.Data;
using BarberBook.Web.Common;
using BarberBook.Web.Models.Schedule;
using BarberBook.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using NodaTime.Text;

namespace BarberBook.Web.Controllers;

[ApiController]
[Route("api/barbers")]
public class BarbersApiController : ControllerBase
{
    private readonly BarberBookDbContext _dbContext;
    private readonly IAvailabilityService _availabilityService;
    private readonly IClock _clock;
    private readonly DateTimeZone _zone;

    public BarbersApiController(
        BarberBookDbContext dbContext,
        IAvailabilityService availabilityService,
        IClock clock,
        DateTimeZone? zone = null)
    {
        _dbContext = dbContext;
        _availabilityService = availabilityService;
        _clock = clock;
        _zone = zone ?? DateTimeZoneProviders.Tzdb["Asia/Manila"];
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var barbers = await _dbContext.Barbers
            .Include(b => b.User)
            .Include(b => b.WorkingHours)
            .Where(b => b.IsActive && b.User.IsActive)
            .OrderBy(b => b.DisplayName)
            .ToListAsync();

        var dtoList = barbers.Select(b => new
        {
            id = b.Id,
            displayName = b.DisplayName,
            bio = b.Bio,
            photoUrl = b.PhotoUrl,
            specialties = b.Specialties,
            weeklyScheduleText = FormattingHelpers.FormatWeeklySchedule(b.WorkingHours)
        }).ToList();

        return Ok(dtoList);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var barber = await _dbContext.Barbers
            .Include(b => b.User)
            .Include(b => b.WorkingHours)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (barber == null || !barber.IsActive || !barber.User.IsActive)
        {
            return NotFound(new ApiErrorResponse("NOT_FOUND", $"Barber with ID {id} not found or inactive."));
        }

        var today = _clock.GetCurrentInstant().InZone(_zone).Date;
        var upcomingDaysOff = await _dbContext.DaysOff
            .Where(d => d.BarberId == id && d.Date >= today)
            .OrderBy(d => d.Date)
            .Take(30)
            .Select(d => new PublicDayOffDto
            {
                Date = d.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Reason = d.Reason
            })
            .ToListAsync();

        var weeklyHours = barber.WorkingHours
            .OrderBy(w => w.DayOfWeek)
            .ThenBy(w => w.StartTime)
            .Select(w => new PublicWorkingHoursDto
            {
                DayOfWeek = w.DayOfWeek,
                DayName = GetDayName(w.DayOfWeek),
                StartTime = w.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture),
                EndTime = w.EndTime.ToString("HH:mm", CultureInfo.InvariantCulture)
            })
            .ToList();

        var dto = new
        {
            id = barber.Id,
            displayName = barber.DisplayName,
            bio = barber.Bio,
            photoUrl = barber.PhotoUrl,
            specialties = barber.Specialties,
            weeklyScheduleText = FormattingHelpers.FormatWeeklySchedule(barber.WorkingHours),
            weeklyHours = weeklyHours,
            upcomingDaysOff = upcomingDaysOff
        };

        return Ok(dto);
    }

    [HttpGet("{id:int}/schedule")]
    public async Task<IActionResult> GetSchedule(int id)
    {
        var barber = await _dbContext.Barbers
            .Include(b => b.User)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (barber == null || !barber.IsActive || !barber.User.IsActive)
        {
            return NotFound(new ApiErrorResponse("NOT_FOUND", $"Barber with ID {id} not found or inactive."));
        }

        var weeklyHours = await _dbContext.WorkingHours
            .Where(w => w.BarberId == id)
            .OrderBy(w => w.DayOfWeek)
            .ThenBy(w => w.StartTime)
            .Select(w => new PublicWorkingHoursDto
            {
                DayOfWeek = w.DayOfWeek,
                DayName = GetDayName(w.DayOfWeek),
                StartTime = w.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture),
                EndTime = w.EndTime.ToString("HH:mm", CultureInfo.InvariantCulture)
            })
            .ToListAsync();

        var today = _clock.GetCurrentInstant().InZone(_zone).Date;
        var upcomingDaysOff = await _dbContext.DaysOff
            .Where(d => d.BarberId == id && d.Date >= today)
            .OrderBy(d => d.Date)
            .Take(30)
            .Select(d => new PublicDayOffDto
            {
                Date = d.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Reason = d.Reason
            })
            .ToListAsync();

        var response = new PublicBarberScheduleDto
        {
            BarberId = barber.Id,
            DisplayName = barber.DisplayName,
            WeeklyHours = weeklyHours,
            UpcomingDaysOff = upcomingDaysOff
        };

        return Ok(response);
    }

    [HttpGet("{id:int}/availability")]
    public async Task<IActionResult> GetAvailability(
        [FromRoute] int id,
        [FromQuery] int serviceId,
        [FromQuery] string date)
    {
        if (serviceId <= 0)
        {
            return BadRequest(new ApiErrorResponse("VALIDATION_ERROR", "A valid serviceId parameter is required."));
        }

        if (string.IsNullOrWhiteSpace(date))
        {
            return BadRequest(new ApiErrorResponse("VALIDATION_ERROR", "The date parameter is required in YYYY-MM-DD format."));
        }

        var parseResult = LocalDatePattern.CreateWithInvariantCulture("yyyy-MM-dd").Parse(date);
        if (!parseResult.Success)
        {
            return BadRequest(new ApiErrorResponse("VALIDATION_ERROR", "Invalid date format. Expected YYYY-MM-DD."));
        }

        var localDate = parseResult.Value;

        var slotDetails = await _availabilityService.GetAvailableSlotDetailsAsync(id, serviceId, localDate);
        var slots = slotDetails.Select(s => s.Time.ToString("HH:mm", CultureInfo.InvariantCulture)).ToList();

        var response = new PublicAvailabilityResponse
        {
            BarberId = id,
            ServiceId = serviceId,
            Date = date,
            Slots = slots,
            SlotDetails = slotDetails
        };

        return Ok(response);
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

