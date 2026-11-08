using System.Security.Claims;
using BarberBook.Data;
using BarberBook.Data.Enums;
using BarberBook.Web.Common;
using BarberBook.Web.Models.Appointment;
using BarberBook.Web.Models.Customer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace BarberBook.Web.Controllers;

[Route("book")]
public class BookingController : Controller
{
    private readonly BarberBookDbContext _dbContext;
    private readonly IClock _clock;
    private readonly DateTimeZone _zone;

    public BookingController(
        BarberBookDbContext dbContext,
        IClock clock,
        DateTimeZone? zone = null)
    {
        _dbContext = dbContext;
        _clock = clock;
        _zone = zone ?? DateTimeZoneProviders.Tzdb["Asia/Manila"];
    }

    [Authorize(Roles = "Customer")]
    [HttpGet("")]
    public async Task<IActionResult> Index(
        [FromQuery] int? serviceId,
        [FromQuery] int? barberId,
        [FromQuery] int? branchId,
        [FromQuery] int? service,
        [FromQuery] int? barber,
        [FromQuery] int? branch)
    {
        var effServiceId = serviceId ?? service;
        var effBarberId = barberId ?? barber;
        var effBranchId = branchId ?? branch;

        var branches = await _dbContext.Branches
            .Where(b => b.IsActive)
            .OrderBy(b => b.Name)
            .Select(b => new BarberBook.Web.Models.Admin.BranchOptionDto
            {
                Id = b.Id,
                Name = b.Name
            })
            .ToListAsync();

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
            .ToListAsync();

        var barbers = await _dbContext.Barbers
            .Include(b => b.User)
            .Include(b => b.Branch)
            .Include(b => b.WorkingHours)
            .Where(b => b.IsActive && b.User.IsActive)
            .OrderBy(b => b.DisplayName)
            .ToListAsync();

        var barberDtos = barbers.Select(b => new PublicBarberDto
        {
            Id = b.Id,
            DisplayName = b.DisplayName,
            Bio = b.Bio,
            PhotoUrl = b.PhotoUrl,
            Specialties = b.Specialties,
            BranchId = b.BranchId,
            BranchName = b.Branch != null ? b.Branch.Name : null,
            WeeklyScheduleText = FormattingHelpers.FormatWeeklySchedule(b.WorkingHours)
        }).ToList();

        var today = _clock.GetCurrentInstant().InZone(_zone).Date;

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        string custName = string.Empty;
        string custEmail = string.Empty;
        string custPhone = string.Empty;

        if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out var custId))
        {
            var user = await _dbContext.Users.FindAsync(custId);
            if (user != null)
            {
                custName = user.FullName;
                custEmail = user.Email ?? string.Empty;
                custPhone = user.PhoneNumber ?? string.Empty;
            }
        }

        var model = new BookingWizardViewModel
        {
            Branches = branches,
            Services = services,
            Barbers = barberDtos,
            PreSelectedBranchId = effBranchId,
            PreSelectedServiceId = effServiceId,
            PreSelectedBarberId = effBarberId,
            TodayDateString = today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            CustomerName = custName,
            CustomerEmail = custEmail,
            CustomerPhone = custPhone
        };

        return View(model);
    }

    [Authorize(Roles = "Customer")]
    [HttpGet("confirmation/{referenceCode?}")]
    [HttpGet("confirmation")]
    public async Task<IActionResult> Confirmation([FromRoute] string? referenceCode = null, [FromQuery(Name = "referenceCode")] string? queryRefCode = null)
    {
        var code = !string.IsNullOrWhiteSpace(referenceCode) ? referenceCode : queryRefCode;
        if (string.IsNullOrWhiteSpace(code))
        {
            return RedirectToAction("MyAppointments", "Home");
        }

        var appointment = await _dbContext.Appointments
            .Include(a => a.Customer)
            .Include(a => a.Barber).ThenInclude(b => b.User)
            .Include(a => a.Barber).ThenInclude(b => b.Branch)
            .Include(a => a.Branch)
            .Include(a => a.Service)
            .FirstOrDefaultAsync(a => a.ReferenceCode == code);

        if (appointment == null)
        {
            return NotFound();
        }

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var currentUserId))
        {
            return Unauthorized();
        }

        bool isAdmin = User.IsInRole("Admin");
        bool isAssignedBarber = User.IsInRole("Barber") && appointment.Barber.UserId == currentUserId;
        bool isOwnerCustomer = User.IsInRole("Customer") && appointment.CustomerId == currentUserId;

        if (!isAdmin && !isAssignedBarber && !isOwnerCustomer)
        {
            return Forbid();
        }

        var startsAtManila = appointment.StartsAt.InZone(_zone);
        var endsAtManila = appointment.EndsAt.InZone(_zone);

        var dto = new AppointmentDetailDto
        {
            Id = appointment.Id,
            ReferenceCode = appointment.ReferenceCode,
            CustomerId = appointment.CustomerId,
            CustomerName = appointment.Customer.FullName,
            CustomerPhone = appointment.Customer.PhoneNumber ?? string.Empty,
            CustomerEmail = appointment.Customer.Email ?? string.Empty,
            BranchId = appointment.BranchId ?? appointment.Barber?.BranchId,
            BranchName = appointment.Branch?.Name ?? appointment.Barber?.Branch?.Name,
            BranchAddress = appointment.Branch?.Address ?? appointment.Barber?.Branch?.Address,
            BarberId = appointment.BarberId,
            BarberName = appointment.Barber?.DisplayName ?? string.Empty,
            ServiceId = appointment.ServiceId,
            ServiceName = appointment.ServiceName,
            Price = appointment.Price,
            FormattedPrice = FormattingHelpers.FormatPeso(appointment.Price),
            StartsAt = appointment.StartsAt.ToString(),
            EndsAt = appointment.EndsAt.ToString(),
            DurationMinutes = (int)(appointment.EndsAt - appointment.StartsAt).TotalMinutes,
            FormattedDate = FormattingHelpers.FormatDate(appointment.StartsAt),
            FormattedTime = $"{FormattingHelpers.FormatTime12h(startsAtManila.TimeOfDay)} – {FormattingHelpers.FormatTime12h(endsAtManila.TimeOfDay)}",
            Status = appointment.Status.ToString().ToLowerInvariant(),
            StatusDisplay = "Pending — awaiting confirmation by the shop",
            Notes = appointment.Notes,
            CreatedAt = appointment.CreatedAt.ToString()
        };

        return View(dto);
    }
}

