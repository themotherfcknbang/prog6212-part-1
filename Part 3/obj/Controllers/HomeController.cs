using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

public class HomeController : Controller
{
    private readonly ApiClient _api;

    public HomeController(ApiClient api) => _api = api;

    // Home page: all upcoming events, with filtering
    public async Task<IActionResult> Index(
        string? keyword, string? location,
        [FromQuery(Name = "from")] DateOnly? fromDate,
        [FromQuery(Name = "to")] DateOnly? toDate)
    {
        var vm = new HomeViewModel
        {
            Keyword = keyword, Location = location, From = fromDate, To = toDate
        };

        if (fromDate.HasValue && toDate.HasValue && toDate < fromDate)
        {
            vm.Error = "The 'to' date cannot be earlier than the 'from' date.";
            return View(vm);
        }

        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(keyword)) query.Add($"keyword={Uri.EscapeDataString(keyword.Trim())}");
        if (!string.IsNullOrWhiteSpace(location)) query.Add($"location={Uri.EscapeDataString(location.Trim())}");
        if (fromDate.HasValue) query.Add($"from={fromDate:yyyy-MM-dd}");
        if (toDate.HasValue) query.Add($"to={toDate:yyyy-MM-dd}");

        var url = "api/events" + (query.Count > 0 ? "?" + string.Join("&", query) : "");
        var result = await _api.GetAsync<List<EventSummary>>(url);

        if (result.Success) vm.Events = result.Data ?? new();
        else vm.Error = result.Error;

        return View(vm);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
