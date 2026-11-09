namespace RaceDay.Web.Models;

public class HomeViewModel
{
    public List<EventSummary> Events { get; set; } = new();
    public string? Keyword { get; set; }
    public string? Location { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public string? Error { get; set; }
    public bool HasFilters => !string.IsNullOrWhiteSpace(Keyword) || !string.IsNullOrWhiteSpace(Location)
                              || From.HasValue || To.HasValue;
}

public class EventDetailsViewModel
{
    public EventDetail Event { get; set; } = null!;

    // The logged-in participant's active enrolments for this event, keyed by category
    public Dictionary<int, EnrolmentInfo> MyEnrolments { get; set; } = new();

    // Weather is a bonus: if it can't be loaded the page still works, so it has its own error text
    public WeatherInfo? Weather { get; set; }
    public string? WeatherError { get; set; }
}
