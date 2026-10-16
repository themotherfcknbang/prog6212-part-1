using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

public record CategoryDto(
    int CategoryID, int EventID, string CategoryName,
    decimal DistanceKM, decimal EntryFee, int? MaxParticipants);

public record EventSummaryDto(
    int EventID, string EventName, DateOnly EventDate, string Location,
    TimeOnly? StartTime, string OrganisationName, int CategoryCount);

public record EventDetailDto(
    int EventID, int OrganiserID, string OrganisationName, string EventName,
    DateOnly EventDate, string Location, string? Description,
    string? RouteDescription, TimeOnly? StartTime, DateTime CreatedAt,
    List<CategoryDto> Categories);

// Used for both create (POST) and update (PUT)
public class EventRequestDto
{
    [Required, StringLength(150)]
    public string EventName { get; set; } = string.Empty;

    [Required]
    public DateOnly? EventDate { get; set; }

    [Required, StringLength(150)]
    public string Location { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string? RouteDescription { get; set; }
    public TimeOnly? StartTime { get; set; }
}