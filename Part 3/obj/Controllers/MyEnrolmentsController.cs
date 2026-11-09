using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

[RequireRole("Participant")]
public class MyEnrolmentsController : BaseController
{
    private readonly ApiClient _api;

    public MyEnrolmentsController(ApiClient api) => _api = api;

    // The participant's own enrolments and their status
    public async Task<IActionResult> Index()
    {
        var result = await _api.GetAsync<List<EnrolmentInfo>>(
            $"api/participants/{HttpContext.Session.GetUserId()}/enrolments");

        var redirect = SessionExpired(result);
        if (redirect != null) return redirect;

        if (!result.Success)
        {
            ViewData["Error"] = result.Error;
            return View(new List<EnrolmentInfo>());
        }
        return View(result.Data ?? new List<EnrolmentInfo>());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var result = await _api.DeleteAsync<MessageDto>($"api/enrolments/{id}");

        var redirect = SessionExpired(result);
        if (redirect != null) return redirect;

        if (result.Success) TempData["Success"] = "Your enrolment has been cancelled.";
        else TempData["Error"] = result.Error;

        return RedirectToAction(nameof(Index));
    }
}
