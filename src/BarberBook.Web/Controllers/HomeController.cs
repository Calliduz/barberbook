using Microsoft.AspNetCore.Mvc;

namespace BarberBook.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}

