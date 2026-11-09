using Microsoft.AspNetCore.Mvc;

namespace RaceDay.Web.Controllers;

/// <summary>Friendly pages for 404, 403 and other status codes instead of a blank browser error.</summary>
public class StatusController : Controller
{
    [Route("Status/{code:int}")]
    public IActionResult Index(int code)
    {
        ViewData["Code"] = code;
        return View();
    }
}
