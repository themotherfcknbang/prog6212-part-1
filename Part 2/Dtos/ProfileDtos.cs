using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

public record OrganiserProfileDto(
    int OrganiserID, string Role, string FullName, string Email,
    string OrganisationName, string? PhoneNumber, DateTime CreatedAt);

public record ParticipantProfileDto(
    int ParticipantID, string Role, string FullName, string Email,
    DateOnly DateOfBirth, string? Gender,
    string? EmergencyContactName, string? EmergencyContactPhone, DateTime CreatedAt);

// One body for both roles: each role only uses its own fields.
public class UpdateProfileDto
{
    [Required, StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    // Organiser fields
    [StringLength(150)]
    public string? OrganisationName { get; set; }

    [StringLength(20)]
    public string? PhoneNumber { get; set; }

    // Participant fields
    public DateOnly? DateOfBirth { get; set; }

    [StringLength(20)]
    public string? Gender { get; set; }

    [StringLength(100)]
    public string? EmergencyContactName { get; set; }

    [StringLength(20)]
    public string? EmergencyContactPhone { get; set; }
}

public class ChangePasswordDto
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, MinLength(8), StringLength(100)]
    public string NewPassword { get; set; } = string.Empty;
}