using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

public class EventsController : BaseController
{
    private readonly ApiClient _api;

    public EventsController(ApiClient api) => _api = api;

    // Full event information and its categories
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var result = await _api.GetAsync<EventDetail>($"api/events/{id}");
        if (!result.Success || result.Data == null)
        {
            TempData["Error"] = result.IsNotFound ? "That event could not be found." : result.Error;
            return RedirectToAction("Index", "Home");
        }

        var vm = new EventDetailsViewModel { Event = result.Data };

        // For participants, find out which categories they are already in
        if (HttpContext.Session.GetRole() == "Participant")
        {
            var mine = await _api.GetAsync<List<EnrolmentInfo>>(
                $"api/participants/{HttpContext.Session.GetUserId()}/enrolments");

            var redirect = SessionExpired(mine);
            if (redirect != null) return redirect;

            if (mine.Success && mine.Data != null)
                vm.MyEnrolments = mine.Data
                    .Where(e => e.EventID == id && e.Status != "Cancelled")
                    .ToDictionary(e => e.CategoryID);
        }

        // Weather only matters for events that haven't happened yet
        if (result.Data.EventDate >= DateOnly.FromDateTime(DateTime.Today))
        {
            var weather = await _api.GetAsync<WeatherInfo>($"api/events/{id}/weather");
            if (weather.Success && weather.Data != null) vm.Weather = weather.Data;
            else vm.WeatherError = weather.IsNotFound
                ? "We couldn't find this location on the map, so there is no weather forecast."
                : "The weather forecast is unavailable right now.";
        }

        return View(vm);
    }

    // Enrol in a category: the API records it straight away
    [HttpPost, ValidateAntiForgeryToken, RequireRole("Participant")]
    public async Task<IActionResult> Enrol(int eventId, int categoryId)
    {
        var result = await _api.PostAsync<EnrolmentInfo>($"api/categories/{categoryId}/enrolments");

        var redirect = SessionExpired(result);
        if (redirect != null) return redirect;

        if (result.Success)
            TempData["Success"] = $"You're in! Your enrolment is confirmed. Bib number: {result.Data?.BibNumber}.";
        else
            TempData["Error"] = result.Error;

        return RedirectToAction(nameof(Details), new { id = eventId });
    }
}
