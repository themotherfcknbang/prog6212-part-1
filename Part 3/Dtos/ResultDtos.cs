using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

// Used for both create (POST) and update (PUT)
public class ResultRequestDto
{
    [Required]
    public string Status { get; set; } = string.Empty;   // Finished, DNF, DNS or DSQ

    public TimeOnly? FinishTime { get; set; }

    [Range(1, int.MaxValue)]
    public int? Position { get; set; }
}

// TotalFinishers = how many people finished that category (for "47th of 312").
// It is only filled in on the participant's own results list.
public record ResultDto(
    int ResultID, int EnrolmentID, int ParticipantID, string ParticipantName,
    int EventID, string EventName, DateOnly EventDate,
    int CategoryID, string CategoryName, decimal DistanceKM,
    TimeOnly? FinishTime, int? Position, string Status, DateTime RecordedAt,
    int? TotalFinishers = null);