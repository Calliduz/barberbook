using BarberBook.Data;
using BarberBook.Data.Entities;
using BarberBook.Data.Enums;
using BarberBook.Web.Common;
using BarberBook.Web.Models.Admin;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using BarberEntity = BarberBook.Data.Entities.Barber;

namespace BarberBook.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class BarbersController : Controller
{
    private readonly BarberBookDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IClock _clock;
    private readonly IValidator<CreateBarberRequest> _createValidator;
    private readonly IValidator<UpdateBarberRequest> _updateValidator;
    private readonly IValidator<ResetBarberPasswordRequest> _resetPasswordValidator;

    public BarbersController(
        BarberBookDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IClock clock,
        IValidator<CreateBarberRequest> createValidator,
        IValidator<UpdateBarberRequest> updateValidator,
        IValidator<ResetBarberPasswordRequest> resetPasswordValidator)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _clock = clock;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _resetPasswordValidator = resetPasswordValidator;
    }

    [HttpGet("admin/barbers")]
    [HttpGet("api/admin/barbers")]
    public async Task<IActionResult> Index(
        [FromQuery] string? q = null,
        [FromQuery] string status = "all",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 50) pageSize = 10;

        var query = _dbContext.Barbers
            .Include(b => b.User)
            .Include(b => b.Appointments)
            .AsQueryable();

        // Status filter
        if (status.Equals("active", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(b => b.IsActive && b.User.IsActive);
        }
        else if (status.Equals("inactive", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(b => !b.IsActive || !b.User.IsActive);
        }

        // Search query
        if (!string.IsNullOrWhiteSpace(q))
        {
            var search = q.Trim().ToLower();
            query = query.Where(b =>
                b.DisplayName.ToLower().Contains(search) ||
                b.User.FullName.ToLower().Contains(search) ||
                (b.User.Email != null && b.User.Email.ToLower().Contains(search)) ||
                (b.Specialties != null && b.Specialties.ToLower().Contains(search)));
        }

        var totalCount = await query.CountAsync();

        var barbers = await query
            .OrderBy(b => b.DisplayName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new AdminBarberItemDto
            {
                Id = b.Id,
                UserId = b.UserId,
                FullName = b.User.FullName,
                Email = b.User.Email ?? string.Empty,
                DisplayName = b.DisplayName,
                Bio = b.Bio,
                PhotoUrl = b.PhotoUrl,
                Specialties = b.Specialties,
                IsActive = b.IsActive && b.User.IsActive,
                CompletedAppointmentsCount = b.Appointments.Count(a => a.Status == AppointmentStatus.Completed)
            })
            .ToListAsync();

        var viewModel = new BarberListViewModel
        {
            Items = barbers,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            SearchQuery = q,
            StatusFilter = status
        };

        if (IsJsonRequest())
        {
            return Ok(viewModel);
        }

        return View(viewModel);
    }

    [HttpGet("admin/barbers/{id:int}")]
    [HttpGet("api/admin/barbers/{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        var barber = await _dbContext.Barbers
            .Include(b => b.User)
            .Include(b => b.Appointments)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (barber == null)
        {
            return IsJsonRequest()
                ? NotFound(new ApiErrorResponse("NOT_FOUND", $"Barber with ID {id} not found."))
                : NotFound();
        }

        var dto = new AdminBarberItemDto
        {
            Id = barber.Id,
            UserId = barber.UserId,
            FullName = barber.User.FullName,
            Email = barber.User.Email ?? string.Empty,
            DisplayName = barber.DisplayName,
            Bio = barber.Bio,
            PhotoUrl = barber.PhotoUrl,
            Specialties = barber.Specialties,
            IsActive = barber.IsActive && barber.User.IsActive,
            CompletedAppointmentsCount = barber.Appointments.Count(a => a.Status == AppointmentStatus.Completed)
        };

        if (IsJsonRequest())
        {
            return Ok(dto);
        }

        return Ok(dto);
    }

    [HttpPost("admin/barbers")]
    [HttpPost("api/admin/barbers")]
    public async Task<IActionResult> Create([FromBody] CreateBarberRequest? jsonRequest, [FromForm] CreateBarberRequest? formRequest)
    {
        var request = jsonRequest ?? formRequest ?? new CreateBarberRequest();

        // 1. FluentValidation
        var validationResult = await _createValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.ToDictionary(e => e.PropertyName, e => e.ErrorMessage);
            if (IsJsonRequest())
            {
                return BadRequest(new ApiErrorResponse("VALIDATION_ERROR", "Validation failed.", errors));
            }

            TempData["ErrorMessage"] = string.Join(" ", validationResult.Errors.Select(e => e.ErrorMessage));
            return RedirectToAction(nameof(Index));
        }

        // 2. Duplicate Email Check
        var cleanEmail = request.Email.Trim().ToLowerInvariant();
        var existingUser = await _userManager.FindByEmailAsync(cleanEmail);
        if (existingUser != null)
        {
            if (IsJsonRequest())
            {
                return BadRequest(new ApiErrorResponse("DUPLICATE_EMAIL", $"An account with email '{cleanEmail}' already exists."));
            }

            TempData["ErrorMessage"] = $"An account with email '{cleanEmail}' already exists.";
            return RedirectToAction(nameof(Index));
        }

        // 3. Create ApplicationUser
        var user = new ApplicationUser
        {
            UserName = cleanEmail,
            Email = cleanEmail,
            FullName = request.FullName.Trim(),
            Role = UserRole.Barber,
            IsActive = true,
            CreatedAt = _clock.GetCurrentInstant()
        };

        var identityResult = await _userManager.CreateAsync(user, request.Password);
        if (!identityResult.Succeeded)
        {
            var errorMsg = string.Join(" ", identityResult.Errors.Select(e => e.Description));
            if (IsJsonRequest())
            {
                return BadRequest(new ApiErrorResponse("IDENTITY_ERROR", errorMsg));
            }

            TempData["ErrorMessage"] = errorMsg;
            return RedirectToAction(nameof(Index));
        }

        await _userManager.AddToRoleAsync(user, "Barber");

        // 4. Create Barber Profile
        var barber = new BarberEntity
        {
            UserId = user.Id,
            DisplayName = request.DisplayName.Trim(),
            Bio = string.IsNullOrWhiteSpace(request.Bio) ? null : request.Bio.Trim(),
            PhotoUrl = string.IsNullOrWhiteSpace(request.PhotoUrl) ? null : request.PhotoUrl.Trim(),
            Specialties = string.IsNullOrWhiteSpace(request.Specialties) ? null : request.Specialties.Trim(),
            IsActive = true
        };

        _dbContext.Barbers.Add(barber);
        await _dbContext.SaveChangesAsync();

        var resultDto = new AdminBarberItemDto
        {
            Id = barber.Id,
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            DisplayName = barber.DisplayName,
            Bio = barber.Bio,
            PhotoUrl = barber.PhotoUrl,
            Specialties = barber.Specialties,
            IsActive = true,
            CompletedAppointmentsCount = 0
        };

        if (IsJsonRequest())
        {
            return CreatedAtAction(nameof(Details), new { id = barber.Id }, resultDto);
        }

        TempData["SuccessMessage"] = $"Barber account for {barber.DisplayName} created successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("admin/barbers/{id:int}/edit")]
    [HttpPut("api/admin/barbers/{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateBarberRequest? jsonRequest, [FromForm] UpdateBarberRequest? formRequest)
    {
        var request = jsonRequest ?? formRequest ?? new UpdateBarberRequest();

        // 1. FluentValidation
        var validationResult = await _updateValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.ToDictionary(e => e.PropertyName, e => e.ErrorMessage);
            if (IsJsonRequest())
            {
                return BadRequest(new ApiErrorResponse("VALIDATION_ERROR", "Validation failed.", errors));
            }

            TempData["ErrorMessage"] = string.Join(" ", validationResult.Errors.Select(e => e.ErrorMessage));
            return RedirectToAction(nameof(Index));
        }

        // 2. Find barber
        var barber = await _dbContext.Barbers
            .Include(b => b.User)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (barber == null)
        {
            return IsJsonRequest()
                ? NotFound(new ApiErrorResponse("NOT_FOUND", $"Barber with ID {id} not found."))
                : NotFound();
        }

        // 3. Update barber & user details
        barber.DisplayName = request.DisplayName.Trim();
        barber.Bio = string.IsNullOrWhiteSpace(request.Bio) ? null : request.Bio.Trim();
        barber.PhotoUrl = string.IsNullOrWhiteSpace(request.PhotoUrl) ? null : request.PhotoUrl.Trim();
        barber.Specialties = string.IsNullOrWhiteSpace(request.Specialties) ? null : request.Specialties.Trim();

        barber.User.FullName = request.FullName.Trim();

        await _dbContext.SaveChangesAsync();

        var resultDto = new AdminBarberItemDto
        {
            Id = barber.Id,
            UserId = barber.UserId,
            FullName = barber.User.FullName,
            Email = barber.User.Email ?? string.Empty,
            DisplayName = barber.DisplayName,
            Bio = barber.Bio,
            PhotoUrl = barber.PhotoUrl,
            Specialties = barber.Specialties,
            IsActive = barber.IsActive && barber.User.IsActive
        };

        if (IsJsonRequest())
        {
            return Ok(resultDto);
        }

        TempData["SuccessMessage"] = $"Barber profile for {barber.DisplayName} updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("admin/barbers/{id:int}/reset-password")]
    [HttpPost("api/admin/barbers/{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetBarberPasswordRequest? jsonRequest, [FromForm] ResetBarberPasswordRequest? formRequest)
    {
        var request = jsonRequest ?? formRequest ?? new ResetBarberPasswordRequest();

        var validationResult = await _resetPasswordValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.ToDictionary(e => e.PropertyName, e => e.ErrorMessage);
            if (IsJsonRequest())
            {
                return BadRequest(new ApiErrorResponse("VALIDATION_ERROR", "Validation failed.", errors));
            }

            TempData["ErrorMessage"] = string.Join(" ", validationResult.Errors.Select(e => e.ErrorMessage));
            return RedirectToAction(nameof(Index));
        }

        var barber = await _dbContext.Barbers
            .Include(b => b.User)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (barber == null)
        {
            return IsJsonRequest()
                ? NotFound(new ApiErrorResponse("NOT_FOUND", $"Barber with ID {id} not found."))
                : NotFound();
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(barber.User);
        var result = await _userManager.ResetPasswordAsync(barber.User, token, request.NewPassword);

        if (!result.Succeeded)
        {
            var errorMsg = string.Join(" ", result.Errors.Select(e => e.Description));
            if (IsJsonRequest())
            {
                return BadRequest(new ApiErrorResponse("PASSWORD_RESET_FAILED", errorMsg));
            }

            TempData["ErrorMessage"] = errorMsg;
            return RedirectToAction(nameof(Index));
        }

        if (IsJsonRequest())
        {
            return Ok(new { success = true, message = $"Password for {barber.DisplayName} reset successfully." });
        }

        TempData["SuccessMessage"] = $"Password for {barber.DisplayName} reset successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("admin/barbers/{id:int}/toggle-status")]
    [HttpPost("api/admin/barbers/{id:int}/toggle-status")]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var barber = await _dbContext.Barbers
            .Include(b => b.User)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (barber == null)
        {
            return IsJsonRequest()
                ? NotFound(new ApiErrorResponse("NOT_FOUND", $"Barber with ID {id} not found."))
                : NotFound();
        }

        // Toggle both barber profile and user account
        var newStatus = !barber.IsActive;
        barber.IsActive = newStatus;
        barber.User.IsActive = newStatus;

        await _dbContext.SaveChangesAsync();

        var statusText = newStatus ? "activated" : "deactivated";

        if (IsJsonRequest())
        {
            return Ok(new { barberId = barber.Id, isActive = barber.IsActive, message = $"Barber {barber.DisplayName} has been {statusText}." });
        }

        TempData["SuccessMessage"] = $"Barber {barber.DisplayName} has been {statusText}.";
        return RedirectToAction(nameof(Index));
    }

    private bool IsJsonRequest()
    {
        return Request.Path.StartsWithSegments("/api") ||
               Request.Headers.Accept.ToString().Contains("application/json");
    }
}
