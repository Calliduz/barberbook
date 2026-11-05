using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BarberBook.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
[Route("admin")]
public class DashboardController : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet("appointments")]
    public IActionResult Appointments()
    {
        return View();
    }

    [HttpGet("history")]
    public IActionResult History()
    {
        return View();
    }

    [HttpGet("barbers")]
    public IActionResult Barbers()
    {
        return View();
    }

    [HttpGet("schedules")]
    public IActionResult Schedules()
    {
        return View();
    }

    [HttpGet("services")]
    public IActionResult Services()
    {
        return View();
    }

    [HttpGet("customers")]
    public IActionResult Customers()
    {
        return View();
    }

    [HttpGet("reports")]
    public IActionResult Reports()
    {
        return View();
    }

    [HttpGet("inventory")]
    public IActionResult Inventory()
    {
        return View();
    }
}

