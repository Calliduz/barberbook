using BarberBook.Data;
using BarberBook.Data.Entities;
using BarberBook.Data.Enums;
using BarberBook.Web.Common;
using BarberBook.Web.Models.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace BarberBook.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class CustomersController : Controller
{
    private readonly BarberBookDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly DateTimeZone _zone;

    public CustomersController(
        BarberBookDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        DateTimeZone? zone = null)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _zone = zone ?? DateTimeZoneProviders.Tzdb["Asia/Manila"];
    }

    private bool IsJsonRequest()
    {
        var path = Request.Path.Value ?? string.Empty;
        var accept = Request.Headers.Accept.ToString();
        var contentType = Request.ContentType ?? string.Empty;

        return path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) ||
               (accept.Contains("application/json") && !accept.Contains("text/html")) ||
               contentType.Contains("application/json");
    }

    [HttpGet("admin/customers")]
    [HttpGet("api/admin/customers")]
    public async Task<IActionResult> Index(
        [FromQuery] string? q = null,
        [FromQuery] string status = "all",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var baseQuery = _dbContext.Users
            .Where(u => u.Role == UserRole.Customer)
            .AsQueryable();

        var totalAll = await baseQuery.CountAsync();
        var totalActive = await baseQuery.CountAsync(u => u.IsActive);
        var totalInactive = totalAll - totalActive;

        if (status.Equals("active", StringComparison.OrdinalIgnoreCase))
        {
            baseQuery = baseQuery.Where(u => u.IsActive);
        }
        else if (status.Equals("inactive", StringComparison.OrdinalIgnoreCase))
        {
            baseQuery = baseQuery.Where(u => !u.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var search = q.Trim().ToLower();
            baseQuery = baseQuery.Where(u =>
                u.FullName.ToLower().Contains(search) ||
                (u.Email != null && u.Email.ToLower().Contains(search)) ||
                (u.PhoneNumber != null && u.PhoneNumber.Contains(search)));
        }

        var totalFiltered = await baseQuery.CountAsync();

        var customersRaw = await baseQuery
            .OrderBy(u => u.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var customerIds = customersRaw.Select(c => c.Id).ToList();

        // Get completed appointments and spend per customer
        var appts = await _dbContext.Appointments
            .Where(a => customerIds.Contains(a.CustomerId))
            .Select(a => new { a.CustomerId, a.Status, a.Price, a.StartsAt })
            .ToListAsync();

        var items = customersRaw.Select(u =>
        {
            var userAppts = appts.Where(a => a.CustomerId == u.Id).ToList();
            var completed = userAppts.Where(a => a.Status == AppointmentStatus.Completed).ToList();
            var noShows = userAppts.Where(a => a.Status == AppointmentStatus.NoShow).ToList();
            var totalSpent = completed.Sum(a => a.Price);
            var memberSinceLocal = u.CreatedAt.InZone(_zone);

            var lastVisit = userAppts.OrderByDescending(a => a.StartsAt).FirstOrDefault();
            var lastVisitStr = lastVisit != null
                ? lastVisit.StartsAt.InZone(_zone).ToString("MMM d, yyyy", null)
                : "No visits yet";

            return new AdminCustomerItemViewModel
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email ?? "",
                Phone = u.PhoneNumber ?? "-",
                IsActive = u.IsActive,
                MemberSince = memberSinceLocal.ToString("MMM d, yyyy", null),
                TotalAppointments = userAppts.Count,
                CompletedVisits = completed.Count,
                NoShowCount = noShows.Count,
                TotalSpent = totalSpent,
                FormattedTotalSpent = FormattingHelpers.FormatPeso(totalSpent),
                LastVisit = lastVisitStr
            };
        }).ToList();

        if (IsJsonRequest())
        {
            var dtoList = items.Select(i => new AdminCustomerApiItemDto
            {
                Id = i.Id,
                FullName = i.FullName,
                Email = i.Email,
                Phone = i.Phone,
                IsActive = i.IsActive,
                MemberSince = i.MemberSince,
                TotalAppointments = i.TotalAppointments,
                CompletedVisits = i.CompletedVisits,
                TotalSpent = i.TotalSpent,
                LastVisit = i.LastVisit
            }).ToList();

            return Ok(new AdminCustomerPagedResultDto
            {
                Items = dtoList,
                TotalCount = totalFiltered,
                Page = page,
                PageSize = pageSize
            });
        }

        var model = new AdminCustomerListViewModel
        {
            Items = items,
            SearchQuery = q,
            StatusFilter = status,
            Page = page,
            PageSize = pageSize,
            TotalCustomers = totalFiltered,
            ActiveCount = totalActive,
            InactiveCount = totalInactive
        };

        return View(model);
    }

    [HttpGet("admin/customers/{id:int}")]
    [HttpGet("api/admin/customers/{id:int}")]
    public async Task<IActionResult> Detail(int id, [FromQuery] string tab = "profile")
    {
        var customer = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == id && u.Role == UserRole.Customer);

        if (customer == null)
        {
            if (IsJsonRequest())
            {
                return NotFound(new ApiErrorResponse("NOT_FOUND", $"Customer with ID {id} not found."));
            }
            TempData["ErrorMessage"] = "Customer not found.";
            return RedirectToAction(nameof(Index));
        }

        var appointments = await _dbContext.Appointments
            .Include(a => a.Barber)
            .Include(a => a.Service)
            .Include(a => a.StatusHistories)
            .Where(a => a.CustomerId == id)
            .OrderByDescending(a => a.StartsAt)
            .ToListAsync();

        var completed = appointments.Where(a => a.Status == AppointmentStatus.Completed).ToList();
        var cancelled = appointments.Where(a => a.Status == AppointmentStatus.Cancelled).ToList();
        var noShows = appointments.Where(a => a.Status == AppointmentStatus.NoShow).ToList();
        var totalSpent = completed.Sum(a => a.Price);

        var lastVisitApp = completed.OrderByDescending(a => a.StartsAt).FirstOrDefault();
        var lastVisitStr = lastVisitApp != null
            ? lastVisitApp.StartsAt.InZone(_zone).ToString("MMMM dd, yyyy", null)
            : "No visits yet";

        var memberSince = customer.CreatedAt.InZone(_zone).ToString("MMMM dd, yyyy", null);

        var historyItems = appointments.Select(a =>
        {
            var startsAtLocal = a.StartsAt.InZone(_zone);
            var endsAtLocal = a.EndsAt.InZone(_zone);

            return new AdminCustomerAppointmentHistoryItemViewModel
            {
                Id = a.Id,
                ReferenceCode = a.ReferenceCode,
                BarberName = a.Barber.DisplayName,
                ServiceName = a.ServiceName,
                Price = a.Price,
                FormattedPrice = FormattingHelpers.FormatPeso(a.Price),
                StartsAt = a.StartsAt,
                ScheduledDateFormatted = startsAtLocal.Date.ToString("MMM dd, yyyy", null),
                ScheduledTime12h = $"{startsAtLocal.TimeOfDay.ToString("hh:mm tt", null)} – {endsAtLocal.TimeOfDay.ToString("hh:mm tt", null)}",
                Status = a.Status,
                Notes = a.Notes,
                Timeline = a.StatusHistories
                    .OrderBy(sh => sh.ChangedAt)
                    .Select(sh => new AdminCustomerStatusHistoryItemViewModel
                    {
                        FromStatus = sh.FromStatus?.ToString().ToLowerInvariant() ?? "(initial)",
                        ToStatus = sh.ToStatus.ToString().ToLowerInvariant(),
                        ChangedByRole = sh.ChangedByRole,
                        Note = sh.Note,
                        TimestampFormatted = sh.ChangedAt.InZone(_zone).ToString("MMM dd, yyyy hh:mm tt", null)
                    }).ToList()
            };
        }).ToList();

        if (IsJsonRequest())
        {
            return Ok(new
            {
                id = customer.Id,
                fullName = customer.FullName,
                email = customer.Email,
                phone = customer.PhoneNumber,
                isActive = customer.IsActive,
                memberSince,
                totalAppointments = appointments.Count,
                completedVisits = completed.Count,
                cancelledVisits = cancelled.Count,
                noShowVisits = noShows.Count,
                totalSpent,
                formattedTotalSpent = FormattingHelpers.FormatPeso(totalSpent),
                lastVisit = lastVisitStr,
                appointments = historyItems
            });
        }

        var model = new AdminCustomerDetailViewModel
        {
            Id = customer.Id,
            FullName = customer.FullName,
            Email = customer.Email ?? string.Empty,
            Phone = customer.PhoneNumber ?? string.Empty,
            IsActive = customer.IsActive,
            MemberSince = memberSince,
            TotalAppointments = appointments.Count,
            CompletedVisits = completed.Count,
            CancelledVisits = cancelled.Count,
            NoShowVisits = noShows.Count,
            TotalSpent = totalSpent,
            FormattedTotalSpent = FormattingHelpers.FormatPeso(totalSpent),
            LastVisit = lastVisitStr,
            ActiveTab = tab,
            Appointments = historyItems,
            EditForm = new AdminCustomerEditProfileModel
            {
                FullName = customer.FullName,
                Email = customer.Email ?? string.Empty,
                Phone = customer.PhoneNumber
            }
        };

        return View(model);
    }

    [HttpPost("admin/customers/{id:int}/edit")]
    [HttpPut("api/admin/customers/{id:int}")]
    public async Task<IActionResult> Edit(
        int id,
        [FromBody] AdminCustomerEditProfileModel? jsonModel,
        [FromForm] AdminCustomerEditProfileModel? formModel)
    {
        var model = jsonModel ?? formModel;
        if (model == null || string.IsNullOrWhiteSpace(model.FirstName) || string.IsNullOrWhiteSpace(model.LastName) || string.IsNullOrWhiteSpace(model.Email))
        {
            var msg = string.IsNullOrWhiteSpace(model?.FirstName)
                ? "First name is required."
                : string.IsNullOrWhiteSpace(model?.LastName)
                    ? "Last name is required."
                    : "Email is required.";
            if (IsJsonRequest())
            {
                return BadRequest(new ApiErrorResponse("VALIDATION_ERROR", msg));
            }
            TempData["Error"] = TempData["ErrorMessage"] = msg;
            return RedirectToAction(nameof(Detail), new { id, tab = "profile" });
        }

        var customer = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == id && u.Role == UserRole.Customer);

        if (customer == null)
        {
            if (IsJsonRequest())
            {
                return NotFound(new ApiErrorResponse("NOT_FOUND", $"Customer with ID {id} not found."));
            }
            TempData["Error"] = TempData["ErrorMessage"] = "Customer not found.";
            return RedirectToAction(nameof(Index));
        }

        // Email uniqueness check
        var targetEmail = model.Email.Trim().ToLowerInvariant();
        if (!string.Equals(customer.Email, targetEmail, StringComparison.OrdinalIgnoreCase))
        {
            var emailTaken = await _dbContext.Users
                .AnyAsync(u => u.Id != id && u.NormalizedEmail == targetEmail.ToUpperInvariant());

            if (emailTaken)
            {
                if (IsJsonRequest())
                {
                    return Conflict(new ApiErrorResponse("DUPLICATE_EMAIL", "Email address is already in use by another account."));
                }
                TempData["Error"] = TempData["ErrorMessage"] = "Email address is already in use by another account.";
                return RedirectToAction(nameof(Detail), new { id, tab = "profile" });
            }

            customer.Email = targetEmail;
            customer.NormalizedEmail = targetEmail.ToUpperInvariant();
            customer.UserName = targetEmail;
            customer.NormalizedUserName = targetEmail.ToUpperInvariant();
        }

        customer.FullName = model.FullName.Trim();
        customer.PhoneNumber = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim();

        await _dbContext.SaveChangesAsync();

        if (IsJsonRequest())
        {
            return Ok(new
            {
                success = true,
                message = "Customer profile updated successfully.",
                customer = new
                {
                    id = customer.Id,
                    fullName = customer.FullName,
                    email = customer.Email,
                    phone = customer.PhoneNumber
                }
            });
        }

        TempData["Success"] = TempData["SuccessMessage"] = $"Profile for '{customer.FullName}' updated successfully.";
        return RedirectToAction(nameof(Detail), new { id, tab = "profile" });
    }

    [HttpPost("admin/customers/{id:int}/toggle-status")]
    [HttpPost("api/admin/customers/{id:int}/status")]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var customer = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == id && u.Role == UserRole.Customer);
        if (customer == null)
        {
            if (IsJsonRequest())
            {
                return NotFound(new ApiErrorResponse("NOT_FOUND", $"Customer with ID {id} not found."));
            }
            TempData["Error"] = TempData["ErrorMessage"] = "Customer not found.";
            return RedirectToAction(nameof(Index));
        }

        // BR-13: Deactivate / reactivate
        customer.IsActive = !customer.IsActive;
        await _dbContext.SaveChangesAsync();

        var msg = customer.IsActive
            ? $"Customer '{customer.FullName}' account has been reactivated."
            : $"Customer '{customer.FullName}' account has been deactivated.";

        if (IsJsonRequest())
        {
            return Ok(new { success = true, isActive = customer.IsActive, message = msg });
        }

        TempData["Success"] = TempData["SuccessMessage"] = msg;
        return RedirectToAction(nameof(Detail), new { id, tab = "security" });
    }

    [HttpPost("admin/customers/{id:int}/reset-password")]
    [HttpPost("api/admin/customers/{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(
        int id,
        [FromBody] AdminCustomerResetPasswordModel? jsonModel,
        [FromForm] AdminCustomerResetPasswordModel? formModel)
    {
        var model = jsonModel ?? formModel;
        var newPassword = model?.NewPassword;

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
        {
            var msg = "Password must be at least 6 characters long.";
            if (IsJsonRequest())
            {
                return BadRequest(new ApiErrorResponse("VALIDATION_ERROR", msg));
            }
            TempData["Error"] = TempData["ErrorMessage"] = msg;
            return RedirectToAction(nameof(Detail), new { id, tab = "security" });
        }

        var customer = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == id && u.Role == UserRole.Customer);

        if (customer == null)
        {
            if (IsJsonRequest())
            {
                return NotFound(new ApiErrorResponse("NOT_FOUND", $"Customer with ID {id} not found."));
            }
            TempData["Error"] = TempData["ErrorMessage"] = "Customer not found.";
            return RedirectToAction(nameof(Index));
        }

        // AS-21 / LM-5: Admin resets password directly without email tokens
        var token = await _userManager.GeneratePasswordResetTokenAsync(customer);
        var result = await _userManager.ResetPasswordAsync(customer, token, newPassword);

        if (!result.Succeeded)
        {
            var errorMsg = string.Join(" ", result.Errors.Select(e => e.Description));
            if (IsJsonRequest())
            {
                return BadRequest(new ApiErrorResponse("PASSWORD_RESET_FAILED", errorMsg));
            }
            TempData["Error"] = TempData["ErrorMessage"] = errorMsg;
            return RedirectToAction(nameof(Detail), new { id, tab = "security" });
        }

        var successMsg = $"Password for '{customer.FullName}' has been reset successfully.";
        if (IsJsonRequest())
        {
            return Ok(new { success = true, message = successMsg });
        }

        TempData["Success"] = TempData["SuccessMessage"] = successMsg;
        return RedirectToAction(nameof(Detail), new { id, tab = "security" });
    }
}
