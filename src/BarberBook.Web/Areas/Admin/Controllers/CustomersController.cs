using BarberBook.Data;
using BarberBook.Data.Enums;
using BarberBook.Web.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace BarberBook.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class CustomersController : Controller
{
    private readonly BarberBookDbContext _dbContext;
    private readonly DateTimeZone _zone;

    public CustomersController(
        BarberBookDbContext dbContext,
        DateTimeZone? zone = null)
    {
        _dbContext = dbContext;
        _zone = zone ?? DateTimeZoneProviders.Tzdb["Asia/Manila"];
    }

    [HttpGet("admin/customers")]
    public async Task<IActionResult> Index(
        [FromQuery] string? q = null,
        [FromQuery] string status = "all")
    {
        var query = _dbContext.Users
            .Where(u => u.Role == UserRole.Customer)
            .AsQueryable();

        if (status.Equals("active", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(u => u.IsActive);
        }
        else if (status.Equals("inactive", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(u => !u.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var search = q.Trim().ToLower();
            query = query.Where(u =>
                u.FullName.ToLower().Contains(search) ||
                (u.Email != null && u.Email.ToLower().Contains(search)) ||
                (u.PhoneNumber != null && u.PhoneNumber.Contains(search)));
        }

        var customersRaw = await query
            .OrderBy(u => u.FullName)
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
                Phone = u.PhoneNumber ?? "—",
                IsActive = u.IsActive,
                MemberSince = memberSinceLocal.ToString("MMM d, yyyy", null),
                TotalAppointments = userAppts.Count,
                CompletedVisits = completed.Count,
                TotalSpent = totalSpent,
                FormattedTotalSpent = FormattingHelpers.FormatPeso(totalSpent),
                LastVisit = lastVisitStr
            };
        }).ToList();

        var model = new AdminCustomerListViewModel
        {
            Items = items,
            SearchQuery = q,
            StatusFilter = status,
            TotalCustomers = customersRaw.Count
        };

        return View(model);
    }

    [HttpPost("admin/customers/{id:int}/toggle-status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var customer = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == id && u.Role == UserRole.Customer);
        if (customer == null)
        {
            TempData["ErrorMessage"] = "Customer not found.";
            return RedirectToAction(nameof(Index));
        }

        // BR-13: Deactivate / reactivate
        customer.IsActive = !customer.IsActive;
        await _dbContext.SaveChangesAsync();

        TempData["SuccessMessage"] = customer.IsActive
            ? $"Customer '{customer.FullName}' account has been reactivated."
            : $"Customer '{customer.FullName}' account has been deactivated (BR-13).";

        return RedirectToAction(nameof(Index));
    }
}

public class AdminCustomerListViewModel
{
    public List<AdminCustomerItemViewModel> Items { get; set; } = new();
    public string? SearchQuery { get; set; }
    public string StatusFilter { get; set; } = "all";
    public int TotalCustomers { get; set; }
}

public class AdminCustomerItemViewModel
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string MemberSince { get; set; } = string.Empty;
    public int TotalAppointments { get; set; }
    public int CompletedVisits { get; set; }
    public decimal TotalSpent { get; set; }
    public string FormattedTotalSpent { get; set; } = string.Empty;
    public string LastVisit { get; set; } = string.Empty;
}
