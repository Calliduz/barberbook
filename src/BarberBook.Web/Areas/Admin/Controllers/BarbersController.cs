using BarberBook.Data;
using BarberBook.Data.Entities;
using BarberBook.Data.Enums;
using BarberBook.Data.Extensions;
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
    private readonly IWebHostEnvironment _env;
    private readonly IValidator<CreateBarberRequest> _createValidator;
    private readonly IValidator<UpdateBarberRequest> _updateValidator;
    private readonly IValidator<ResetBarberPasswordRequest> _resetPasswordValidator;

    public BarbersController(
        BarberBookDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IClock clock,
        IWebHostEnvironment env,
        IValidator<CreateBarberRequest> createValidator,
        IValidator<UpdateBarberRequest> updateValidator,
        IValidator<ResetBarberPasswordRequest> resetPasswordValidator)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _clock = clock;
        _env = env;
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
            .AsNoTracking()
            .AsQueryable();

        // Status filter
        if (status.Equals("active", StringComparison.OrdinalIgnoreCase))
        {
            var activeUserIds = await _dbContext.Users.AsNoTracking().Where(u => u.IsActive).Select(u => u.Id).ToListAsync();
            query = query.Where(b => b.IsActive && activeUserIds.Contains(b.UserId));
        }
        else if (status.Equals("inactive", StringComparison.OrdinalIgnoreCase))
        {
            var inactiveUserIds = await _dbContext.Users.AsNoTracking().Where(u => !u.IsActive).Select(u => u.Id).ToListAsync();
            query = query.Where(b => !b.IsActive || inactiveUserIds.Contains(b.UserId));
        }

        // Search query
        if (!string.IsNullOrWhiteSpace(q))
        {
            var search = q.Trim().ToLower();
            var matchingUserIds = await _dbContext.Users.AsNoTracking()
                .Where(u => u.FullName.ToLower().Contains(search) || (u.Email != null && u.Email.ToLower().Contains(search)))
                .Select(u => u.Id)
                .ToListAsync();

            query = query.Where(b =>
                b.DisplayName.ToLower().Contains(search) ||
                matchingUserIds.Contains(b.UserId) ||
                (b.Specialties != null && b.Specialties.ToLower().Contains(search)));
        }

        var totalCount = await query.CountAsync();

        var branches = await _dbContext.Branches
            .AsNoTracking()
            .Where(br => br.IsActive)
            .OrderBy(br => br.Name)
            .Select(br => new BranchOptionDto { Id = br.Id, Name = br.Name })
            .ToListAsync();

        var rawBarbers = await query
            .OrderBy(b => b.DisplayName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        await _dbContext.HydrateBarbersAsync(rawBarbers, includeUser: true, includeBranch: true);

        var barberIds = rawBarbers.Select(b => b.Id).ToList();
        var completedCounts = (await _dbContext.Appointments
            .AsNoTracking()
            .Where(a => barberIds.Contains(a.BarberId) && a.Status == AppointmentStatus.Completed)
            .Select(a => a.BarberId)
            .ToListAsync())
            .GroupBy(id => id)
            .ToDictionary(g => g.Key, g => g.Count());

        var barbers = rawBarbers.Select(b => new AdminBarberItemDto
        {
            Id = b.Id,
            UserId = b.UserId,
            FullName = b.User?.FullName ?? string.Empty,
            Email = b.User?.Email ?? string.Empty,
            DisplayName = b.DisplayName,
            Bio = b.Bio,
            PhotoUrl = b.PhotoUrl,
            Specialties = b.Specialties,
            BranchId = b.BranchId,
            BranchName = b.Branch != null ? b.Branch.Name : null,
            CommissionRate = b.CommissionRate,
            IsActive = b.IsActive && (b.User?.IsActive ?? false),
            CompletedAppointmentsCount = completedCounts.TryGetValue(b.Id, out var cnt) ? cnt : 0
        }).ToList();

        var viewModel = new BarberListViewModel
        {
            Items = barbers,
            Branches = branches,
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
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id);

        if (barber == null)
        {
            return IsJsonRequest()
                ? NotFound(new ApiErrorResponse("NOT_FOUND", $"Barber with ID {id} not found."))
                : NotFound();
        }

        await _dbContext.HydrateBarbersAsync(new[] { barber }, includeUser: true, includeBranch: true);

        var completedCount = await _dbContext.Appointments
            .CountAsync(a => a.BarberId == id && a.Status == AppointmentStatus.Completed);

        var dto = new AdminBarberItemDto
        {
            Id = barber.Id,
            UserId = barber.UserId,
            FullName = barber.User?.FullName ?? string.Empty,
            Email = barber.User?.Email ?? string.Empty,
            DisplayName = barber.DisplayName,
            Bio = barber.Bio,
            PhotoUrl = barber.PhotoUrl,
            Specialties = barber.Specialties,
            BranchId = barber.BranchId,
            BranchName = barber.Branch != null ? barber.Branch.Name : null,
            CommissionRate = barber.CommissionRate,
            IsActive = barber.IsActive && (barber.User?.IsActive ?? false),
            CompletedAppointmentsCount = completedCount
        };

        if (IsJsonRequest())
        {
            return Ok(dto);
        }

        return Ok(dto);
    }

    private async Task<string?> HandlePhotoUploadAsync(IFormFile? file, string? fallbackUrl)
    {
        if (file == null || file.Length == 0)
        {
            return string.IsNullOrWhiteSpace(fallbackUrl) ? null : fallbackUrl.Trim();
        }

        // 1. Enforce maximum file size (5 MB limit to prevent disk exhaustion DoS)
        const long maxFileSize = 5 * 1024 * 1024;
        if (file.Length > maxFileSize)
        {
            return string.IsNullOrWhiteSpace(fallbackUrl) ? null : fallbackUrl.Trim();
        }

        // 2. Disallow SVG and non-raster formats to prevent Stored XSS via embedded scripts
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExts = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        if (!allowedExts.Contains(ext))
        {
            return string.IsNullOrWhiteSpace(fallbackUrl) ? null : fallbackUrl.Trim();
        }

        // 3. Verify magic bytes / file signatures to prevent disguised executables
        using (var stream = file.OpenReadStream())
        {
            var header = new byte[12];
            var bytesRead = await stream.ReadAsync(header, 0, header.Length);
            if (bytesRead < 4)
            {
                return string.IsNullOrWhiteSpace(fallbackUrl) ? null : fallbackUrl.Trim();
            }

            bool isValidHeader = false;
            // JPEG: FF D8 FF
            if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            {
                isValidHeader = true;
            }
            // PNG: 89 50 4E 47
            else if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
            {
                isValidHeader = true;
            }
            // WEBP: RIFF....WEBP
            else if (bytesRead >= 12 &&
                     header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
                     header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
            {
                isValidHeader = true;
            }

            if (!isValidHeader)
            {
                return string.IsNullOrWhiteSpace(fallbackUrl) ? null : fallbackUrl.Trim();
            }
        }

        var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "barbers");
        if (!Directory.Exists(uploadsDir))
        {
            Directory.CreateDirectory(uploadsDir);
        }

        var fileName = $"barber_{Guid.NewGuid():N}{ext}";
        var filePath = Path.Combine(uploadsDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return $"/uploads/barbers/{fileName}";
    }

    [HttpPost("admin/barbers")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateForm([FromForm] CreateBarberRequest request, IFormFile? photoFile)
    {
        if (photoFile != null && photoFile.Length > 0)
        {
            request.PhotoUrl = await HandlePhotoUploadAsync(photoFile, request.PhotoUrl);
        }
        return await ProcessCreateAsync(request, isApi: false);
    }

    [HttpPost("api/admin/barbers")]
    public async Task<IActionResult> CreateApi([FromBody] CreateBarberRequest request)
    {
        return await ProcessCreateAsync(request, isApi: true);
    }

    private async Task<IActionResult> ProcessCreateAsync(CreateBarberRequest request, bool isApi)
    {
        // 1. FluentValidation
        var validationResult = await _createValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.ToDictionary(e => e.PropertyName, e => e.ErrorMessage);
            if (isApi)
            {
                return BadRequest(new ApiErrorResponse("VALIDATION_ERROR", "Validation failed.", errors));
            }

            TempData["ErrorMessage"] = string.Join(" ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Redirect("/admin/barbers");
        }

        // 2. Duplicate Email Check
        var cleanEmail = request.Email.Trim().ToLowerInvariant();
        var existingUser = await _userManager.FindByEmailAsync(cleanEmail);
        if (existingUser != null)
        {
            if (isApi)
            {
                return BadRequest(new ApiErrorResponse("DUPLICATE_EMAIL", $"An account with email '{cleanEmail}' already exists."));
            }

            TempData["ErrorMessage"] = $"An account with email '{cleanEmail}' already exists.";
            return Redirect("/admin/barbers");
        }

        // 3. Create ApplicationUser
        var resolvedFullName = request.ResolvedFullName;
        var user = new ApplicationUser
        {
            UserName = cleanEmail,
            Email = cleanEmail,
            FullName = resolvedFullName,
            Role = UserRole.Barber,
            IsActive = true,
            CreatedAt = _clock.GetCurrentInstant()
        };

        var identityResult = await _userManager.CreateAsync(user, request.Password);
        if (!identityResult.Succeeded)
        {
            var errorMsg = string.Join(" ", identityResult.Errors.Select(e => e.Description));
            if (isApi)
            {
                return BadRequest(new ApiErrorResponse("IDENTITY_ERROR", errorMsg));
            }

            TempData["ErrorMessage"] = errorMsg;
            return Redirect("/admin/barbers");
        }

        await _userManager.AddToRoleAsync(user, "Barber");

        decimal commRate = 0.50m;
        if (request.CommissionRate.HasValue)
        {
            commRate = request.CommissionRate.Value > 1 ? request.CommissionRate.Value / 100m : request.CommissionRate.Value;
        }

        // 4. Create Barber Profile
        var barber = new BarberEntity
        {
            UserId = user.Id,
            DisplayName = request.DisplayName.Trim(),
            Bio = string.IsNullOrWhiteSpace(request.Bio) ? null : request.Bio.Trim(),
            PhotoUrl = string.IsNullOrWhiteSpace(request.PhotoUrl) ? null : request.PhotoUrl.Trim(),
            Specialties = string.IsNullOrWhiteSpace(request.Specialties) ? null : request.Specialties.Trim(),
            BranchId = request.BranchId,
            CommissionRate = commRate,
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
            BranchId = barber.BranchId,
            CommissionRate = barber.CommissionRate,
            IsActive = true,
            CompletedAppointmentsCount = 0
        };

        if (isApi)
        {
            return CreatedAtAction(nameof(Details), new { id = barber.Id }, resultDto);
        }

        TempData["SuccessMessage"] = $"Barber account for {barber.DisplayName} created successfully.";
        return Redirect("/admin/barbers");
    }

    [HttpPost("admin/barbers/{id:int}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateForm(int id, [FromForm] UpdateBarberRequest request, IFormFile? photoFile)
    {
        if (photoFile != null && photoFile.Length > 0)
        {
            request.PhotoUrl = await HandlePhotoUploadAsync(photoFile, request.PhotoUrl);
        }
        return await ProcessUpdateAsync(id, request, isApi: false);
    }

    [HttpPut("api/admin/barbers/{id:int}")]
    public async Task<IActionResult> UpdateApi(int id, [FromBody] UpdateBarberRequest request)
    {
        return await ProcessUpdateAsync(id, request, isApi: true);
    }

    private async Task<IActionResult> ProcessUpdateAsync(int id, UpdateBarberRequest request, bool isApi)
    {
        // 1. FluentValidation
        var validationResult = await _updateValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.ToDictionary(e => e.PropertyName, e => e.ErrorMessage);
            if (isApi)
            {
                return BadRequest(new ApiErrorResponse("VALIDATION_ERROR", "Validation failed.", errors));
            }

            TempData["ErrorMessage"] = string.Join(" ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Redirect("/admin/barbers");
        }

        // 2. Find barber
        var barber = await _dbContext.Barbers
            .FirstOrDefaultAsync(b => b.Id == id);

        if (barber == null)
        {
            return isApi
                ? NotFound(new ApiErrorResponse("NOT_FOUND", $"Barber with ID {id} not found."))
                : NotFound();
        }

        var barberUser = await _userManager.FindByIdAsync(barber.UserId.ToString());
        if (barberUser != null) barber.User = barberUser;

        // 3. Update barber & user details
        barber.DisplayName = request.DisplayName.Trim();
        barber.Bio = string.IsNullOrWhiteSpace(request.Bio) ? null : request.Bio.Trim();
        if (!string.IsNullOrWhiteSpace(request.PhotoUrl))
        {
            barber.PhotoUrl = request.PhotoUrl.Trim();
        }
        barber.Specialties = string.IsNullOrWhiteSpace(request.Specialties) ? null : request.Specialties.Trim();
        barber.BranchId = request.BranchId;

        if (request.CommissionRate.HasValue)
        {
            barber.CommissionRate = request.CommissionRate.Value > 1 ? request.CommissionRate.Value / 100m : request.CommissionRate.Value;
        }

        barber.User.FullName = request.ResolvedFullName;

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
            BranchId = barber.BranchId,
            CommissionRate = barber.CommissionRate,
            IsActive = barber.IsActive && barber.User.IsActive
        };

        if (isApi)
        {
            return Ok(resultDto);
        }

        TempData["SuccessMessage"] = $"Barber profile for {barber.DisplayName} updated successfully.";
        return Redirect("/admin/barbers");
    }

    [HttpPost("admin/barbers/{id:int}/reset-password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(int id, [FromForm] ResetBarberPasswordRequest request)
    {
        return await ProcessResetPasswordAsync(id, request, isApi: false);
    }

    [HttpPost("api/admin/barbers/{id:int}/reset-password")]
    public async Task<IActionResult> ResetPasswordApi(int id, [FromBody] ResetBarberPasswordRequest request)
    {
        return await ProcessResetPasswordAsync(id, request, isApi: true);
    }

    private async Task<IActionResult> ProcessResetPasswordAsync(int id, ResetBarberPasswordRequest request, bool isApi)
    {
        var validationResult = await _resetPasswordValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.ToDictionary(e => e.PropertyName, e => e.ErrorMessage);
            if (isApi)
            {
                return BadRequest(new ApiErrorResponse("VALIDATION_ERROR", "Validation failed.", errors));
            }

            TempData["ErrorMessage"] = string.Join(" ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Redirect("/admin/barbers");
        }

        var barber = await _dbContext.Barbers
            .FirstOrDefaultAsync(b => b.Id == id);

        if (barber == null)
        {
            return isApi
                ? NotFound(new ApiErrorResponse("NOT_FOUND", $"Barber with ID {id} not found."))
                : NotFound();
        }

        var barberUser = await _userManager.FindByIdAsync(barber.UserId.ToString());
        if (barberUser == null)
        {
            return isApi
                ? NotFound(new ApiErrorResponse("NOT_FOUND", "User account not found."))
                : NotFound();
        }
        barber.User = barberUser;

        var token = await _userManager.GeneratePasswordResetTokenAsync(barber.User);
        var result = await _userManager.ResetPasswordAsync(barber.User, token, request.NewPassword);

        if (!result.Succeeded)
        {
            var errorMsg = string.Join(" ", result.Errors.Select(e => e.Description));
            if (isApi)
            {
                return BadRequest(new ApiErrorResponse("PASSWORD_RESET_FAILED", errorMsg));
            }

            TempData["ErrorMessage"] = errorMsg;
            return Redirect("/admin/barbers");
        }

        if (isApi)
        {
            return Ok(new { success = true, message = $"Password for {barber.DisplayName} reset successfully." });
        }

        TempData["SuccessMessage"] = $"Password for {barber.DisplayName} reset successfully.";
        return Redirect("/admin/barbers");
    }

    [HttpPost("admin/barbers/{id:int}/toggle-status")]
    [HttpPost("api/admin/barbers/{id:int}/toggle-status")]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var barber = await _dbContext.Barbers
            .FirstOrDefaultAsync(b => b.Id == id);

        if (barber == null)
        {
            return IsJsonRequest()
                ? NotFound(new ApiErrorResponse("NOT_FOUND", $"Barber with ID {id} not found."))
                : NotFound();
        }

        var barberUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == barber.UserId);

        // Toggle both barber profile and user account
        var newStatus = !barber.IsActive;
        barber.IsActive = newStatus;
        if (barberUser != null) barberUser.IsActive = newStatus;

        await _dbContext.SaveChangesAsync();

        var statusText = newStatus ? "activated" : "deactivated";

        if (IsJsonRequest())
        {
            return Ok(new { barberId = barber.Id, isActive = barber.IsActive, message = $"Barber {barber.DisplayName} has been {statusText}." });
        }

        TempData["SuccessMessage"] = $"Barber {barber.DisplayName} has been {statusText}.";
        return Redirect("/admin/barbers");
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
