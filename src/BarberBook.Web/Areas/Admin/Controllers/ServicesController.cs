using BarberBook.Data;
using BarberBook.Data.Entities;
using BarberBook.Data.Enums;
using BarberBook.Web.Common;
using BarberBook.Web.Models.Admin;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BarberBook.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class ServicesController : Controller
{
    private readonly BarberBookDbContext _dbContext;
    private readonly IValidator<CreateServiceRequest> _createValidator;
    private readonly IValidator<UpdateServiceRequest> _updateValidator;

    public ServicesController(
        BarberBookDbContext dbContext,
        IValidator<CreateServiceRequest> createValidator,
        IValidator<UpdateServiceRequest> updateValidator)
    {
        _dbContext = dbContext;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet("admin/services")]
    [HttpGet("api/admin/services")]
    public async Task<IActionResult> Index(
        [FromQuery] string? q = null,
        [FromQuery] string status = "all",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 50) pageSize = 10;

        var query = _dbContext.Services
            .AsNoTracking()
            .AsQueryable();

        // Status filter
        if (status.Equals("active", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(s => s.IsActive);
        }
        else if (status.Equals("inactive", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(s => !s.IsActive);
        }

        // Search filter
        if (!string.IsNullOrWhiteSpace(q))
        {
            var search = q.Trim().ToLower();
            query = query.Where(s =>
                s.Name.ToLower().Contains(search) ||
                (s.Description != null && s.Description.ToLower().Contains(search)));
        }

        var totalCount = await query.CountAsync();

        var rawServices = await query
            .OrderBy(s => s.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var serviceIds = rawServices.Select(s => s.Id).ToList();
        var completedCounts = (await _dbContext.Appointments
            .AsNoTracking()
            .Where(a => serviceIds.Contains(a.ServiceId) && a.Status == AppointmentStatus.Completed)
            .Select(a => a.ServiceId)
            .ToListAsync())
            .GroupBy(id => id)
            .ToDictionary(g => g.Key, g => g.Count());

        var services = rawServices.Select(s => new AdminServiceItemDto
        {
            Id = s.Id,
            Name = s.Name,
            Description = s.Description,
            Price = s.Price,
            DurationMinutes = s.DurationMinutes,
            IsActive = s.IsActive,
            CompletedBookingsCount = completedCounts.TryGetValue(s.Id, out var cnt) ? cnt : 0
        }).ToList();

        var viewModel = new ServiceListViewModel
        {
            Items = services,
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

    [HttpGet("admin/services/{id:int}")]
    [HttpGet("api/admin/services/{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        var service = await _dbContext.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id);

        if (service == null)
        {
            return IsJsonRequest()
                ? NotFound(new ApiErrorResponse("NOT_FOUND", $"Service with ID {id} not found."))
                : NotFound();
        }

        var completedCount = await _dbContext.Appointments
            .CountAsync(a => a.ServiceId == id && a.Status == AppointmentStatus.Completed);

        var dto = new AdminServiceItemDto
        {
            Id = service.Id,
            Name = service.Name,
            Description = service.Description,
            Price = service.Price,
            DurationMinutes = service.DurationMinutes,
            IsActive = service.IsActive,
            CompletedBookingsCount = completedCount
        };

        if (IsJsonRequest())
        {
            return Ok(dto);
        }

        return Ok(dto);
    }

    [HttpPost("admin/services")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] CreateServiceRequest request)
    {
        return await ProcessCreateAsync(request, isApi: false);
    }

    [HttpPost("api/admin/services")]
    public async Task<IActionResult> CreateApi([FromBody] CreateServiceRequest request)
    {
        return await ProcessCreateAsync(request, isApi: true);
    }

    private async Task<IActionResult> ProcessCreateAsync(CreateServiceRequest request, bool isApi)
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
            return Redirect("/admin/services");
        }

        // 2. Create Service
        var service = new Service
        {
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Price = request.Price,
            DurationMinutes = request.DurationMinutes,
            IsActive = true
        };

        _dbContext.Services.Add(service);
        await _dbContext.SaveChangesAsync();

        var resultDto = new AdminServiceItemDto
        {
            Id = service.Id,
            Name = service.Name,
            Description = service.Description,
            Price = service.Price,
            DurationMinutes = service.DurationMinutes,
            IsActive = service.IsActive,
            CompletedBookingsCount = 0
        };

        if (isApi)
        {
            return CreatedAtAction(nameof(Details), new { id = service.Id }, resultDto);
        }

        TempData["SuccessMessage"] = $"Service '{service.Name}' created successfully.";
        return Redirect("/admin/services");
    }

    [HttpPost("admin/services/{id:int}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, [FromForm] UpdateServiceRequest request)
    {
        return await ProcessUpdateAsync(id, request, isApi: false);
    }

    [HttpPut("api/admin/services/{id:int}")]
    public async Task<IActionResult> UpdateApi(int id, [FromBody] UpdateServiceRequest request)
    {
        return await ProcessUpdateAsync(id, request, isApi: true);
    }

    private async Task<IActionResult> ProcessUpdateAsync(int id, UpdateServiceRequest request, bool isApi)
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
            return Redirect("/admin/services");
        }

        // 2. Find Service
        var service = await _dbContext.Services.FindAsync(id);
        if (service == null)
        {
            return isApi
                ? NotFound(new ApiErrorResponse("NOT_FOUND", $"Service with ID {id} not found."))
                : NotFound();
        }

        // 3. Update Service properties (BR-14: price changes do NOT affect historical appointment snapshots)
        service.Name = request.Name.Trim();
        service.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        service.Price = request.Price;
        service.DurationMinutes = request.DurationMinutes;

        await _dbContext.SaveChangesAsync();

        var resultDto = new AdminServiceItemDto
        {
            Id = service.Id,
            Name = service.Name,
            Description = service.Description,
            Price = service.Price,
            DurationMinutes = service.DurationMinutes,
            IsActive = service.IsActive
        };

        if (isApi)
        {
            return Ok(resultDto);
        }

        TempData["SuccessMessage"] = $"Service '{service.Name}' updated successfully.";
        return Redirect("/admin/services");
    }

    [HttpPost("admin/services/{id:int}/toggle-status")]
    [HttpPost("api/admin/services/{id:int}/toggle-status")]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var service = await _dbContext.Services.FindAsync(id);
        if (service == null)
        {
            return IsJsonRequest()
                ? NotFound(new ApiErrorResponse("NOT_FOUND", $"Service with ID {id} not found."))
                : NotFound();
        }

        service.IsActive = !service.IsActive;
        await _dbContext.SaveChangesAsync();

        var statusText = service.IsActive ? "activated" : "deactivated";

        if (IsJsonRequest())
        {
            return Ok(new { serviceId = service.Id, isActive = service.IsActive, message = $"Service '{service.Name}' has been {statusText}." });
        }

        TempData["SuccessMessage"] = $"Service '{service.Name}' has been {statusText}.";
        return Redirect("/admin/services");
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

