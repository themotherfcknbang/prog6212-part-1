using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Extensions;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

[ApiController]
public class EnrolmentsController : ControllerBase
{
    private const string Cancelled = "Cancelled";
    private readonly RaceDayDbContext _db;

    public EnrolmentsController(RaceDayDbContext db) => _db = db;

    private static EnrolmentDto ToDto(Enrolment en) =>
        new(en.EnrolmentID, en.ParticipantID, en.CategoryID, en.Category.EventID,
            en.Category.Event.EventName, en.Category.Event.EventDate, en.Category.Event.Location,
            en.Category.CategoryName, en.Category.DistanceKM, en.Category.EntryFee,
            en.EnrolmentDate, en.Status, en.BibNumber);

    private IQueryable<Enrolment> WithEvent() =>
        _db.Enrolments.Include(en => en.Category).ThenInclude(c => c.Event);

    // POST /api/categories/3/enrolments
    [Authorize(Roles = "Participant")]
    [HttpPost("api/categories/{categoryId:int}/enrolments")]
    public async Task<IActionResult> Enrol(int categoryId)
    {
        var category = await _db.Categories
            .Include(c => c.Event)
            .FirstOrDefaultAsync(c => c.CategoryID == categoryId);
        if (category == null) return NotFound(new { message = "Category not found." });

        if (category.Event.EventDate < DateOnly.FromDateTime(DateTime.Today))
            return BadRequest(new { message = "This event has already taken place." });

        var participantId = User.GetUserId();

        var existing = await _db.Enrolments.FirstOrDefaultAsync(en =>
            en.CategoryID == categoryId && en.ParticipantID == participantId);
        if (existing != null && existing.Status != Cancelled)
            return Conflict(new { message = "You are already enrolled in this category." });

        var activeCount = await _db.Enrolments.CountAsync(en =>
            en.CategoryID == categoryId && en.Status != Cancelled);
        if (category.MaxParticipants.HasValue && activeCount >= category.MaxParticipants.Value)
            return Conflict(new { message = "This category is full." });

        Enrolment enrolment;
        if (existing != null)
        {
            // Re-enrolling after a cancellation re-activates the old row,
            // because (ParticipantID, CategoryID) is unique.
            existing.Status = "Confirmed";
            existing.EnrolmentDate = DateTime.Now;
            enrolment = existing;
        }
        else
        {
            enrolment = new Enrolment
            {
                ParticipantID = participantId,
                CategoryID = categoryId,
                Status = "Confirmed"
            };
            _db.Enrolments.Add(enrolment);
        }

        try
        {
            await _db.SaveChangesAsync();

            // The bib number is built from the identity value, so it is unique
            // even if two people enrol at the same moment.
            enrolment.BibNumber ??= $"B{enrolment.EnrolmentID:D5}";
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Two simultaneous requests hit the unique constraint
            return Conflict(new { message = "You are already enrolled in this category." });
        }

        var saved = await WithEvent().AsNoTracking()
            .FirstAsync(en => en.EnrolmentID == enrolment.EnrolmentID);
        return CreatedAtAction(nameof(GetById), new { id = saved.EnrolmentID }, ToDto(saved));
    }

    // GET /api/participants/5/enrolments
    [Authorize(Roles = "Participant")]
    [HttpGet("api/participants/{id:int}/enrolments")]
    public async Task<IActionResult> GetForParticipant(int id)
    {
        if (id != User.GetUserId())
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You can only view your own enrolments." });

        var list = await WithEvent().AsNoTracking()
            .Where(en => en.ParticipantID == id)
            .OrderByDescending(en => en.Category.Event.EventDate)
            .ToListAsync();

        return Ok(list.Select(ToDto));
    }

    // GET /api/enrolments/7
    [Authorize]
    [HttpGet("api/enrolments/{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var en = await WithEvent().AsNoTracking()
            .FirstOrDefaultAsync(x => x.EnrolmentID == id);
        if (en == null) return NotFound(new { message = "Enrolment not found." });

        var userId = User.GetUserId();
        var allowed = User.IsInRole("Participant")
            ? en.ParticipantID == userId
            : en.Category.Event.OrganiserID == userId;

        if (!allowed)
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You do not have access to this enrolment." });

        return Ok(ToDto(en));
    }

    // DELETE /api/enrolments/7   (cancels it; the row is kept for history)
    [Authorize(Roles = "Participant")]
    [HttpDelete("api/enrolments/{id:int}")]
    public async Task<IActionResult> Cancel(int id)
    {
        var en = await WithEvent().FirstOrDefaultAsync(x => x.EnrolmentID == id);
        if (en == null) return NotFound(new { message = "Enrolment not found." });

        if (en.ParticipantID != User.GetUserId())
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You can only cancel your own enrolments." });

        if (en.Status == Cancelled)
            return Conflict(new { message = "This enrolment is already cancelled." });

        if (en.Category.Event.EventDate < DateOnly.FromDateTime(DateTime.Today))
            return Conflict(new { message = "This event has already taken place." });

        en.Status = Cancelled;
        await _db.SaveChangesAsync();
        return Ok(new { message = "Enrolment cancelled." });
    }

    // GET /api/categories/3/enrolments?includeCancelled=false
    [Authorize(Roles = "Organiser")]
    [HttpGet("api/categories/{categoryId:int}/enrolments")]
    public async Task<IActionResult> GetRoster(int categoryId, [FromQuery] bool includeCancelled = false)
    {
        var category = await _db.Categories
            .Include(c => c.Event)
            .FirstOrDefaultAsync(c => c.CategoryID == categoryId);
        if (category == null) return NotFound(new { message = "Category not found." });

        if (category.Event.OrganiserID != User.GetUserId())
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You can only view the roster for your own events." });

        var query = _db.Enrolments.AsNoTracking().Where(en => en.CategoryID == categoryId);
        if (!includeCancelled)
            query = query.Where(en => en.Status != Cancelled);

        var roster = await query
            .OrderBy(en => en.EnrolmentID)
            .Select(en => new RosterEntryDto(
                en.EnrolmentID, en.ParticipantID, en.Participant.FullName,
                en.EnrolmentDate, en.Status, en.BibNumber))
            .ToListAsync();

        return Ok(roster);
    }
}