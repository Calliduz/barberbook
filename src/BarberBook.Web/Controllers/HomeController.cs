using BarberBook.Data;
using BarberBook.Data.Entities;
using BarberBook.Data.Extensions;
using BarberBook.Web.Common;
using BarberBook.Web.Models.Customer;
using BarberBook.Web.Services;
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
    private readonly IInventoryService _inventoryService;
    private readonly IClock _clock;
    private readonly DateTimeZone _zone;

    public HomeController(
        BarberBookDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IValidator<UpdateProfileRequest> profileValidator,
        IInventoryService inventoryService,
        IClock clock,
        DateTimeZone? zone = null)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _profileValidator = profileValidator;
        _inventoryService = inventoryService;
        _clock = clock;
        _zone = zone ?? DateTimeZoneProviders.Tzdb["Asia/Manila"];
    }

    public async Task<IActionResult> Index()
    {
        var highlights = await _dbContext.Services
            .Where(s => s.IsActive)
            .OrderBy(s => s.Price)
            .Take(6)
            .ToListAsync();

        var activeBarbers = await _dbContext.Barbers
            .Where(b => b.IsActive)
            .OrderBy(b => b.DisplayName)
            .ToListAsync();

        var userIds = activeBarbers.Select(b => b.UserId).Distinct().ToList();
        var users = await _dbContext.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id);

        var branchIds = activeBarbers.Where(b => b.BranchId.HasValue).Select(b => b.BranchId!.Value).Distinct().ToList();
        var branchesDict = await _dbContext.Branches.Where(b => branchIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id);

        var barbers = activeBarbers
            .Where(b => users.TryGetValue(b.UserId, out var u) && u.IsActive)
            .Select(b =>
            {
                if (users.TryGetValue(b.UserId, out var u)) b.User = u;
                if (b.BranchId.HasValue && branchesDict.TryGetValue(b.BranchId.Value, out var br)) b.Branch = br;
                return b;
            })
            .ToList();

        var branches = await _dbContext.Branches
            .Where(b => b.IsActive)
            .OrderBy(b => b.Name)
            .ToListAsync();

        var products = await _inventoryService.GetProductsAsync(includeInactive: false);
        products = products.OrderBy(p => p.Id).ToList();

        ViewBag.Barbers = barbers;
        ViewBag.Branches = branches;
        ViewBag.Products = products;
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
        var activeUserIds = await _dbContext.Users.AsNoTracking().Where(u => u.IsActive).Select(u => u.Id).ToListAsync();
        var barbers = await _dbContext.Barbers
            .AsNoTracking()
            .Where(b => b.IsActive && activeUserIds.Contains(b.UserId))
            .OrderBy(b => b.DisplayName)
            .ToListAsync();

        await _dbContext.HydrateBarbersAsync(barbers, includeUser: true, includeBranch: true, includeWorkingHours: true);

        return View(barbers);
    }

    [HttpGet("barbers/{id:int}")]
    public async Task<IActionResult> BarberProfile(int id)
    {
        var barber = await _dbContext.Barbers
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id && b.IsActive);

        if (barber == null)
        {
            return NotFound();
        }

        await _dbContext.HydrateBarbersAsync(new[] { barber }, includeUser: true, includeBranch: true, includeWorkingHours: true);

        if (barber.User == null || !barber.User.IsActive)
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

    [HttpGet("branches")]
    [HttpGet("api/branches")]
    public async Task<IActionResult> Branches()
    {
        var branches = await _dbContext.Branches
            .AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.Name)
            .ToListAsync();

        var activeBarbers = await _dbContext.Barbers
            .AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.DisplayName)
            .ToListAsync();
        await _dbContext.HydrateBarbersAsync(activeBarbers, includeUser: true, includeBranch: false, includeWorkingHours: true);

        foreach (var br in branches)
        {
            br.Barbers = activeBarbers.Where(b => b.BranchId == br.Id).ToList();
        }

        if (IsJsonRequest())
        {
            return Ok(branches.Select(b => new
            {
                b.Id,
                b.Name,
                b.Address,
                b.PhoneNumber,
                OpeningTime = b.OpeningTime.ToString("h:mm tt", null),
                ClosingTime = b.ClosingTime.ToString("h:mm tt", null),
                b.IsActive,
                Barbers = b.Barbers.Select(barber => new
                {
                    barber.Id,
                    barber.DisplayName,
                    barber.Specialties,
                    barber.PhotoUrl
                })
            }));
        }

        return View(branches);
    }

    [HttpGet("products")]
    [HttpGet("api/products")]
    public async Task<IActionResult> Products([FromQuery] string? category = null, [FromQuery] string? search = null)
    {
        var products = await _inventoryService.GetProductsAsync(search: search, category: category, includeInactive: false);

        if (IsJsonRequest())
        {
            return Ok(products);
        }

        var categories = (await _dbContext.Products
            .Where(p => p.IsActive)
            .Select(p => p.Category)
            .ToListAsync())
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        ViewBag.Categories = categories;
        ViewBag.SelectedCategory = category;
        ViewBag.SearchTerm = search;
        return View(products);
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
        var accept = Request.Headers.Accept.ToString();
        if (accept.Contains("text/html"))
        {
            return false;
        }

        var path = Request.Path.Value ?? string.Empty;
        var contentType = Request.ContentType ?? string.Empty;

        return path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) ||
               path.Equals("/api", StringComparison.OrdinalIgnoreCase) ||
               accept.Contains("application/json") ||
               contentType.Contains("application/json");
    }
}
