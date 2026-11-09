using System.ComponentModel.DataAnnotations;

namespace RaceDay.Web.Models;

public record PictureResponse(string ProfilePictureUrl);

public class ProfileViewModel
{
    [Required(ErrorMessage = "Please enter your full name."), StringLength(100)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    // Shown but not editable
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your date of birth.")]
    [Display(Name = "Date of birth")]
    public DateOnly? DateOfBirth { get; set; }

    [StringLength(20)]
    public string? Gender { get; set; }

    [StringLength(100)]
    [Display(Name = "Emergency contact name")]
    public string? EmergencyContactName { get; set; }

    [StringLength(20)]
    [Display(Name = "Emergency contact phone")]
    public string? EmergencyContactPhone { get; set; }

    public string? ProfilePictureUrl { get; set; }
}
