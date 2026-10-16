using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Extensions;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/profile")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    public ProfileController(RaceDayDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetProfile()
    {
        var id = User.GetUserId();

        if (User.IsInRole("Organiser"))
        {
            var organiser = await _db.Organisers.FindAsync(id);
            if (organiser == null) return Unauthorized();
            return Ok(ToDto(organiser));
        }

        var participant = await _db.Participants.FindAsync(id);
        if (participant == null) return Unauthorized();
        return Ok(ToDto(participant));
    }

    [HttpPut]
    public async Task<IActionResult> UpdateProfile(UpdateProfileDto dto)
    {
        var id = User.GetUserId();

        if (User.IsInRole("Organiser"))
        {
            var organiser = await _db.Organisers.FindAsync(id);
            if (organiser == null) return Unauthorized();

            organiser.FullName = dto.FullName.Trim();
            if (!string.IsNullOrWhiteSpace(dto.OrganisationName))
                organiser.OrganisationName = dto.OrganisationName.Trim();
            organiser.PhoneNumber = dto.PhoneNumber;

            await _db.SaveChangesAsync();
            return Ok(ToDto(organiser));
        }

        var participant = await _db.Participants.FindAsync(id);
        if (participant == null) return Unauthorized();

        if (dto.DateOfBirth.HasValue &&
            dto.DateOfBirth.Value > DateOnly.FromDateTime(DateTime.Today))
            return BadRequest(new { message = "Date of birth cannot be in the future." });

        participant.FullName = dto.FullName.Trim();
        if (dto.DateOfBirth.HasValue)
            participant.DateOfBirth = dto.DateOfBirth.Value;
        participant.Gender = dto.Gender;
        participant.EmergencyContactName = dto.EmergencyContactName;
        participant.EmergencyContactPhone = dto.EmergencyContactPhone;

        await _db.SaveChangesAsync();
        return Ok(ToDto(participant));
    }

    [HttpPut("password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
    {
        var id = User.GetUserId();

        if (User.IsInRole("Organiser"))
        {
            var organiser = await _db.Organisers.FindAsync(id);
            if (organiser == null) return Unauthorized();

            if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, organiser.PasswordHash))
                return Unauthorized(new { message = "Current password is incorrect." });

            organiser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        }
        else
        {
            var participant = await _db.Participants.FindAsync(id);
            if (participant == null) return Unauthorized();

            if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, participant.PasswordHash))
                return Unauthorized(new { message = "Current password is incorrect." });

            participant.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        }

        await _db.SaveChangesAsync();
        return Ok(new { message = "Password updated." });
    }

    private static OrganiserProfileDto ToDto(Organiser o) =>
        new(o.OrganiserID, "Organiser", o.FullName, o.Email,
            o.OrganisationName, o.PhoneNumber, o.CreatedAt);

    private static ParticipantProfileDto ToDto(Participant p) =>
        new(p.ParticipantID, "Participant", p.FullName, p.Email,
            p.DateOfBirth, p.Gender, p.EmergencyContactName,
            p.EmergencyContactPhone, p.CreatedAt);
}