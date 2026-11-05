using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BarberBook.Web.Areas.Barber.Controllers;

[Area("Barber")]
[Authorize(Roles = "Barber")]
public abstract class BarberBaseController : Controller
{
}

