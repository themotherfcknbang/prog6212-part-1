using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[ApiController]
public class WeatherController : ControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly IWeatherService _weather;

    public WeatherController(RaceDayDbContext db, IWeatherService weather)
    {
        _db = db;
        _weather = weather;
    }

    // GET /api/events/5/weather
    [HttpGet("api/events/{id:int}/weather")]
    public async Task<IActionResult> GetWeather(int id, CancellationToken ct)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventID == id, ct);
        if (ev == null) return NotFound(new { message = "Event not found." });

        try
        {
            var w = await _weather.GetWeatherAsync(ev.Location, ev.EventDate, ct);
            if (w == null)
                return NotFound(new { message = "Could not find map coordinates for this event's location." });

            return Ok(new WeatherDto(ev.EventID, ev.EventName, ev.Location,
                w.ForecastDate, w.ForecastType, w.Temperature, w.TemperatureMin,
                w.TemperatureMax, w.WindSpeed, w.Conditions, w.Note));
        }
        catch (WeatherUnavailableException)
        {
            return StatusCode(StatusCodes.Status502BadGateway,
                new { message = "The weather service is currently unavailable. Please try again later." });
        }
    }
}