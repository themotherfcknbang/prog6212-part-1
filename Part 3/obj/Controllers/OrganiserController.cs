using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

[RequireRole("Organiser")]
public class OrganiserController : OrganiserBaseController
{
    public OrganiserController(ApiClient api) : base(api) { }

    // Summary of the organiser's events, enrolments per event and upcoming events
    public async Task<IActionResult> Dashboard()
    {
        var vm = new DashboardViewModel { OrganiserName = HttpContext.Session.GetFullName() ?? string.Empty };

        var eventsResult = await Api.GetAsync<List<EventSummary>>(
            $"api/organisers/{HttpContext.Session.GetUserId()}/events");

        var redirect = SessionExpired(eventsResult);
        if (redirect != null) return redirect;

        if (!eventsResult.Success || eventsResult.Data == null)
        {
            vm.Error = eventsResult.Error;
            return View(vm);
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var rows = new List<DashboardEventRow>();
        foreach (var e in eventsResult.Data)
            rows.Add(new DashboardEventRow { Event = e, Enrolments = await CountEnrolmentsAsync(e.EventID) });

        vm.TotalEvents = rows.Count;
        vm.Upcoming = rows.Where(r => r.Event.EventDate >= today).OrderBy(r => r.Event.EventDate).ToList();
        vm.Past = rows.Where(r => r.Event.EventDate < today).OrderByDescending(r => r.Event.EventDate).Take(5).ToList();
        vm.UpcomingEvents = vm.Upcoming.Count;
        vm.TotalEnrolments = rows.Sum(r => r.Enrolments);

        return View(vm);
    }

    // Active enrolments across all of an event's categories
    private async Task<int> CountEnrolmentsAsync(int eventId)
    {
        var detail = await Api.GetAsync<EventDetail>($"api/events/{eventId}");
        if (!detail.Success || detail.Data == null) return 0;

        var total = 0;
        foreach (var c in detail.Data.Categories)
        {
            var roster = await Api.GetAsync<List<RosterEntry>>($"api/categories/{c.CategoryID}/enrolments");
            if (roster.Success && roster.Data != null) total += roster.Data.Count;
        }
        return total;
    }
}
