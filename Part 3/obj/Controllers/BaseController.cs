using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

public abstract class BaseController : Controller
{
    /// <summary>
    /// If the API says our session has ended (401), clear ours and send the user to log in.
    /// Returns null when everything is fine, so callers can write:
    ///   var redirect = SessionExpired(result); if (redirect != null) return redirect;
    /// </summary>
    protected IActionResult? SessionExpired<T>(ApiResult<T> result)
    {
        if (!result.IsUnauthorized) return null;

        HttpContext.Session.Clear();
        TempData["Warning"] = "Your session has expired. Please log in again.";
        return RedirectToAction("Login", "Account");
    }
}
