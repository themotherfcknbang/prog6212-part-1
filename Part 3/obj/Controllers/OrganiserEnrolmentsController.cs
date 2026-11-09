using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Who has entered each distance of an organiser's event.</summary>
[RequireRole("Organiser")]
public class OrganiserEnrolmentsController : OrganiserBaseController
{
    public OrganiserEnrolmentsController(ApiClient api) : base(api) { }

    [HttpGet]
    public async Task<IActionResult> Index(int eventId, int? categoryId, bool includeCancelled = false)
    {
        var (ev, redirect) = await LoadOwnEventAsync(eventId);
        if (redirect != null) return redirect;

        var vm = new EnrolmentsPageViewModel
        {
            Event = ev!,
            SelectedCategoryId = categoryId,
            IncludeCancelled = includeCancelled
        };

        var categories = ev!.Categories
            .Where(c => categoryId == null || c.CategoryID == categoryId)
            .OrderBy(c => c.DistanceKM);

        foreach (var category in categories)
        {
            var roster = await Api.GetAsync<List<RosterEntry>>(
                $"api/categories/{category.CategoryID}/enrolments?includeCancelled={includeCancelled.ToString().ToLowerInvariant()}");

            var expired = SessionExpired(roster);
            if (expired != null) return expired;

            vm.Groups.Add(new EnrolmentGroup
            {
                Category = category,
                Entries = roster.Data ?? new List<RosterEntry>(),
                Error = roster.Success ? null : roster.Error
            });
        }

        return View(vm);
    }
}
