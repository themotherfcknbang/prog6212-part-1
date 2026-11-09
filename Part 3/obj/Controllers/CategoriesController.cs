using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>An organiser's distances (categories) for one of their events.</summary>
[RequireRole("Organiser")]
public class CategoriesController : OrganiserBaseController
{
    public CategoriesController(ApiClient api) : base(api) { }

    // ---------- List ----------
    [HttpGet]
    public async Task<IActionResult> Index(int eventId)
    {
        var (ev, redirect) = await LoadOwnEventAsync(eventId);
        if (redirect != null) return redirect;

        var vm = new CategoriesPageViewModel { Event = ev! };

        // How many active entries each distance has
        foreach (var c in ev!.Categories)
        {
            var roster = await Api.GetAsync<List<RosterEntry>>($"api/categories/{c.CategoryID}/enrolments");

            var expired = SessionExpired(roster);
            if (expired != null) return expired;

            vm.Entries[c.CategoryID] = roster.Success && roster.Data != null ? roster.Data.Count : 0;
        }

        return View(vm);
    }

    // ---------- Create ----------
    [HttpGet]
    public async Task<IActionResult> Create(int eventId)
    {
        var (ev, redirect) = await LoadOwnEventAsync(eventId);
        if (redirect != null) return redirect;

        return View("Form", new CategoryFormViewModel { EventID = ev!.EventID, EventName = ev.EventName });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CategoryFormViewModel vm)
    {
        if (!ModelState.IsValid) return View("Form", vm);

        var result = await Api.PostAsync<CategoryInfo>($"api/events/{vm.EventID}/categories", ToRequest(vm));

        var redirect = SessionExpired(result);
        if (redirect != null) return redirect;

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error);
            return View("Form", vm);
        }

        TempData["Success"] = "Distance added.";
        return RedirectToAction(nameof(Index), new { eventId = vm.EventID });
    }

    // ---------- Edit ----------
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var category = await Api.GetAsync<CategoryInfo>($"api/categories/{id}");

        var expired = SessionExpired(category);
        if (expired != null) return expired;

        if (!category.Success || category.Data == null)
        {
            TempData["Error"] = category.IsNotFound ? "That distance could not be found." : category.Error;
            return RedirectToAction("Index", "OrganiserEvents");
        }

        // Also proves the event belongs to the logged-in organiser
        var (ev, redirect) = await LoadOwnEventAsync(category.Data.EventID);
        if (redirect != null) return redirect;

        var c = category.Data;
        return View("Form", new CategoryFormViewModel
        {
            CategoryID = c.CategoryID,
            EventID = c.EventID,
            EventName = ev!.EventName,
            CategoryName = c.CategoryName,
            DistanceKM = c.DistanceKM,
            EntryFee = c.EntryFee,
            MaxParticipants = c.MaxParticipants
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CategoryFormViewModel vm)
    {
        if (vm.CategoryID == null) return RedirectToAction(nameof(Index), new { eventId = vm.EventID });
        if (!ModelState.IsValid) return View("Form", vm);

        var result = await Api.PutAsync<CategoryInfo>($"api/categories/{vm.CategoryID}", ToRequest(vm));

        var redirect = SessionExpired(result);
        if (redirect != null) return redirect;

        if (!result.Success)
        {
            // e.g. "Maximum participants cannot be lower than the 40 active enrolments."
            ModelState.AddModelError(string.Empty, result.Error);
            return View("Form", vm);
        }

        TempData["Success"] = "Distance updated.";
        return RedirectToAction(nameof(Index), new { eventId = vm.EventID });
    }

    // ---------- Delete ----------
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, int eventId)
    {
        var result = await Api.DeleteAsync<MessageDto>($"api/categories/{id}");

        var redirect = SessionExpired(result);
        if (redirect != null) return redirect;

        if (result.Success) TempData["Success"] = "Distance deleted.";
        else TempData["Error"] = result.Error;   // e.g. "This category has active enrolments and cannot be deleted."

        return RedirectToAction(nameof(Index), new { eventId });
    }

    private static object ToRequest(CategoryFormViewModel vm) => new
    {
        vm.CategoryName, vm.DistanceKM, vm.EntryFee, vm.MaxParticipants
    };
}
