using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

[RequireRole("Organiser")]
public class OrganiserEventsController : OrganiserBaseController
{
    private const long MaxImageBytes = 5 * 1024 * 1024;
    private static readonly string[] ImageTypes = { "image/jpeg", "image/png", "image/webp" };

    public OrganiserEventsController(ApiClient api) : base(api) { }

    // ---------- List ----------
    public async Task<IActionResult> Index()
    {
        var result = await Api.GetAsync<List<EventSummary>>(
            $"api/organisers/{HttpContext.Session.GetUserId()}/events");

        var redirect = SessionExpired(result);
        if (redirect != null) return redirect;

        if (!result.Success)
        {
            ViewData["Error"] = result.Error;
            return View(new List<EventSummary>());
        }
        return View(result.Data ?? new List<EventSummary>());
    }

    // ---------- Create ----------
    [HttpGet]
    public IActionResult Create() => View("Form", new EventFormViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EventFormViewModel vm)
    {
        ValidateBanner(vm);
        if (vm.EventDate.HasValue && vm.EventDate.Value < DateOnly.FromDateTime(DateTime.Today))
            ModelState.AddModelError(nameof(vm.EventDate), "The event date cannot be in the past.");
        if (!ModelState.IsValid) return View("Form", vm);

        var result = await Api.PostAsync<EventDetail>("api/events", ToRequest(vm));

        var redirect = SessionExpired(result);
        if (redirect != null) return redirect;

        if (!result.Success || result.Data == null)
        {
            ModelState.AddModelError(string.Empty, result.Error);
            return View("Form", vm);
        }

        await TryUploadBannerAsync(result.Data.EventID, vm.Banner);

        TempData["Success"] = "Event created. Now add the distances people can enter.";
        return RedirectToAction("Index", "Categories", new { eventId = result.Data.EventID });
    }

    // ---------- Edit ----------
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var (ev, redirect) = await LoadOwnEventAsync(id);
        if (redirect != null) return redirect;

        return View("Form", new EventFormViewModel
        {
            EventID = ev!.EventID,
            EventName = ev.EventName,
            EventDate = ev.EventDate,
            Location = ev.Location,
            StartTime = ev.StartTime,
            Description = ev.Description,
            RouteDescription = ev.RouteDescription,
            ExistingBannerUrl = ev.BannerUrl
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EventFormViewModel vm)
    {
        if (vm.EventID == null) return RedirectToAction(nameof(Index));

        ValidateBanner(vm);
        if (!ModelState.IsValid) return View("Form", vm);

        var result = await Api.PutAsync<EventDetail>($"api/events/{vm.EventID}", ToRequest(vm));

        var redirect = SessionExpired(result);
        if (redirect != null) return redirect;

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error);
            return View("Form", vm);
        }

        await TryUploadBannerAsync(vm.EventID.Value, vm.Banner);

        TempData["Success"] = "Event updated.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Delete ----------
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await Api.DeleteAsync<MessageDto>($"api/events/{id}");

        var redirect = SessionExpired(result);
        if (redirect != null) return redirect;

        if (result.Success) TempData["Success"] = "Event deleted.";
        else TempData["Error"] = result.Error;   // e.g. "This event has active enrolments and cannot be deleted."

        return RedirectToAction(nameof(Index));
    }

    // ---------- Helpers ----------
    private static object ToRequest(EventFormViewModel vm) => new
    {
        vm.EventName, vm.EventDate, vm.Location, vm.Description, vm.RouteDescription, vm.StartTime
    };

    private void ValidateBanner(EventFormViewModel vm)
    {
        if (vm.Banner == null || vm.Banner.Length == 0) return;

        if (vm.Banner.Length > MaxImageBytes)
            ModelState.AddModelError(nameof(vm.Banner), "The banner image must be 5 MB or smaller.");
        else if (!ImageTypes.Contains(vm.Banner.ContentType, StringComparer.OrdinalIgnoreCase))
            ModelState.AddModelError(nameof(vm.Banner), "The banner must be a JPEG, PNG or WebP image.");
    }

    // The event is already saved by now, so a failed upload only warns instead of failing everything
    private async Task TryUploadBannerAsync(int eventId, IFormFile? banner)
    {
        if (banner == null || banner.Length == 0) return;

        var upload = await Api.UploadAsync<BannerResponse>($"api/events/{eventId}/banner", banner);
        if (!upload.Success)
            TempData["Warning"] = $"The event was saved, but the banner could not be uploaded: {upload.Error}";
    }
}
