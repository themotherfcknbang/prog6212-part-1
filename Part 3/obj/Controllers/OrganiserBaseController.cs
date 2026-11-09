using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Shared helpers for every organiser controller.</summary>
public abstract class OrganiserBaseController : BaseController
{
    protected readonly ApiClient Api;

    protected OrganiserBaseController(ApiClient api) => Api = api;

    /// <summary>
    /// Loads an event and checks it belongs to the logged-in organiser.
    /// If not, returns a redirect (with a friendly message) instead of the event.
    /// </summary>
    protected async Task<(EventDetail? Event, IActionResult? Redirect)> LoadOwnEventAsync(int eventId)
    {
        var result = await Api.GetAsync<EventDetail>($"api/events/{eventId}");

        if (!result.Success || result.Data == null)
        {
            TempData["Error"] = result.IsNotFound ? "That event could not be found." : result.Error;
            return (null, RedirectToAction("Index", "OrganiserEvents"));
        }

        if (result.Data.OrganiserID != HttpContext.Session.GetUserId())
        {
            TempData["Error"] = "You can only manage your own events.";
            return (null, RedirectToAction("Index", "OrganiserEvents"));
        }

        return (result.Data, null);
    }
}
