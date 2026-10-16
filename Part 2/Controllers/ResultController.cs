using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Extensions;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

[ApiController]
public class ResultsController : ControllerBase
{
    private static readonly string[] ValidStatuses = { "Finished", "DNF", "DNS", "DSQ" };
    private readonly RaceDayDbContext _db;

    public ResultsController(RaceDayDbContext db) => _db = db;

    private IQueryable<Result> WithDetails() => _db.Results
        .Include(r => r.Enrolment).ThenInclude(en => en.Participant)
        .Include(r => r.Enrolment).ThenInclude(en => en.Category).ThenInclude(c => c.Event);

    private static ResultDto ToDto(Result r) =>
        new(r.ResultID, r.EnrolmentID, r.Enrolment.ParticipantID, r.Enrolment.Participant.FullName,
            r.Enrolment.Category.EventID, r.Enrolment.Category.Event.EventName,
            r.Enrolment.Category.Event.EventDate,
            r.Enrolment.CategoryID, r.Enrolment.Category.CategoryName,
            r.Enrolment.Category.DistanceKM,
            r.FinishTime, r.Position, r.Status, r.RecordedAt);

    // Returns an error message, or null when the request is valid
    private static string? Validate(ResultRequestDto dto)
    {
        if (!ValidStatuses.Contains(dto.Status, StringComparer.OrdinalIgnoreCase))
            return "Status must be one of: Finished, DNF, DNS, DSQ.";

        if (dto.Status.Equals("Finished", StringComparison.OrdinalIgnoreCase))
        {
            if (dto.FinishTime == null)
                return "A finish time is required for a finished result.";
            if (dto.FinishTime.Value <= TimeOnly.MinValue)
                return "Finish time must be greater than zero.";
        }
        else if (dto.FinishTime != null || dto.Position != null)
        {
            return "Only finished results can have a finish time or a position.";
        }
        return null;
    }

    private static string CanonicalStatus(string status) =>
        ValidStatuses.First(s => s.Equals(status, StringComparison.OrdinalIgnoreCase));

    // POST /api/enrolments/7/results
    [Authorize(Roles = "Organiser")]
    [HttpPost("api/enrolments/{enrolmentId:int}/results")]
    public async Task<IActionResult> Create(int enrolmentId, ResultRequestDto dto)
    {
        var enrolment = await _db.Enrolments
            .Include(en => en.Category).ThenInclude(c => c.Event)
            .FirstOrDefaultAsync(en => en.EnrolmentID == enrolmentId);
        if (enrolment == null) return NotFound(new { message = "Enrolment not found." });

        if (enrolment.Category.Event.OrganiserID != User.GetUserId())
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You can only record results for your own events." });

        if (enrolment.Status == "Cancelled")
            return Conflict(new { message = "Cannot record a result for a cancelled enrolment." });

        if (enrolment.Category.Event.EventDate > DateOnly.FromDateTime(DateTime.Today))
            return BadRequest(new { message = "Results can only be recorded once the event has taken place." });

        var error = Validate(dto);
        if (error != null) return BadRequest(new { message = error });

        if (await _db.Results.AnyAsync(r => r.EnrolmentID == enrolmentId))
            return Conflict(new { message = "A result has already been recorded for this enrolment." });

        var result = new Result
        {
            EnrolmentID = enrolmentId,
            Status = CanonicalStatus(dto.Status),
            FinishTime = dto.FinishTime,
            Position = dto.Position
        };
        _db.Results.Add(result);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Two simultaneous requests hit the unique constraint on EnrolmentID
            return Conflict(new { message = "A result has already been recorded for this enrolment." });
        }

        var saved = await WithDetails().AsNoTracking().FirstAsync(r => r.ResultID == result.ResultID);
        return CreatedAtAction(nameof(GetById), new { id = saved.ResultID }, ToDto(saved));
    }

    // PUT /api/results/3
    [Authorize(Roles = "Organiser")]
    [HttpPut("api/results/{id:int}")]
    public async Task<IActionResult> Update(int id, ResultRequestDto dto)
    {
        var result = await WithDetails().FirstOrDefaultAsync(r => r.ResultID == id);
        if (result == null) return NotFound(new { message = "Result not found." });

        if (result.Enrolment.Category.Event.OrganiserID != User.GetUserId())
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You can only correct results for your own events." });

        var error = Validate(dto);
        if (error != null) return BadRequest(new { message = error });

        result.Status = CanonicalStatus(dto.Status);
        result.FinishTime = dto.FinishTime;
        result.Position = dto.Position;

        await _db.SaveChangesAsync();
        return Ok(ToDto(result));
    }

    // GET /api/events/5/results?categoryId=3
    [HttpGet("api/events/{eventId:int}/results")]
    public async Task<IActionResult> GetForEvent(int eventId, [FromQuery] int? categoryId)
    {
        if (!await _db.Events.AnyAsync(e => e.EventID == eventId))
            return NotFound(new { message = "Event not found." });

        if (categoryId.HasValue &&
            !await _db.Categories.AnyAsync(c => c.CategoryID == categoryId && c.EventID == eventId))
            return NotFound(new { message = "Category not found for this event." });

        var query = WithDetails().AsNoTracking()
            .Where(r => r.Enrolment.Category.EventID == eventId);

        if (categoryId.HasValue)
            query = query.Where(r => r.Enrolment.CategoryID == categoryId.Value);

        var results = await query
            .OrderBy(r => r.Enrolment.Category.DistanceKM)
            .ThenBy(r => r.Position == null)       // finished runners first
            .ThenBy(r => r.Position)
            .ThenBy(r => r.FinishTime)
            .ToListAsync();

        return Ok(results.Select(ToDto));
    }

    // GET /api/participants/5/results
    [Authorize(Roles = "Participant")]
    [HttpGet("api/participants/{id:int}/results")]
    public async Task<IActionResult> GetForParticipant(int id)
    {
        if (id != User.GetUserId())
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You can only view your own results." });

        var results = await WithDetails().AsNoTracking()
            .Where(r => r.Enrolment.ParticipantID == id)
            .OrderByDescending(r => r.Enrolment.Category.Event.EventDate)
            .ToListAsync();

        return Ok(results.Select(ToDto));
    }

    // GET /api/results/3
    [Authorize]
    [HttpGet("api/results/{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await WithDetails().AsNoTracking().FirstOrDefaultAsync(r => r.ResultID == id);
        if (result == null) return NotFound(new { message = "Result not found." });

        var userId = User.GetUserId();
        var allowed = User.IsInRole("Participant")
            ? result.Enrolment.ParticipantID == userId
            : result.Enrolment.Category.Event.OrganiserID == userId;

        if (!allowed)
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You do not have access to this result." });

        return Ok(ToDto(result));
    }
}