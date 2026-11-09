using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

[RequireRole("Participant")]
public class MyResultsController : BaseController
{
    private readonly ApiClient _api;

    public MyResultsController(ApiClient api) => _api = api;

    // Personal race history: finish time and position for every completed event
    public async Task<IActionResult> Index()
    {
        var result = await _api.GetAsync<List<ResultInfo>>(
            $"api/participants/{HttpContext.Session.GetUserId()}/results");

        var redirect = SessionExpired(result);
        if (redirect != null) return redirect;

        if (!result.Success)
        {
            ViewData["Error"] = result.Error;
            return View(new List<ResultInfo>());
        }
        return View(result.Data ?? new List<ResultInfo>());
    }
}
