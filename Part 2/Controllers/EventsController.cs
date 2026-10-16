using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Extensions;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

[ApiController]
public class EventsController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    public EventsController(RaceDayDbContext db) => _db = db;

    private static readonly Expression<Func<Event, EventSummaryDto>> ToSummary = e =>
        new EventSummaryDto(e.EventID, e.EventName, e.EventDate, e.Location,
            e.StartTime, e.Organiser.OrganisationName, e.Categories.Count);

    // GET /api/events?location=&keyword=&from=&to=
    [HttpGet("api/events")]
    public async Task<IActionResult> GetEvents(
        [FromQuery] string? location,
        [FromQuery] string? keyword,
        [FromQuery(Name = "from")] DateOnly? fromDate,
        [FromQuery(Name = "to")] DateOnly? toDate)
    {
        var start = fromDate ?? DateOnly.FromDateTime(DateTime.Today);
        if (toDate.HasValue && toDate.Value < start)
            return BadRequest(new { message = "'to' cannot be earlier than the start date." });

        var query = _db.Events.AsNoTracking().Where(e => e.EventDate >= start);

        if (toDate.HasValue)
            query = query.Where(e => e.EventDate <= toDate.Value);
        if (!string.IsNullOrWhiteSpace(location))
            query = query.Where(e => e.Location.Contains(location));
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(e => e.EventName.Contains(keyword) ||
                                     (e.Description != null && e.Description.Contains(keyword)));

        var events = await query
            .OrderBy(e => e.EventDate).ThenBy(e => e.StartTime)
            .Select(ToSummary)
            .ToListAsync();

        return Ok(events);
    }

    // GET /api/events/5
    [HttpGet("api/events/{id:int}")]
    public async Task<IActionResult> GetEvent(int id)
    {
        var ev = await _db.Events.AsNoTracking()
            .Include(e => e.Organiser)
            .Include(e => e.Categories)
            .FirstOrDefaultAsync(e => e.EventID == id);

        if (ev == null) return NotFound(new { message = "Event not found." });
        return Ok(ToDetail(ev));
    }

    // POST /api/events
    [Authorize(Roles = "Organiser")]
    [HttpPost("api/events")]
    public async Task<IActionResult> CreateEvent(EventRequestDto dto)
    {
        if (dto.EventDate!.Value < DateOnly.FromDateTime(DateTime.Today))
            return BadRequest(new { message = "Event date cannot be in the past." });

        var ev = new Event
        {
            OrganiserID = User.GetUserId(),
            EventName = dto.EventName.Trim(),
            EventDate = dto.EventDate.Value,
            Location = dto.Location.Trim(),
            Description = dto.Description,
            RouteDescription = dto.RouteDescription,
            StartTime = dto.StartTime
        };
        _db.Events.Add(ev);
        await _db.SaveChangesAsync();
        await _db.Entry(ev).Reference(e => e.Organiser).LoadAsync();

        return CreatedAtAction(nameof(GetEvent), new { id = ev.EventID }, ToDetail(ev));
    }

    // PUT /api/events/5
    [Authorize(Roles = "Organiser")]
    [HttpPut("api/events/{id:int}")]
    public async Task<IActionResult> UpdateEvent(int id, EventRequestDto dto)
    {
        var ev = await _db.Events
            .Include(e => e.Organiser)
            .Include(e => e.Categories)
            .FirstOrDefaultAsync(e => e.EventID == id);

        if (ev == null) return NotFound(new { message = "Event not found." });
        if (ev.OrganiserID != User.GetUserId())
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You can only modify your own events." });

        var newDate = dto.EventDate!.Value;
        if (newDate != ev.EventDate && newDate < DateOnly.FromDateTime(DateTime.Today))
            return BadRequest(new { message = "Event date cannot be moved into the past." });

        ev.EventName = dto.EventName.Trim();
        ev.EventDate = newDate;
        ev.Location = dto.Location.Trim();
        ev.Description = dto.Description;
        ev.RouteDescription = dto.RouteDescription;
        ev.StartTime = dto.StartTime;

        await _db.SaveChangesAsync();
        return Ok(ToDetail(ev));
    }

    // DELETE /api/events/5
    [Authorize(Roles = "Organiser")]
    [HttpDelete("api/events/{id:int}")]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var ev = await _db.Events.FindAsync(id);
        if (ev == null) return NotFound(new { message = "Event not found." });
        if (ev.OrganiserID != User.GetUserId())
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You can only delete your own events." });

        var enrolments = await _db.Enrolments
            .Where(en => en.Category.EventID == id)
            .ToListAsync();

        if (enrolments.Any(en => en.Status != "Cancelled"))
            return Conflict(new { message = "This event has active enrolments and cannot be deleted." });

        // Cancelled enrolments must go first: the Enrolments -> Categories
        // foreign key is NO ACTION, so it would otherwise block the delete.
        if (enrolments.Count > 0)
        {
            _db.Enrolments.RemoveRange(enrolments);
            await _db.SaveChangesAsync();
        }

        _db.Events.Remove(ev);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Event deleted." });
    }

    // GET /api/organisers/3/events
    [HttpGet("api/organisers/{id:int}/events")]
    public async Task<IActionResult> GetOrganiserEvents(int id)
    {
        if (!await _db.Organisers.AnyAsync(o => o.OrganiserID == id))
            return NotFound(new { message = "Organiser not found." });

        var events = await _db.Events.AsNoTracking()
            .Where(e => e.OrganiserID == id)
            .OrderByDescending(e => e.EventDate)
            .Select(ToSummary)
            .ToListAsync();

        return Ok(events);
    }

    private static EventDetailDto ToDetail(Event e) =>
        new(e.EventID, e.OrganiserID, e.Organiser.OrganisationName, e.EventName,
            e.EventDate, e.Location, e.Description, e.RouteDescription,
            e.StartTime, e.CreatedAt,
            e.Categories.OrderBy(c => c.DistanceKM)
                .Select(c => new CategoryDto(c.CategoryID, c.EventID, c.CategoryName,
                    c.DistanceKM, c.EntryFee, c.MaxParticipants))
                .ToList());
}