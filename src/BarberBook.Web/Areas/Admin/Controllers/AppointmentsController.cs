using BarberBook.Data;
using BarberBook.Web.Common;
using BarberBook.Web.Models.Admin;
using BarberBook.Web.Models.Customer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace BarberBook.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class AppointmentsController : Controller
{
    private readonly BarberBookDbContext _dbContext;
    private readonly IClock _clock;
    private readonly DateTimeZone _zone;

    public AppointmentsController(
        BarberBookDbContext dbContext,
        IClock clock,
        DateTimeZone? zone = null)
    {
        _dbContext = dbContext;
        _clock = clock;
        _zone = zone ?? DateTimeZoneProviders.Tzdb["Asia/Manila"];
    }

    [HttpGet("admin/appointments")]
    public async Task<IActionResult> Index(
        [FromQuery] string? view,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var services = await _dbContext.Services
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new PublicServiceDto
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description,
                Price = s.Price,
                FormattedPrice = FormattingHelpers.FormatPeso(s.Price),
                DurationMinutes = s.DurationMinutes
            })
            .ToListAsync(cancellationToken);

        var barbers = await _dbContext.Barbers
            .Include(b => b.User)
            .Where(b => b.IsActive && b.User.IsActive)
            .OrderBy(b => b.DisplayName)
            .Select(b => new PublicBarberDto
            {
                Id = b.Id,
                DisplayName = b.DisplayName,
                Bio = b.Bio,
                PhotoUrl = b.PhotoUrl,
                Specialties = b.Specialties
            })
            .ToListAsync(cancellationToken);

        var todayInManila = _clock.GetCurrentInstant().InZone(_zone).Date;
        var todayStr = todayInManila.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

        var model = new AdminAppointmentsViewModel
        {
            Services = services,
            Barbers = barbers,
            TodayDateString = todayStr,
            InitialView = string.Equals(view, "day", StringComparison.OrdinalIgnoreCase) ? "day" :
                          string.Equals(view, "week", StringComparison.OrdinalIgnoreCase) ? "week" : "list",
            InitialStatus = status?.ToLowerInvariant()
        };

        return View(model);
    }

    [HttpGet("admin/history")]
    [HttpGet("admin/appointments/history")]
    public async Task<IActionResult> History(CancellationToken cancellationToken)
    {
        var services = await _dbContext.Services
            .OrderBy(s => s.Name)
            .Select(s => new PublicServiceDto
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description,
                Price = s.Price,
                FormattedPrice = FormattingHelpers.FormatPeso(s.Price),
                DurationMinutes = s.DurationMinutes
            })
            .ToListAsync(cancellationToken);

        var barbers = await _dbContext.Barbers
            .OrderBy(b => b.DisplayName)
            .Select(b => new PublicBarberDto
            {
                Id = b.Id,
                DisplayName = b.DisplayName,
                Bio = b.Bio,
                PhotoUrl = b.PhotoUrl,
                Specialties = b.Specialties
            })
            .ToListAsync(cancellationToken);

        var todayInManila = _clock.GetCurrentInstant().InZone(_zone).Date;
        var todayStr = todayInManila.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

        var model = new AdminAppointmentsViewModel
        {
            Services = services,
            Barbers = barbers,
            TodayDateString = todayStr,
            InitialView = "list"
        };

        return View(model);
    }
}
