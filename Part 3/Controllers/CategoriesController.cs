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
public class CategoriesController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    public CategoriesController(RaceDayDbContext db) => _db = db;

    private static readonly Expression<Func<Category, CategoryDto>> ToDtoExpr = c =>
        new CategoryDto(c.CategoryID, c.EventID, c.CategoryName,
            c.DistanceKM, c.EntryFee, c.MaxParticipants);

    private static CategoryDto ToDto(Category c) =>
        new(c.CategoryID, c.EventID, c.CategoryName,
            c.DistanceKM, c.EntryFee, c.MaxParticipants);

    // GET /api/events/5/categories
    [HttpGet("api/events/{eventId:int}/categories")]
    public async Task<IActionResult> GetForEvent(int eventId)
    {
        if (!await _db.Events.AnyAsync(e => e.EventID == eventId))
            return NotFound(new { message = "Event not found." });

        var categories = await _db.Categories.AsNoTracking()
            .Where(c => c.EventID == eventId)
            .OrderBy(c => c.DistanceKM)
            .Select(ToDtoExpr)
            .ToListAsync();

        return Ok(categories);
    }

    // GET /api/categories/3
    [HttpGet("api/categories/{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var category = await _db.Categories.AsNoTracking()
            .Where(c => c.CategoryID == id)
            .Select(ToDtoExpr)
            .FirstOrDefaultAsync();

        if (category == null) return NotFound(new { message = "Category not found." });
        return Ok(category);
    }

    // POST /api/events/5/categories
    [Authorize(Roles = "Organiser")]
    [HttpPost("api/events/{eventId:int}/categories")]
    public async Task<IActionResult> Create(int eventId, CategoryRequestDto dto)
    {
        var ev = await _db.Events.FindAsync(eventId);
        if (ev == null) return NotFound(new { message = "Event not found." });
        if (ev.OrganiserID != User.GetUserId())
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You can only add categories to your own events." });

        var category = new Category
        {
            EventID = eventId,
            CategoryName = dto.CategoryName.Trim(),
            DistanceKM = dto.DistanceKM,
            EntryFee = dto.EntryFee,
            MaxParticipants = dto.MaxParticipants
        };
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = category.CategoryID }, ToDto(category));
    }

    // PUT /api/categories/3
    [Authorize(Roles = "Organiser")]
    [HttpPut("api/categories/{id:int}")]
    public async Task<IActionResult> Update(int id, CategoryRequestDto dto)
    {
        var category = await _db.Categories
            .Include(c => c.Event)
            .FirstOrDefaultAsync(c => c.CategoryID == id);

        if (category == null) return NotFound(new { message = "Category not found." });
        if (category.Event.OrganiserID != User.GetUserId())
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You can only modify categories on your own events." });

        // Don't let the capacity drop below people who are already enrolled
        if (dto.MaxParticipants.HasValue)
        {
            var active = await _db.Enrolments
                .CountAsync(en => en.CategoryID == id && en.Status != "Cancelled");
            if (dto.MaxParticipants.Value < active)
                return Conflict(new
                {
                    message = $"Maximum participants cannot be lower than the {active} active enrolments."
                });
        }

        category.CategoryName = dto.CategoryName.Trim();
        category.DistanceKM = dto.DistanceKM;
        category.EntryFee = dto.EntryFee;
        category.MaxParticipants = dto.MaxParticipants;

        await _db.SaveChangesAsync();
        return Ok(ToDto(category));
    }

    // DELETE /api/categories/3
    [Authorize(Roles = "Organiser")]
    [HttpDelete("api/categories/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await _db.Categories
            .Include(c => c.Event)
            .FirstOrDefaultAsync(c => c.CategoryID == id);

        if (category == null) return NotFound(new { message = "Category not found." });
        if (category.Event.OrganiserID != User.GetUserId())
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You can only delete categories on your own events." });

        var enrolments = await _db.Enrolments
            .Where(en => en.CategoryID == id)
            .ToListAsync();

        if (enrolments.Any(en => en.Status != "Cancelled"))
            return Conflict(new { message = "This category has active enrolments and cannot be deleted." });

        // Cancelled enrolments go first, because the Enrolments -> Categories
        // foreign key is NO ACTION and would otherwise block the delete.
        if (enrolments.Count > 0)
        {
            _db.Enrolments.RemoveRange(enrolments);
            await _db.SaveChangesAsync();
        }

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Category deleted." });
    }
}