using System.Globalization;
using System.Text.Json;

namespace RaceDay.Api.Services;

public record WeatherInfo(
    DateOnly ForecastDate, string ForecastType,
    double Temperature, double? TemperatureMin, double? TemperatureMax,
    double WindSpeed, string Conditions, string? Note);

public class WeatherUnavailableException : Exception
{
    public WeatherUnavailableException(string message, Exception? inner = null)
        : base(message, inner) { }
}

public interface IWeatherService
{
    // Returns null when the location can't be turned into coordinates.
    Task<WeatherInfo?> GetWeatherAsync(string location, DateOnly date, CancellationToken ct = default);
}

public class WeatherService : IWeatherService
{
    private readonly HttpClient _http;

    public WeatherService(HttpClient http) => _http = http;

    private record Place(double Lat, double Lon);

    public async Task<WeatherInfo?> GetWeatherAsync(string location, DateOnly date, CancellationToken ct = default)
    {
        try
        {
            var place = await GeocodeAsync(location, ct);
            if (place == null) return null;

            var today = DateOnly.FromDateTime(DateTime.Today);
            var withinForecast = date >= today && date <= today.AddDays(15);

            return withinForecast
                ? await GetDailyForecastAsync(place, date, ct)
                : await GetCurrentAsync(place, date, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested &&
            (ex is HttpRequestException or TaskCanceledException or JsonException
                or KeyNotFoundException or InvalidOperationException))
        {
            throw new WeatherUnavailableException("The weather service is currently unavailable.", ex);
        }
    }

    // "Soweto, Johannesburg" or "Pietermaritzburg to Durban" won't geocode as one
    // string, so we try the whole text first and then each piece in turn.
    private async Task<Place?> GeocodeAsync(string location, CancellationToken ct)
    {
        var candidates = new List<string> { location.Trim() };
        candidates.AddRange(location.Split(new[] { ",", " to " },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        foreach (var name in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var url = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(name)}&count=5&language=en&format=json";
            using var doc = JsonDocument.Parse(await _http.GetStringAsync(url, ct));

            if (!doc.RootElement.TryGetProperty("results", out var results) ||
                results.GetArrayLength() == 0)
                continue;

            // Prefer a South African match, otherwise take the first one
            var chosen = results[0];
            foreach (var r in results.EnumerateArray())
            {
                if (r.TryGetProperty("country_code", out var cc) && cc.GetString() == "ZA")
                {
                    chosen = r;
                    break;
                }
            }

            return new Place(chosen.GetProperty("latitude").GetDouble(),
                             chosen.GetProperty("longitude").GetDouble());
        }
        return null;
    }

    private async Task<WeatherInfo> GetDailyForecastAsync(Place place, DateOnly date, CancellationToken ct)
    {
        var d = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var url = string.Create(CultureInfo.InvariantCulture,
            $"https://api.open-meteo.com/v1/forecast?latitude={place.Lat}&longitude={place.Lon}" +
            $"&daily=weather_code,temperature_2m_max,temperature_2m_min,wind_speed_10m_max" +
            $"&timezone=auto&start_date={d}&end_date={d}");

        using var doc = JsonDocument.Parse(await _http.GetStringAsync(url, ct));
        var daily = doc.RootElement.GetProperty("daily");

        var max = daily.GetProperty("temperature_2m_max")[0].GetDouble();
        var min = daily.GetProperty("temperature_2m_min")[0].GetDouble();
        var wind = daily.GetProperty("wind_speed_10m_max")[0].GetDouble();
        var code = daily.GetProperty("weather_code")[0].GetInt32();

        return new WeatherInfo(date, "forecast",
            Math.Round((max + min) / 2, 1), Math.Round(min, 1), Math.Round(max, 1),
            Math.Round(wind, 1), Describe(code), null);
    }

    private async Task<WeatherInfo> GetCurrentAsync(Place place, DateOnly date, CancellationToken ct)
    {
        var url = string.Create(CultureInfo.InvariantCulture,
            $"https://api.open-meteo.com/v1/forecast?latitude={place.Lat}&longitude={place.Lon}" +
            $"&current=temperature_2m,wind_speed_10m,weather_code&timezone=auto");

        using var doc = JsonDocument.Parse(await _http.GetStringAsync(url, ct));
        var current = doc.RootElement.GetProperty("current");

        return new WeatherInfo(date, "current",
            Math.Round(current.GetProperty("temperature_2m").GetDouble(), 1), null, null,
            Math.Round(current.GetProperty("wind_speed_10m").GetDouble(), 1),
            Describe(current.GetProperty("weather_code").GetInt32()),
            "No forecast is available for this date (forecasts cover the next 16 days). Showing current conditions at the event location instead.");
    }

    // WMO weather interpretation codes used by Open-Meteo
    private static string Describe(int code) => code switch
    {
        0 => "Clear sky",
        1 => "Mainly clear",
        2 => "Partly cloudy",
        3 => "Overcast",
        45 or 48 => "Fog",
        51 or 53 or 55 => "Drizzle",
        56 or 57 => "Freezing drizzle",
        61 or 63 or 65 => "Rain",
        66 or 67 => "Freezing rain",
        71 or 73 or 75 => "Snow",
        77 => "Snow grains",
        80 or 81 or 82 => "Rain showers",
        85 or 86 => "Snow showers",
        95 => "Thunderstorm",
        96 or 99 => "Thunderstorm with hail",
        _ => "Unknown conditions"
    };
}