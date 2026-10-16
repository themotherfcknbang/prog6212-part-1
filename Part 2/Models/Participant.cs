namespace RaceDay.Api.Models;

public class Participant
{
    public int ParticipantID { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
}