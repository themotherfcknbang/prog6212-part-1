using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Capture and correct the results of an organiser's event, one participant at a time.</summary>
[RequireRole("Organiser")]
public class OrganiserResultsController : OrganiserBaseController
{
    private static readonly string[] Statuses = { "Finished", "DNF", "DNS", "DSQ" };
    private static readonly string[] TimeFormats = { "H:mm:ss", "HH:mm:ss" };

    public OrganiserResultsController(ApiClient api) : base(api) { }

    // ---------- Page ----------
    [HttpGet]
    public async Task<IActionResult> Index(int eventId, int? categoryId)
    {
        var (ev, redirect) = await LoadOwnEventAsync(eventId);
        if (redirect != null) return redirect;

        var vm = new ResultsPageViewModel
        {
            Event = ev!,
            SelectedCategoryId = categoryId,
            // The API only accepts results from the event day onwards
            EventHasPassed = ev!.EventDate <= DateOnly.FromDateTime(DateTime.Today)
        };

        var categories = ev.Categories
            .Where(c => categoryId == null || c.CategoryID == categoryId)
            .OrderBy(c => c.DistanceKM);

        foreach (var category in categories)
        {
            var group = new ResultsGroup { Category = category };

            var roster = await Api.GetAsync<List<RosterEntry>>($"api/categories/{category.CategoryID}/enrolments");
            var expired = SessionExpired(roster);
            if (expired != null) return expired;

            var results = await Api.GetAsync<List<ResultInfo>>(
                $"api/events/{eventId}/results?categoryId={category.CategoryID}");

            if (!roster.Success || !results.Success)
            {
                group.Error = !roster.Success ? roster.Error : results.Error;
                vm.Groups.Add(group);
                continue;
            }

            var byEnrolment = (results.Data ?? new List<ResultInfo>())
                .ToDictionary(r => r.EnrolmentID);

            group.Rows = (roster.Data ?? new List<RosterEntry>())
                .Select(e =>
                {
                    byEnrolment.TryGetValue(e.EnrolmentID, out var r);
                    return new ResultRow
                    {
                        EnrolmentID = e.EnrolmentID,
                        ParticipantName = e.ParticipantName,
                        BibNumber = e.BibNumber,
                        ResultID = r?.ResultID,
                        Status = r?.Status ?? "Finished",
                        FinishTime = r?.FinishTime?.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                        Position = r?.Position
                    };
                })
                // Captured finishers first by position, then the rest by name
                .OrderBy(row => row.ResultID == null ? 1 : 0)
                .ThenBy(row => row.Position ?? int.MaxValue)
                .ThenBy(row => row.ParticipantName)
                .ToList();

            vm.Groups.Add(group);
        }

        return View(vm);
    }

    // ---------- Save one row (creates the result, or corrects an existing one) ----------
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(
        int eventId, int categoryId, int enrolmentId, int? resultId,
        string status, string? finishTime, int? position)
    {
        var back = RedirectToAction("Index", "OrganiserResults", new { eventId, categoryId }, $"row-{enrolmentId}");

        if (!Statuses.Contains(status))
        {
            TempData["Error"] = "Please choose a valid status.";
            return back;
        }

        var finished = status == "Finished";
        TimeOnly? time = null;

        if (finished)
        {
            if (!TimeOnly.TryParseExact(finishTime?.Trim(), TimeFormats, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var parsed) || parsed == TimeOnly.MinValue)
            {
                TempData["Error"] = "Please enter the finish time as hh:mm:ss, for example 01:45:30.";
                return back;
            }
            time = parsed;

            if (position is < 1)
            {
                TempData["Error"] = "Position must be 1 or higher.";
                return back;
            }
        }

        // DNF, DNS and DSQ carry no time or position
        var body = new { Status = status, FinishTime = finished ? time : null, Position = finished ? position : null };

        var result = resultId.HasValue
            ? await Api.PutAsync<ResultInfo>($"api/results/{resultId}", body)
            : await Api.PostAsync<ResultInfo>($"api/enrolments/{enrolmentId}/results", body);

        var redirect = SessionExpired(result);
        if (redirect != null) return redirect;

        if (result.Success) TempData["Success"] = "Result saved.";
        else TempData["Error"] = result.Error;

        return back;
    }
}
