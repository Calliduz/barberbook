using BarberBook.Web.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BarberBook.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet("services")]
    public IActionResult Services()
    {
        return View();
    }

    [HttpGet("barbers")]
    public IActionResult Barbers()
    {
        return View();
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
}
