using BarberBook.Data;
using BarberBook.Data.Entities;
using BarberBook.Data.Enums;
using BarberBook.Web.Common;
using BarberBook.Web.Models.Admin;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using NodaTime.Text;

namespace BarberBook.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class BranchesController : Controller
{
    private readonly BarberBookDbContext _dbContext;
    private readonly IValidator<CreateBranchRequest> _createValidator;
    private readonly IValidator<UpdateBranchRequest> _updateValidator;
    private readonly IClock _clock;

    public BranchesController(
        BarberBookDbContext dbContext,
        IValidator<CreateBranchRequest> createValidator,
        IValidator<UpdateBranchRequest> updateValidator,
        IClock clock)
    {
        _dbContext = dbContext;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _clock = clock;
    }

    [HttpGet("admin/branches")]
    [HttpGet("api/admin/branches")]
    public async Task<IActionResult> Index(
        [FromQuery] string? q = null,
        [FromQuery] string status = "all",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 50) pageSize = 10;

        var query = _dbContext.Branches
            .Include(b => b.Barbers)
            .Include(b => b.Appointments)
            .AsQueryable();

        if (status.Equals("active", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(b => b.IsActive);
        }
        else if (status.Equals("inactive", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(b => !b.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var search = q.Trim().ToLower();
            query = query.Where(b =>
                b.Name.ToLower().Contains(search) ||
                b.Address.ToLower().Contains(search) ||
                (b.PhoneNumber != null && b.PhoneNumber.ToLower().Contains(search)));
        }

        var totalCount = await query.CountAsync();

        var branches = await query
            .OrderBy(b => b.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new AdminBranchItemDto
            {
                Id = b.Id,
                Name = b.Name,
                Address = b.Address,
                PhoneNumber = b.PhoneNumber,
                OpeningTime = b.OpeningTime.ToString("HH:mm", null),
                ClosingTime = b.ClosingTime.ToString("HH:mm", null),
                IsActive = b.IsActive,
                BarbersCount = b.Barbers.Count(bar => bar.IsActive),
                CompletedAppointmentsCount = b.Appointments.Count(a => a.Status == AppointmentStatus.Completed)
            })
            .ToListAsync();

        var viewModel = new BranchListViewModel
        {
            Items = branches,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            SearchQuery = q,
            StatusFilter = status
        };

        if (IsJsonRequest())
        {
            return Ok(new
            {
                items = branches,
                totalCount = totalCount,
                page = page,
                pageSize = pageSize,
                totalPages = viewModel.TotalPages
            });
        }

        return View(viewModel);
    }

    [HttpPost("admin/branches")]
    public async Task<IActionResult> Create([FromForm] CreateBranchRequest request)
    {
        return await ProcessCreateAsync(request, isApi: false);
    }

    [HttpPost("api/admin/branches")]
    public async Task<IActionResult> CreateApi([FromBody] CreateBranchRequest request)
    {
        return await ProcessCreateAsync(request, isApi: true);
    }

    private async Task<IActionResult> ProcessCreateAsync(CreateBranchRequest request, bool isApi)
    {
        var validationResult = await _createValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.ToDictionary(e => e.PropertyName, e => e.ErrorMessage);
            if (isApi || IsJsonRequest())
            {
                return BadRequest(new ApiErrorResponse("VALIDATION_ERROR", "Validation failed.", errors));
            }

            TempData["ErrorMessage"] = string.Join(" ", validationResult.Errors.Select(e => e.ErrorMessage));
            return RedirectToAction(nameof(Index));
        }

        var parseOpen = LocalTimePattern.CreateWithInvariantCulture("HH:mm").Parse(request.OpeningTime);
        var openTime = parseOpen.Success ? parseOpen.Value : new LocalTime(9, 0);

        var parseClose = LocalTimePattern.CreateWithInvariantCulture("HH:mm").Parse(request.ClosingTime);
        var closeTime = parseClose.Success ? parseClose.Value : new LocalTime(18, 0);

        var branch = new Branch
        {
            Name = request.Name.Trim(),
            Address = request.Address.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            OpeningTime = openTime,
            ClosingTime = closeTime,
            IsActive = true,
            CreatedAt = _clock.GetCurrentInstant()
        };

        _dbContext.Branches.Add(branch);
        await _dbContext.SaveChangesAsync();

        var resultDto = new AdminBranchItemDto
        {
            Id = branch.Id,
            Name = branch.Name,
            Address = branch.Address,
            PhoneNumber = branch.PhoneNumber,
            OpeningTime = branch.OpeningTime.ToString("HH:mm", null),
            ClosingTime = branch.ClosingTime.ToString("HH:mm", null),
            IsActive = true,
            BarbersCount = 0,
            CompletedAppointmentsCount = 0
        };

        if (isApi || IsJsonRequest())
        {
            return Created($"/api/admin/branches/{branch.Id}", resultDto);
        }

        TempData["SuccessMessage"] = $"Branch '{branch.Name}' created successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("admin/branches/{id:int}/edit")]
    public async Task<IActionResult> Update(int id, [FromForm] UpdateBranchRequest request)
    {
        return await ProcessUpdateAsync(id, request, isApi: false);
    }

    [HttpPut("api/admin/branches/{id:int}")]
    public async Task<IActionResult> UpdateApi(int id, [FromBody] UpdateBranchRequest request)
    {
        return await ProcessUpdateAsync(id, request, isApi: true);
    }

    private async Task<IActionResult> ProcessUpdateAsync(int id, UpdateBranchRequest request, bool isApi)
    {
        var validationResult = await _updateValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.ToDictionary(e => e.PropertyName, e => e.ErrorMessage);
            if (isApi || IsJsonRequest())
            {
                return BadRequest(new ApiErrorResponse("VALIDATION_ERROR", "Validation failed.", errors));
            }

            TempData["ErrorMessage"] = string.Join(" ", validationResult.Errors.Select(e => e.ErrorMessage));
            return RedirectToAction(nameof(Index));
        }

        var branch = await _dbContext.Branches.FirstOrDefaultAsync(b => b.Id == id);
        if (branch == null)
        {
            return (isApi || IsJsonRequest())
                ? NotFound(new ApiErrorResponse("NOT_FOUND", $"Branch with ID {id} not found."))
                : NotFound();
        }

        var parseOpen = LocalTimePattern.CreateWithInvariantCulture("HH:mm").Parse(request.OpeningTime);
        var openTime = parseOpen.Success ? parseOpen.Value : new LocalTime(9, 0);

        var parseClose = LocalTimePattern.CreateWithInvariantCulture("HH:mm").Parse(request.ClosingTime);
        var closeTime = parseClose.Success ? parseClose.Value : new LocalTime(18, 0);

        branch.Name = request.Name.Trim();
        branch.Address = request.Address.Trim();
        branch.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        branch.OpeningTime = openTime;
        branch.ClosingTime = closeTime;

        await _dbContext.SaveChangesAsync();

        var resultDto = new AdminBranchItemDto
        {
            Id = branch.Id,
            Name = branch.Name,
            Address = branch.Address,
            PhoneNumber = branch.PhoneNumber,
            OpeningTime = branch.OpeningTime.ToString("HH:mm", null),
            ClosingTime = branch.ClosingTime.ToString("HH:mm", null),
            IsActive = branch.IsActive
        };

        if (isApi || IsJsonRequest())
        {
            return Ok(resultDto);
        }

        TempData["SuccessMessage"] = $"Branch '{branch.Name}' updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("admin/branches/{id:int}/toggle-status")]
    [HttpPost("api/admin/branches/{id:int}/toggle-status")]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var branch = await _dbContext.Branches.FirstOrDefaultAsync(b => b.Id == id);
        if (branch == null)
        {
            return IsJsonRequest()
                ? NotFound(new ApiErrorResponse("NOT_FOUND", $"Branch with ID {id} not found."))
                : NotFound();
        }

        branch.IsActive = !branch.IsActive;
        await _dbContext.SaveChangesAsync();

        var statusText = branch.IsActive ? "activated" : "deactivated";

        if (IsJsonRequest())
        {
            return Ok(new { branchId = branch.Id, isActive = branch.IsActive, message = $"Branch '{branch.Name}' has been {statusText}." });
        }

        TempData["SuccessMessage"] = $"Branch '{branch.Name}' has been {statusText}.";
        return RedirectToAction(nameof(Index));
    }

    private bool IsJsonRequest()
    {
        return Request.Path.StartsWithSegments("/api") ||
               Request.Headers.Accept.ToString().Contains("application/json");
    }
}
