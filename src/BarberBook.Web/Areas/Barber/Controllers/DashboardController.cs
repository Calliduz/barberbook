using BarberBook.Data;
using BarberBook.Data.Entities;
using BarberBook.Data.Enums;
using BarberBook.Web.Common;
using BarberBook.Web.Models.Barber;
using BarberBook.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using NodaTime.Text;

namespace BarberBook.Web.Areas.Barber.Controllers;

[Area("Barber")]
[Authorize(Roles = "Barber")]
[Route("barber")]
public class DashboardController : Controller
{
    private readonly BarberBookDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAppointmentTransitionService _transitionService;
    private readonly IReportingService _reportingService;
    private readonly IEquipmentService _equipmentService;
    private readonly IClock _clock;
    private readonly DateTimeZone _zone;

    public DashboardController(
        BarberBookDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IAppointmentTransitionService transitionService,
        IReportingService reportingService,
        IEquipmentService equipmentService,
        IClock clock,
        DateTimeZone? zone = null)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _transitionService = transitionService;
        _reportingService = reportingService;
        _equipmentService = equipmentService;
        _clock = clock;
        _zone = zone ?? DateTimeZoneProviders.Tzdb["Asia/Manila"];
    }

    private async Task<BarberBook.Data.Entities.Barber?> GetCurrentBarberAsync()
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return null;

        return await _dbContext.Barbers
            .Include(b => b.User)
            .Include(b => b.WorkingHours)
            .Include(b => b.Branch)
            .FirstOrDefaultAsync(b => b.UserId == userId.Value);
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var barber = await GetCurrentBarberAsync();
        if (barber == null) return Forbid();

        var today = _clock.GetCurrentInstant().InZone(_zone).Date;
        var startOfToday = today.AtStartOfDayInZone(_zone).ToInstant();
        var endOfToday = today.PlusDays(1).AtStartOfDayInZone(_zone).ToInstant();

        var appointmentsQuery = _dbContext.Appointments
            .Include(a => a.Customer)
            .Include(a => a.Service)
            .Include(a => a.StatusHistories)
            .Include(a => a.Branch)
            .Include(a => a.Barber).ThenInclude(b => b.Branch)
            .Where(a => a.BarberId == barber.Id);

        var todayAppointments = await appointmentsQuery
            .Where(a => a.StartsAt >= startOfToday && a.StartsAt < endOfToday)
            .OrderBy(a => a.StartsAt)
            .ToListAsync();

        var pendingCount = await appointmentsQuery
            .CountAsync(a => a.Status == AppointmentStatus.Pending);

        var completedToday = todayAppointments
            .Where(a => a.Status == AppointmentStatus.Completed)
            .ToList();

        var todayEarnings = completedToday.Sum(a => a.Price);

        var nowInstant = _clock.GetCurrentInstant();
        var nextApp = todayAppointments
            .FirstOrDefault(a => a.StartsAt >= nowInstant &&
                                (a.Status == AppointmentStatus.Confirmed || a.Status == AppointmentStatus.Pending));

        var model = new BarberDashboardViewModel
        {
            BarberName = barber.DisplayName,
            BarberSpecialty = barber.Specialties ?? "Master Grooming Craftsman",
            BarberPhoto = barber.PhotoUrl,
            TodayFormattedDate = today.ToString("dddd, MMMM dd, yyyy", System.Globalization.CultureInfo.InvariantCulture),
            TodayAppointmentsCount = todayAppointments.Count,
            PendingAppointmentsCount = pendingCount,
            CompletedAppointmentsCount = completedToday.Count,
            TodayEarnings = todayEarnings,
            FormattedTodayEarnings = FormattingHelpers.FormatPeso(todayEarnings),
            NextAppointment = nextApp != null ? MapToItem(nextApp) : null,
            TodayAppointments = todayAppointments.Select(MapToItem).ToList()
        };

        return View(model);
    }

    [HttpGet("schedule")]
    public async Task<IActionResult> Schedule([FromQuery] string? date = null, [FromQuery] string? view = "day")
    {
        var barber = await GetCurrentBarberAsync();
        if (barber == null) return Forbid();

        var today = _clock.GetCurrentInstant().InZone(_zone).Date;
        var selectedDate = today;

        if (!string.IsNullOrWhiteSpace(date))
        {
            var parseResult = LocalDatePattern.CreateWithInvariantCulture("yyyy-MM-dd").Parse(date);
            if (parseResult.Success) selectedDate = parseResult.Value;
        }

        var viewMode = string.Equals(view, "week", StringComparison.OrdinalIgnoreCase) ? "week" : "day";

        var startOfSelectedDay = selectedDate.AtStartOfDayInZone(_zone).ToInstant();
        var endOfSelectedDay = selectedDate.PlusDays(1).AtStartOfDayInZone(_zone).ToInstant();

        // Calculate Monday of the selected week
        int diffToMonday = ((int)selectedDate.DayOfWeek + 6) % 7; // Monday = 0
        var weekStart = selectedDate.PlusDays(-diffToMonday);
        var weekEnd = weekStart.PlusDays(6);

        var startOfWeek = weekStart.AtStartOfDayInZone(_zone).ToInstant();
        var endOfWeek = weekEnd.PlusDays(1).AtStartOfDayInZone(_zone).ToInstant();

        var appointments = await _dbContext.Appointments
            .Include(a => a.Customer)
            .Include(a => a.Service)
            .Include(a => a.StatusHistories)
            .Where(a => a.BarberId == barber.Id && a.StartsAt >= startOfWeek && a.StartsAt < endOfWeek)
            .OrderBy(a => a.StartsAt)
            .ToListAsync();

        var dayApps = appointments
            .Where(a => a.StartsAt >= startOfSelectedDay && a.StartsAt < endOfSelectedDay)
            .Select(MapToItem)
            .ToList();

        var weekGroups = new List<BarberScheduleDayGroup>();
        for (int i = 0; i < 7; i++)
        {
            var currentDay = weekStart.PlusDays(i);
            var dayStart = currentDay.AtStartOfDayInZone(_zone).ToInstant();
            var dayEnd = currentDay.PlusDays(1).AtStartOfDayInZone(_zone).ToInstant();

            var appsForDay = appointments
                .Where(a => a.StartsAt >= dayStart && a.StartsAt < dayEnd)
                .Select(MapToItem)
                .ToList();

            weekGroups.Add(new BarberScheduleDayGroup
            {
                Date = currentDay,
                DateFormatted = currentDay.ToString("MMM dd", System.Globalization.CultureInfo.InvariantCulture),
                DayName = currentDay.ToString("dddd", System.Globalization.CultureInfo.InvariantCulture),
                IsToday = currentDay == today,
                IsSelected = currentDay == selectedDate,
                Appointments = appsForDay
            });
        }

        var selectedDow = (int)selectedDate.DayOfWeek % 7;
        var workingHoursToday = barber.WorkingHours
            .FirstOrDefault(w => w.DayOfWeek == selectedDow);

        var model = new BarberScheduleViewModel
        {
            BarberId = barber.Id,
            BarberName = barber.DisplayName,
            SelectedDate = selectedDate,
            SelectedDateFormatted = selectedDate.ToString("MMMM dd, yyyy", System.Globalization.CultureInfo.InvariantCulture),
            ViewMode = viewMode,
            PrevDateString = viewMode == "week" ? weekStart.PlusDays(-7).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) : selectedDate.PlusDays(-1).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            NextDateString = viewMode == "week" ? weekStart.PlusDays(7).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) : selectedDate.PlusDays(1).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            TodayDateString = today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            WeekStartDateFormatted = weekStart.ToString("MMM dd", System.Globalization.CultureInfo.InvariantCulture),
            WeekEndDateFormatted = weekEnd.ToString("MMM dd, yyyy", System.Globalization.CultureInfo.InvariantCulture),
            WorkingHoursToday = workingHoursToday != null ? $"{workingHoursToday.StartTime.ToString("hh:mm tt", System.Globalization.CultureInfo.InvariantCulture)} – {workingHoursToday.EndTime.ToString("hh:mm tt", System.Globalization.CultureInfo.InvariantCulture)}" : "Off Duty / No Scheduled Hours",
            DayAppointments = dayApps,
            WeekDays = weekGroups
        };

        return View(model);
    }

    [HttpGet("appointments")]
    public async Task<IActionResult> Appointments(
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] string? startDate = null,
        [FromQuery] string? endDate = null)
    {
        var barber = await GetCurrentBarberAsync();
        if (barber == null) return Forbid();

        var query = _dbContext.Appointments
            .Include(a => a.Customer)
            .Include(a => a.Service)
            .Include(a => a.StatusHistories)
            .Include(a => a.Branch)
            .Include(a => a.Barber).ThenInclude(b => b.Branch)
            .Where(a => a.BarberId == barber.Id)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<AppointmentStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(a => a.Status == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(a => a.ReferenceCode.ToLower().Contains(term) ||
                                     a.Customer.FullName.ToLower().Contains(term) ||
                                     a.ServiceName.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(startDate))
        {
            var parseStart = LocalDatePattern.CreateWithInvariantCulture("yyyy-MM-dd").Parse(startDate);
            if (parseStart.Success)
            {
                var startInstant = parseStart.Value.AtStartOfDayInZone(_zone).ToInstant();
                query = query.Where(a => a.StartsAt >= startInstant);
            }
        }

        if (!string.IsNullOrWhiteSpace(endDate))
        {
            var parseEnd = LocalDatePattern.CreateWithInvariantCulture("yyyy-MM-dd").Parse(endDate);
            if (parseEnd.Success)
            {
                var endInstant = parseEnd.Value.PlusDays(1).AtStartOfDayInZone(_zone).ToInstant();
                query = query.Where(a => a.StartsAt < endInstant);
            }
        }

        var allBarberApps = await _dbContext.Appointments
            .Where(a => a.BarberId == barber.Id)
            .ToListAsync();

        var filteredApps = await query
            .OrderByDescending(a => a.StartsAt)
            .ToListAsync();

        var model = new BarberAppointmentsViewModel
        {
            BarberId = barber.Id,
            BarberName = barber.DisplayName,
            StatusFilter = status,
            SearchTerm = search,
            StartDate = startDate,
            EndDate = endDate,
            TotalCount = allBarberApps.Count,
            PendingCount = allBarberApps.Count(a => a.Status == AppointmentStatus.Pending),
            ConfirmedCount = allBarberApps.Count(a => a.Status == AppointmentStatus.Confirmed),
            CompletedCount = allBarberApps.Count(a => a.Status == AppointmentStatus.Completed),
            CancelledCount = allBarberApps.Count(a => a.Status == AppointmentStatus.Cancelled),
            RejectedCount = allBarberApps.Count(a => a.Status == AppointmentStatus.Rejected),
            Appointments = filteredApps.Select(MapToItem).ToList()
        };

        return View(model);
    }

    [HttpGet("appointments/{id:int}")]
    public async Task<IActionResult> AppointmentDetail([FromRoute] int id)
    {
        var barber = await GetCurrentBarberAsync();
        if (barber == null) return Forbid();

        var app = await _dbContext.Appointments
            .Include(a => a.Customer)
            .Include(a => a.Service)
            .Include(a => a.StatusHistories)
            .Include(a => a.Branch)
            .Include(a => a.Barber).ThenInclude(b => b.Branch)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (app == null || app.BarberId != barber.Id)
        {
            if (Request.Headers.Accept.ToString().Contains("application/json"))
            {
                return NotFound(new ApiErrorResponse("NOT_FOUND", "Appointment not found or not assigned to you."));
            }
            TempData["ErrorMessage"] = "Appointment not found or not assigned to you.";
            return RedirectToAction(nameof(Appointments));
        }

        var model = MapToItem(app);
        return View("AppointmentDetail", model);
    }

    [HttpGet("/api/barber/appointments/{id:int}")]
    public async Task<IActionResult> ApiAppointmentDetail([FromRoute] int id)
    {
        var barber = await GetCurrentBarberAsync();
        if (barber == null) return Forbid();

        var app = await _dbContext.Appointments
            .Include(a => a.Customer)
            .Include(a => a.Service)
            .Include(a => a.StatusHistories)
            .Include(a => a.Branch)
            .Include(a => a.Barber).ThenInclude(b => b.Branch)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (app == null || app.BarberId != barber.Id)
        {
            return NotFound(new ApiErrorResponse("NOT_FOUND", "Appointment not found or not assigned to you."));
        }

        var model = MapToItem(app);
        return Ok(model);
    }

    [HttpPost("appointments/{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(
        [FromRoute] int id,
        [FromForm] string to,
        [FromForm] string? note = null)
    {
        var barber = await GetCurrentBarberAsync();
        if (barber == null) return Forbid();

        var app = await _dbContext.Appointments
            .FirstOrDefaultAsync(a => a.Id == id && a.BarberId == barber.Id);

        if (app == null)
        {
            TempData["ErrorMessage"] = "Appointment not found or not assigned to you.";
            return RedirectToAction(nameof(Appointments));
        }

        if (!Enum.TryParse<AppointmentStatus>(to, true, out var targetStatus))
        {
            TempData["ErrorMessage"] = $"Invalid target status '{to}'.";
            return RedirectToAction(nameof(Appointments));
        }

        var actor = new TransitionActor(barber.UserId, "Barber", CustomerId: null, BarberId: barber.Id);
        var result = await _transitionService.TransitionAsync(id, targetStatus, actor, note);

        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage ?? "Status update failed.";
        }
        else
        {
            TempData["SuccessMessage"] = $"Appointment #{app.ReferenceCode} transitioned to {targetStatus.ToString().ToLowerInvariant()} successfully.";
        }

        return RedirectToAction(nameof(Appointments));
    }

    [HttpGet("availability")]
    public async Task<IActionResult> Availability()
    {
        var barber = await GetCurrentBarberAsync();
        if (barber == null) return Forbid();

        var workingHours = await _dbContext.WorkingHours
            .Where(w => w.BarberId == barber.Id)
            .OrderBy(w => w.DayOfWeek)
            .ToListAsync();

        var dayOfWeekInts = new[] { 1, 2, 3, 4, 5, 6, 0 }; // Mon to Sun

        var hoursModel = dayOfWeekInts.Select(dow =>
        {
            var match = workingHours.FirstOrDefault(w => w.DayOfWeek == dow);
            return new BarberWorkingHoursItemViewModel
            {
                Id = match?.Id,
                DayOfWeek = dow,
                DayName = GetDayName(dow),
                StartTime = match != null ? match.StartTime.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture) : "09:00",
                EndTime = match != null ? match.EndTime.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture) : "18:00",
                IsWorking = match != null
            };
        }).ToList();

        var today = _clock.GetCurrentInstant().InZone(_zone).Date;
        var daysOff = await _dbContext.DaysOff
            .Where(d => d.BarberId == barber.Id && d.Date >= today)
            .OrderBy(d => d.Date)
            .Select(d => new BarberDayOffItemViewModel
            {
                Id = d.Id,
                DateString = d.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                FormattedDate = d.Date.ToString("MMMM dd, yyyy", System.Globalization.CultureInfo.InvariantCulture),
                Reason = d.Reason
            })
            .ToListAsync();

        var model = new BarberAvailabilityViewModel
        {
            BarberId = barber.Id,
            BarberName = barber.DisplayName,
            WorkingHours = hoursModel,
            DaysOff = daysOff
        };

        return View(model);
    }

    [HttpPost("availability/working-hours")]
    public async Task<IActionResult> UpdateWorkingHours([FromForm] List<BarberWorkingHoursItemViewModel> hours)
    {
        var barber = await GetCurrentBarberAsync();
        if (barber == null) return Forbid();

        var existing = await _dbContext.WorkingHours
            .Where(w => w.BarberId == barber.Id)
            .ToListAsync();

        _dbContext.WorkingHours.RemoveRange(existing);

        var newHours = new List<WorkingHours>();
        foreach (var item in hours)
        {
            if (!item.IsWorking) continue;

            var parseStart = LocalTimePattern.CreateWithInvariantCulture("HH:mm").Parse(item.StartTime);
            var parseEnd = LocalTimePattern.CreateWithInvariantCulture("HH:mm").Parse(item.EndTime);

            if (parseStart.Success && parseEnd.Success && parseStart.Value < parseEnd.Value)
            {
                newHours.Add(new WorkingHours
                {
                    BarberId = barber.Id,
                    DayOfWeek = item.DayOfWeek,
                    StartTime = parseStart.Value,
                    EndTime = parseEnd.Value
                });
            }
        }

        _dbContext.WorkingHours.AddRange(newHours);
        await _dbContext.SaveChangesAsync();

        TempData["SuccessMessage"] = "Weekly schedule updated successfully.";
        return RedirectToAction(nameof(Availability));
    }

    [HttpPost("availability/days-off")]
    public async Task<IActionResult> AddDayOff([FromForm] string date, [FromForm] string? reason = null)
    {
        var barber = await GetCurrentBarberAsync();
        if (barber == null) return Forbid();

        var parseResult = LocalDatePattern.CreateWithInvariantCulture("yyyy-MM-dd").Parse(date);
        if (!parseResult.Success)
        {
            TempData["ErrorMessage"] = "Invalid date format.";
            return RedirectToAction(nameof(Availability));
        }

        var selectedDate = parseResult.Value;
        var today = _clock.GetCurrentInstant().InZone(_zone).Date;

        if (selectedDate < today)
        {
            TempData["ErrorMessage"] = "Cannot schedule a day off in the past.";
            return RedirectToAction(nameof(Availability));
        }

        var exists = await _dbContext.DaysOff
            .AnyAsync(d => d.BarberId == barber.Id && d.Date == selectedDate);

        if (exists)
        {
            TempData["ErrorMessage"] = "A day off is already scheduled for that date.";
            return RedirectToAction(nameof(Availability));
        }

        // Check for conflicts with existing pending/confirmed appointments (WARN, NEVER AUTO-CANCEL)
        var dayStartUtc = selectedDate.AtStartOfDayInZone(_zone).ToInstant();
        var dayEndUtc = selectedDate.PlusDays(1).AtStartOfDayInZone(_zone).ToInstant();

        var conflictCount = await _dbContext.Appointments
            .CountAsync(a => a.BarberId == barber.Id &&
                        (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed) &&
                        a.StartsAt < dayEndUtc &&
                        a.EndsAt > dayStartUtc);

        _dbContext.DaysOff.Add(new DayOff
        {
            BarberId = barber.Id,
            Date = selectedDate,
            Reason = string.IsNullOrWhiteSpace(reason) ? "Scheduled Day Off" : reason.Trim()
        });

        await _dbContext.SaveChangesAsync();

        if (conflictCount > 0)
        {
            TempData["WarningMessage"] = $"Day off for {selectedDate:MMM dd, yyyy} scheduled. Warning: {conflictCount} existing booking(s) conflict with this date (not auto-cancelled).";
        }
        else
        {
            TempData["SuccessMessage"] = $"Day off for {selectedDate:MMM dd, yyyy} scheduled successfully.";
        }
        return RedirectToAction(nameof(Availability));
    }

    [HttpPost("availability/days-off/{id:int}/delete")]
    public async Task<IActionResult> DeleteDayOff(int id)
    {
        var barber = await GetCurrentBarberAsync();
        if (barber == null) return Forbid();

        var dayOff = await _dbContext.DaysOff
            .FirstOrDefaultAsync(d => d.Id == id && d.BarberId == barber.Id);

        if (dayOff != null)
        {
            _dbContext.DaysOff.Remove(dayOff);
            await _dbContext.SaveChangesAsync();
            TempData["SuccessMessage"] = "Day off removed.";
        }

        return RedirectToAction(nameof(Availability));
    }

    [HttpGet("transactions")]
    public async Task<IActionResult> Transactions([FromQuery] string? startDate = null, [FromQuery] string? endDate = null)
    {
        var barber = await GetCurrentBarberAsync();
        if (barber == null) return Forbid();

        // BR-07, BR-12, BF-5: Derived strictly from the session, computed by ReportingService
        LocalDate? sDate = null;
        if (!string.IsNullOrWhiteSpace(startDate))
        {
            var parseStart = LocalDatePattern.CreateWithInvariantCulture("yyyy-MM-dd").Parse(startDate);
            if (parseStart.Success) sDate = parseStart.Value;
        }

        LocalDate? eDate = null;
        if (!string.IsNullOrWhiteSpace(endDate))
        {
            var parseEnd = LocalDatePattern.CreateWithInvariantCulture("yyyy-MM-dd").Parse(endDate);
            if (parseEnd.Success) eDate = parseEnd.Value;
        }

        var report = await _reportingService.GetBarberTransactionsAsync(barber.Id, sDate, eDate);

        var avgTicket = report.CompletedCount > 0 ? report.TotalSales / report.CompletedCount : 0m;

        var items = report.Transactions.Select(tx => new BarberTransactionItemViewModel
        {
            AppointmentId = tx.AppointmentId,
            ReferenceCode = tx.ReferenceCode,
            DateFormatted = tx.DateFormatted,
            TimeFormatted = tx.TimeFormatted,
            CustomerName = tx.Customer,
            ServiceName = tx.Service,
            Price = tx.Amount,
            FormattedPrice = tx.FormattedAmount,
            CommissionRate = tx.CommissionRate,
            CommissionAmount = tx.CommissionAmount,
            FormattedCommissionAmount = tx.FormattedCommissionAmount
        }).ToList();

        var model = new BarberTransactionsViewModel
        {
            BarberId = barber.Id,
            BarberName = barber.DisplayName,
            TotalCompletedCount = report.CompletedCount,
            TotalRevenue = report.TotalSales,
            FormattedTotalRevenue = report.FormattedTotalSales,
            CommissionRate = report.CommissionRate,
            TotalCommission = report.TotalCommission,
            FormattedTotalCommission = report.FormattedTotalCommission,
            ShopShare = report.ShopNetShare,
            FormattedShopShare = report.FormattedShopNetShare,
            AverageTicket = avgTicket,
            FormattedAverageTicket = FormattingHelpers.FormatPeso(avgTicket),
            StartDate = startDate,
            EndDate = endDate,
            Transactions = items
        };

        return View(model);
    }

    [HttpGet("/api/barber/transactions")]
    [HttpGet("/api/me/transactions")]
    public async Task<IActionResult> ApiTransactions([FromQuery] string? startDate = null, [FromQuery] string? endDate = null)
    {
        var barber = await GetCurrentBarberAsync();
        if (barber == null) return Forbid();

        LocalDate? sDate = null;
        if (!string.IsNullOrWhiteSpace(startDate))
        {
            var parseStart = LocalDatePattern.CreateWithInvariantCulture("yyyy-MM-dd").Parse(startDate);
            if (parseStart.Success) sDate = parseStart.Value;
        }

        LocalDate? eDate = null;
        if (!string.IsNullOrWhiteSpace(endDate))
        {
            var parseEnd = LocalDatePattern.CreateWithInvariantCulture("yyyy-MM-dd").Parse(endDate);
            if (parseEnd.Success) eDate = parseEnd.Value;
        }

        var report = await _reportingService.GetBarberTransactionsAsync(barber.Id, sDate, eDate);
        return Ok(report);
    }

    [HttpGet("equipment")]
    public async Task<IActionResult> Equipment()
    {
        var barber = await GetCurrentBarberAsync();
        if (barber == null) return Forbid();

        var dbItems = await _equipmentService.GetBarberActiveEquipmentAsync(barber.Id);
        var items = dbItems.Select(e => new EquipmentItem
        {
            Id = e.Id,
            Name = e.Name,
            SerialNumber = e.SerialNo ?? e.AssetTag,
            AssignedToBarber = barber.DisplayName,
            Condition = e.Condition,
            LastInspectionDate = e.LastInspectedAt ?? "None",
            NextInspectionDue = e.NextCheckDue ?? "Not scheduled",
            LastNotes = e.LatestNotes ?? (e.FlagReasons.Any() ? string.Join(", ", e.FlagReasons) : null)
        }).ToList();

        var model = new BarberEquipmentViewModel
        {
            BarberId = barber.Id,
            BarberName = barber.DisplayName,
            Items = items
        };

        return View(model);
    }

    [HttpPost("equipment/{id:int}/inspect")]
    public async Task<IActionResult> InspectEquipment(
        [FromRoute] int id,
        [FromForm] string condition,
        [FromForm] string? notes = null)
    {
        var barber = await GetCurrentBarberAsync();
        if (barber == null) return Forbid();

        if (!Enum.TryParse<EquipmentCondition>(condition, true, out var condEnum))
        {
            condEnum = EquipmentCondition.Good;
        }

        var userId = User.GetUserId();
        var (success, error, _) = await _equipmentService.LogBarberInspectionAsync(id, barber.Id, condEnum, notes, userId);
        if (success)
        {
            TempData["SuccessMessage"] = "Equipment inspection logged successfully.";
        }
        else
        {
            TempData["ErrorMessage"] = error ?? "Could not record inspection for the specified tool.";
        }

        return RedirectToAction(nameof(Equipment));
    }

    [HttpGet("/api/barber/equipment")]
    public async Task<IActionResult> ApiGetMyEquipment()
    {
        var barber = await GetCurrentBarberAsync();
        if (barber == null) return Forbid();

        var items = await _equipmentService.GetBarberActiveEquipmentAsync(barber.Id);
        return Ok(items);
    }

    [HttpPost("/api/barber/equipment/{id:int}/inspect")]
    public async Task<IActionResult> ApiInspectEquipment(
        [FromRoute] int id,
        [FromBody] BarberBook.Web.Models.Inventory.LogEquipmentCheckRequest request)
    {
        var barber = await GetCurrentBarberAsync();
        if (barber == null) return Forbid();

        var userId = User.GetUserId();
        var (success, error, check) = await _equipmentService.LogBarberInspectionAsync(
            id, barber.Id, request.Condition, request.Notes, userId);

        if (!success)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new ApiErrorResponse("FORBIDDEN", error ?? "You cannot inspect this equipment."));
        }

        return Ok(check);
    }

    [HttpGet("account")]
    public async Task<IActionResult> Account()
    {
        var barber = await GetCurrentBarberAsync();
        if (barber == null) return Forbid();

        var model = new BarberAccountViewModel
        {
            BarberId = barber.Id,
            DisplayName = barber.DisplayName,
            Email = barber.User.Email ?? string.Empty,
            PhoneNumber = barber.User.PhoneNumber ?? string.Empty,
            Specialties = barber.Specialties,
            PhotoUrl = barber.PhotoUrl,
            IsActive = barber.IsActive
        };

        return View(model);
    }

    private BarberAppointmentItemViewModel MapToItem(Appointment app)
    {
        var startsAtManila = app.StartsAt.InZone(_zone);
        var endsAtManila = app.EndsAt.InZone(_zone);

        return new BarberAppointmentItemViewModel
        {
            Id = app.Id,
            ReferenceCode = app.ReferenceCode,
            CustomerName = app.Customer.FullName,
            CustomerPhone = app.Customer.PhoneNumber ?? "No phone provided",
            CustomerEmail = app.Customer.Email ?? "No email provided",
            ServiceName = app.ServiceName,
            Price = app.Price,
            FormattedPrice = FormattingHelpers.FormatPeso(app.Price),
            StartsAt = app.StartsAt,
            EndsAt = app.EndsAt,
            ScheduledDateFormatted = startsAtManila.Date.ToString("MMMM dd, yyyy", System.Globalization.CultureInfo.InvariantCulture),
            ScheduledDateString = startsAtManila.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            ScheduledTime12h = $"{startsAtManila.TimeOfDay.ToString("hh:mm tt", System.Globalization.CultureInfo.InvariantCulture)} – {endsAtManila.TimeOfDay.ToString("hh:mm tt", System.Globalization.CultureInfo.InvariantCulture)}",
            Status = app.Status,
            Notes = app.Notes,
            BranchName = app.Branch?.Name ?? app.Barber?.Branch?.Name,
            BranchAddress = app.Branch?.Address ?? app.Barber?.Branch?.Address,
            BranchPhone = app.Branch?.PhoneNumber ?? app.Barber?.Branch?.PhoneNumber,
            StatusHistories = app.StatusHistories
                .OrderBy(sh => sh.ChangedAt)
                .Select(sh => new BarberStatusHistoryItemViewModel
                {
                    FromStatus = sh.FromStatus?.ToString().ToLowerInvariant() ?? "(initial)",
                    ToStatus = sh.ToStatus.ToString().ToLowerInvariant(),
                    ChangedByRole = sh.ChangedByRole,
                    Note = sh.Note,
                    TimestampFormatted = sh.ChangedAt.InZone(_zone).ToString("MMM dd, yyyy hh:mm tt", System.Globalization.CultureInfo.InvariantCulture)
                }).ToList()
        };
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
