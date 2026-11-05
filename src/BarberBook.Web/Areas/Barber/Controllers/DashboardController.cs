using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BarberBook.Web.Areas.Barber.Controllers;

[Area("Barber")]
[Authorize(Roles = "Barber")]
[Route("barber")]
public class DashboardController : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet("schedule")]
    public IActionResult Schedule()
    {
        return View();
    }

    [HttpGet("appointments")]
    public IActionResult Appointments()
    {
        return View();
    }

    [HttpGet("availability")]
    public IActionResult Availability()
    {
        return View();
    }

    [HttpGet("transactions")]
    public IActionResult Transactions()
    {
        return View();
    }

    [HttpGet("equipment")]
    public IActionResult Equipment()
    {
        return View();
    }
}

