using Microsoft.AspNetCore.Mvc;
using StayBook.Helpers.Http;

namespace StayBook.Controllers;

/// <summary>The single-page staff dashboard shell; data comes from <c>/api/v1/snapshot</c>.</summary>
public sealed class DashboardController : Controller
{
    [HttpGet("/")]
    [HttpHead("/")]
    public IActionResult Index() => View(HttpContext.Actor());
}
