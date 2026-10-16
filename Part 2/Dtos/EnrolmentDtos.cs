namespace RaceDay.Api.Dtos;

public record EnrolmentDto(
    int EnrolmentID, int ParticipantID, int CategoryID, int EventID,
    string EventName, DateOnly EventDate, string Location,
    string CategoryName, decimal DistanceKM, decimal EntryFee,
    DateTime EnrolmentDate, string Status, string? BibNumber);

public record RosterEntryDto(
    int EnrolmentID, int ParticipantID, string ParticipantName,
    DateTime EnrolmentDate, string Status, string? BibNumber);