namespace RaceDay.Web.Models;

// These mirror what the Part 2 API sends back. The MVC app only reads and displays them.

public record AuthResponse(int UserId, string FullName, string Email, string Role);

public record CategoryInfo(
    int CategoryID, int EventID, string CategoryName,
    decimal DistanceKM, decimal EntryFee, int? MaxParticipants);

public record EventSummary(
    int EventID, string EventName, DateOnly EventDate, string Location,
    TimeOnly? StartTime, string OrganisationName, int CategoryCount, string? BannerUrl);

public record EventDetail(
    int EventID, int OrganiserID, string OrganisationName, string EventName,
    DateOnly EventDate, string Location, string? Description,
    string? RouteDescription, TimeOnly? StartTime, DateTime CreatedAt,
    List<CategoryInfo> Categories, string? BannerUrl);

public record EnrolmentInfo(
    int EnrolmentID, int ParticipantID, int CategoryID, int EventID,
    string EventName, DateOnly EventDate, string Location,
    string CategoryName, decimal DistanceKM, decimal EntryFee,
    DateTime EnrolmentDate, string Status, string? BibNumber);

public record RosterEntry(
    int EnrolmentID, int ParticipantID, string ParticipantName,
    DateTime EnrolmentDate, string Status, string? BibNumber);

public record ResultInfo(
    int ResultID, int EnrolmentID, int ParticipantID, string ParticipantName,
    int EventID, string EventName, DateOnly EventDate,
    int CategoryID, string CategoryName, decimal DistanceKM,
    TimeOnly? FinishTime, int? Position, string Status, DateTime RecordedAt,
    int? TotalFinishers);

// ForecastType is "forecast" (a real forecast for the event day) or "current" (conditions right now,
// used when the date is out of forecast range; Note then explains this). Temperatures in °C, wind in km/h.
public record WeatherInfo(
    int EventID, string EventName, string Location,
    DateOnly ForecastDate, string ForecastType,
    double Temperature, double? TemperatureMin, double? TemperatureMax,
    double WindSpeed, string Conditions, string? Note);

public record OrganiserProfile(
    int OrganiserID, string Role, string FullName, string Email,
    string OrganisationName, string? PhoneNumber, DateTime CreatedAt);

public record ParticipantProfile(
    int ParticipantID, string Role, string FullName, string Email,
    DateOnly DateOfBirth, string? Gender,
    string? EmergencyContactName, string? EmergencyContactPhone, DateTime CreatedAt,
    string? ProfilePictureUrl);
