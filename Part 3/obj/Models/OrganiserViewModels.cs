using System.ComponentModel.DataAnnotations;

namespace RaceDay.Web.Models;

public record BannerResponse(string BannerUrl);

// Drives the tab bar shown on every per-event organiser page
public record EventTabsModel(int EventID, string EventName, string Active);

// ---------- Dashboard ----------
public class DashboardEventRow
{
    public EventSummary Event { get; set; } = null!;
    public int Enrolments { get; set; }
}

public class DashboardViewModel
{
    public string OrganiserName { get; set; } = string.Empty;
    public int TotalEvents { get; set; }
    public int UpcomingEvents { get; set; }
    public int TotalEnrolments { get; set; }
    public List<DashboardEventRow> Upcoming { get; set; } = new();
    public List<DashboardEventRow> Past { get; set; } = new();
    public string? Error { get; set; }
}

// ---------- Event create / edit ----------
public class EventFormViewModel
{
    public int? EventID { get; set; }

    [Required(ErrorMessage = "Please enter the event name."), StringLength(150)]
    [Display(Name = "Event name")]
    public string EventName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please choose the event date.")]
    [Display(Name = "Date")]
    public DateOnly? EventDate { get; set; }

    [Required(ErrorMessage = "Please enter the location."), StringLength(150)]
    public string Location { get; set; } = string.Empty;

    [Display(Name = "Start time")]
    public TimeOnly? StartTime { get; set; }

    public string? Description { get; set; }

    [Display(Name = "Route description")]
    public string? RouteDescription { get; set; }

    public string? ExistingBannerUrl { get; set; }

    [Display(Name = "Event banner image")]
    public IFormFile? Banner { get; set; }
}

// ---------- Categories ----------
public class CategoriesPageViewModel
{
    public EventDetail Event { get; set; } = null!;
    public Dictionary<int, int> Entries { get; set; } = new();   // CategoryID -> active enrolments
}

public class CategoryFormViewModel
{
    public int? CategoryID { get; set; }
    public int EventID { get; set; }
    public string? EventName { get; set; }

    [Required(ErrorMessage = "Please enter a category name, for example 10 km."), StringLength(50)]
    [Display(Name = "Category name")]
    public string CategoryName { get; set; } = string.Empty;

    [Range(0.01, 999.99, ErrorMessage = "Distance must be between 0.01 and 999.99 km.")]
    [Display(Name = "Distance (km)")]
    public decimal DistanceKM { get; set; }

    [Range(0, 999999.99, ErrorMessage = "Entry fee cannot be negative.")]
    [Display(Name = "Entry fee (R)")]
    public decimal EntryFee { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Maximum participants must be at least 1 (or leave it empty for no limit).")]
    [Display(Name = "Maximum participants")]
    public int? MaxParticipants { get; set; }
}

// ---------- Enrolments per event ----------
public class EnrolmentGroup
{
    public CategoryInfo Category { get; set; } = null!;
    public List<RosterEntry> Entries { get; set; } = new();
    public string? Error { get; set; }
}

public class EnrolmentsPageViewModel
{
    public EventDetail Event { get; set; } = null!;
    public int? SelectedCategoryId { get; set; }
    public bool IncludeCancelled { get; set; }
    public List<EnrolmentGroup> Groups { get; set; } = new();
}

// ---------- Results entry ----------
public class ResultRow
{
    public int EnrolmentID { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public string? BibNumber { get; set; }
    public int? ResultID { get; set; }
    public string Status { get; set; } = "Finished";
    public string? FinishTime { get; set; }      // hh:mm:ss
    public int? Position { get; set; }
}

public class ResultsGroup
{
    public CategoryInfo Category { get; set; } = null!;
    public List<ResultRow> Rows { get; set; } = new();
    public string? Error { get; set; }
}

public class ResultsPageViewModel
{
    public EventDetail Event { get; set; } = null!;
    public int? SelectedCategoryId { get; set; }
    public bool EventHasPassed { get; set; }
    public List<ResultsGroup> Groups { get; set; } = new();
}
