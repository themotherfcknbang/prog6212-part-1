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

public record ResultDto(
    int ResultID, int EnrolmentID, int ParticipantID, string ParticipantName,
    int EventID, string EventName, DateOnly EventDate,
    int CategoryID, string CategoryName, decimal DistanceKM,
    TimeOnly? FinishTime, int? Position, string Status, DateTime RecordedAt);