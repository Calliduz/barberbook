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
            .Include(s => s.Appointments)
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

        var services = await query
            .OrderBy(s => s.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new AdminServiceItemDto
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description,
                Price = s.Price,
                DurationMinutes = s.DurationMinutes,
                IsActive = s.IsActive,
                CompletedBookingsCount = s.Appointments.Count(a => a.Status == AppointmentStatus.Completed)
            })
            .ToListAsync();

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
            .Include(s => s.Appointments)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (service == null)
        {
            return IsJsonRequest()
                ? NotFound(new ApiErrorResponse("NOT_FOUND", $"Service with ID {id} not found."))
                : NotFound();
        }

        var dto = new AdminServiceItemDto
        {
            Id = service.Id,
            Name = service.Name,
            Description = service.Description,
            Price = service.Price,
            DurationMinutes = service.DurationMinutes,
            IsActive = service.IsActive,
            CompletedBookingsCount = service.Appointments.Count(a => a.Status == AppointmentStatus.Completed)
        };

        if (IsJsonRequest())
        {
            return Ok(dto);
        }

        return Ok(dto);
    }

    [HttpPost("admin/services")]
    [HttpPost("api/admin/services")]
    public async Task<IActionResult> Create([FromBody] CreateServiceRequest? jsonRequest, [FromForm] CreateServiceRequest? formRequest)
    {
        var request = jsonRequest ?? formRequest ?? new CreateServiceRequest();

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

        if (IsJsonRequest())
        {
            return CreatedAtAction(nameof(Details), new { id = service.Id }, resultDto);
        }

        TempData["SuccessMessage"] = $"Service '{service.Name}' created successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("admin/services/{id:int}/edit")]
    [HttpPut("api/admin/services/{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateServiceRequest? jsonRequest, [FromForm] UpdateServiceRequest? formRequest)
    {
        var request = jsonRequest ?? formRequest ?? new UpdateServiceRequest();

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

        // 2. Find Service
        var service = await _dbContext.Services.FindAsync(id);
        if (service == null)
        {
            return IsJsonRequest()
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

        if (IsJsonRequest())
        {
            return Ok(resultDto);
        }

        TempData["SuccessMessage"] = $"Service '{service.Name}' updated successfully.";
        return RedirectToAction(nameof(Index));
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
        return RedirectToAction(nameof(Index));
    }

    private bool IsJsonRequest()
    {
        return Request.Path.StartsWithSegments("/api") ||
               Request.Headers.Accept.ToString().Contains("application/json");
    }
}

