using BarberBook.Data;
using BarberBook.Web.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BarberBook.Web.Controllers;

public class HomeController : Controller
{
    private readonly BarberBookDbContext _dbContext;

    public HomeController(BarberBookDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpGet("services")]
    [HttpGet("api/services")]
    public async Task<IActionResult> Services()
    {
        if (IsJsonRequest())
        {
            var services = await _dbContext.Services
                .Where(s => s.IsActive)
                .OrderBy(s => s.Name)
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.Description,
                    s.Price,
                    s.DurationMinutes,
                    s.IsActive
                })
                .ToListAsync();

            return Ok(services);
        }

        var serviceEntities = await _dbContext.Services
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .ToListAsync();

        return View(serviceEntities);
    }

    [HttpGet("barbers")]
    [HttpGet("api/barbers")]
    public async Task<IActionResult> Barbers()
    {
        if (IsJsonRequest())
        {
            var barbers = await _dbContext.Barbers
                .Include(b => b.User)
                .Where(b => b.IsActive && b.User.IsActive)
                .OrderBy(b => b.DisplayName)
                .Select(b => new
                {
                    b.Id,
                    b.DisplayName,
                    b.Bio,
                    b.PhotoUrl,
                    b.Specialties,
                    FullName = b.User.FullName,
                    b.IsActive
                })
                .ToListAsync();

            return Ok(barbers);
        }

        var barberEntities = await _dbContext.Barbers
            .Include(b => b.User)
            .Where(b => b.IsActive && b.User.IsActive)
            .OrderBy(b => b.DisplayName)
            .ToListAsync();

        return View(barberEntities);
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

    [Authorize(Roles = "Customer")]
    [HttpGet("profile")]
    public IActionResult Profile()
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
