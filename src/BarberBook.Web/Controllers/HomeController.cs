using BarberBook.Data;
using BarberBook.Data.Entities;
using BarberBook.Web.Common;
using BarberBook.Web.Models.Customer;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace BarberBook.Web.Controllers;

public class HomeController : Controller
{
    private readonly BarberBookDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IValidator<UpdateProfileRequest> _profileValidator;
    private readonly IClock _clock;
    private readonly DateTimeZone _zone;

    public HomeController(
        BarberBookDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IValidator<UpdateProfileRequest> profileValidator,
        IClock clock,
        DateTimeZone? zone = null)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _profileValidator = profileValidator;
        _clock = clock;
        _zone = zone ?? DateTimeZoneProviders.Tzdb["Asia/Manila"];
    }

    public async Task<IActionResult> Index()
    {
        var highlights = await _dbContext.Services
            .Where(s => s.IsActive)
            .OrderBy(s => s.Price)
            .Take(4)
            .ToListAsync();

        return View(highlights);
    }

    [HttpGet("services")]
    [HttpGet("api/services")]
    public async Task<IActionResult> Services()
    {
        var services = await _dbContext.Services
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .ToListAsync();

        if (IsJsonRequest())
        {
            var dtoList = services.Select(s => new PublicServiceDto
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description,
                Price = s.Price,
                FormattedPrice = FormattingHelpers.FormatPeso(s.Price),
                DurationMinutes = s.DurationMinutes
            }).ToList();

            return Ok(dtoList);
        }

        return View(services);
    }

    [HttpGet("barbers")]
    public async Task<IActionResult> Barbers()
    {
        var barbers = await _dbContext.Barbers
            .Include(b => b.User)
            .Include(b => b.WorkingHours)
            .Where(b => b.IsActive && b.User.IsActive)
            .OrderBy(b => b.DisplayName)
            .ToListAsync();

        return View(barbers);
    }

    [HttpGet("barbers/{id:int}")]
    public async Task<IActionResult> BarberProfile(int id)
    {
        var barber = await _dbContext.Barbers
            .Include(b => b.User)
            .Include(b => b.WorkingHours)
            .FirstOrDefaultAsync(b => b.Id == id && b.IsActive && b.User.IsActive);

        if (barber == null)
        {
            return NotFound();
        }

        var today = _clock.GetCurrentInstant().InZone(_zone).Date;
        var upcomingDaysOff = await _dbContext.DaysOff
            .Where(d => d.BarberId == id && d.Date >= today)
            .OrderBy(d => d.Date)
            .Take(14)
            .ToListAsync();

        ViewData["UpcomingDaysOff"] = upcomingDaysOff;
        return View(barber);
    }

    [Authorize(Roles = "Customer")]
    [HttpGet("profile")]
    public async Task<IActionResult> Profile()
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Challenge();

        var user = await _userManager.FindByIdAsync(userId.Value.ToString());
        if (user == null || !user.IsActive) return Forbid();

        var model = new UpdateProfileRequest
        {
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber
        };

        return View(model);
    }

    [Authorize(Roles = "Customer")]
    [HttpPost("profile")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile([FromForm] UpdateProfileRequest model)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Challenge();

        var user = await _userManager.FindByIdAsync(userId.Value.ToString());
        if (user == null || !user.IsActive) return Forbid();

        var validationResult = await _profileValidator.ValidateAsync(model);
        if (!validationResult.IsValid)
        {
            foreach (var error in validationResult.Errors)
            {
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            }
            return View("Profile", model);
        }

        var cleanEmail = model.Email.Trim().ToLowerInvariant();
        if (!string.Equals(user.Email, cleanEmail, StringComparison.OrdinalIgnoreCase))
        {
            var existingUserWithEmail = await _userManager.FindByEmailAsync(cleanEmail);
            if (existingUserWithEmail != null && existingUserWithEmail.Id != user.Id)
            {
                ModelState.AddModelError("Email", $"The email address '{cleanEmail}' is already registered.");
                return View("Profile", model);
            }

            user.Email = cleanEmail;
            user.UserName = cleanEmail;
        }

        user.FullName = model.FullName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim();

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var err in result.Errors)
            {
                ModelState.AddModelError(string.Empty, err.Description);
            }
            return View("Profile", model);
        }

        TempData["ProfileSuccess"] = "Your profile has been updated successfully.";
        return RedirectToAction(nameof(Profile));
    }

    [Authorize(Roles = "Customer")]
    [HttpGet("book")]
    public IActionResult Book()
    {
        return View();
    }

    [Authorize(Roles = "Customer")]
    [HttpGet("my-appointments")]
    public IActionResult MyAppointments()
    {
        return View();
    }

    [Route("Home/StatusCodePage")]
    public IActionResult StatusCodePage(int? code)
    {
        var statusCode = code ?? Response.StatusCode;
        if (statusCode == 403)
        {
            return View("Forbidden");
        }
        if (statusCode == 404)
        {
            return View("NotFound");
        }

        ViewData["StatusCode"] = statusCode;
        return View("Error");
    }

    private bool IsJsonRequest()
    {
        return Request.Path.StartsWithSegments("/api") ||
               Request.Headers.Accept.ToString().Contains("application/json");
    }
}
