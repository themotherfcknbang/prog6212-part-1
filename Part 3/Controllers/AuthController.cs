using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    public AuthController(RaceDayDbContext db) => _db = db;

    [HttpPost("register/organiser")]
    public async Task<IActionResult> RegisterOrganiser(RegisterOrganiserDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        if (await EmailExistsAsync(email))
            return Conflict(new { message = "That email is already registered." });

        var organiser = new Organiser
        {
            FullName = dto.FullName.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            OrganisationName = dto.OrganisationName.Trim(),
            PhoneNumber = dto.PhoneNumber
        };
        _db.Organisers.Add(organiser);
        await _db.SaveChangesAsync();

        await SignInAsync(organiser.OrganiserID, organiser.FullName, email, "Organiser");
        return Created("/api/profile",
            new AuthResponseDto(organiser.OrganiserID, organiser.FullName, email, "Organiser"));
    }

    [HttpPost("register/participant")]
    public async Task<IActionResult> RegisterParticipant(RegisterParticipantDto dto)
    {
        if (dto.DateOfBirth > DateOnly.FromDateTime(DateTime.Today))
            return BadRequest(new { message = "Date of birth cannot be in the future." });

        var email = dto.Email.Trim().ToLowerInvariant();
        if (await EmailExistsAsync(email))
            return Conflict(new { message = "That email is already registered." });

        var participant = new Participant
        {
            FullName = dto.FullName.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            DateOfBirth = dto.DateOfBirth,
            Gender = dto.Gender,
            EmergencyContactName = dto.EmergencyContactName,
            EmergencyContactPhone = dto.EmergencyContactPhone
        };
        _db.Participants.Add(participant);
        await _db.SaveChangesAsync();

        await SignInAsync(participant.ParticipantID, participant.FullName, email, "Participant");
        return Created("/api/profile",
            new AuthResponseDto(participant.ParticipantID, participant.FullName, email, "Participant"));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();

        var organiser = await _db.Organisers.FirstOrDefaultAsync(o => o.Email == email);
        if (organiser != null && BCrypt.Net.BCrypt.Verify(dto.Password, organiser.PasswordHash))
        {
            await SignInAsync(organiser.OrganiserID, organiser.FullName, email, "Organiser");
            return Ok(new AuthResponseDto(organiser.OrganiserID, organiser.FullName, email, "Organiser"));
        }

        var participant = await _db.Participants.FirstOrDefaultAsync(p => p.Email == email);
        if (participant != null && BCrypt.Net.BCrypt.Verify(dto.Password, participant.PasswordHash))
        {
            await SignInAsync(participant.ParticipantID, participant.FullName, email, "Participant");
            return Ok(new AuthResponseDto(participant.ParticipantID, participant.FullName, email, "Participant"));
        }

        // Same message whether the email or the password was wrong
        return Unauthorized(new { message = "Invalid email or password." });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new { message = "Logged out." });
    }

    // An email must be unique across BOTH tables, otherwise login would be ambiguous
    private async Task<bool> EmailExistsAsync(string email) =>
        await _db.Organisers.AnyAsync(o => o.Email == email) ||
        await _db.Participants.AnyAsync(p => p.Email == email);

    private async Task SignInAsync(int userId, string fullName, string email, string role)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, fullName),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));
    }
}