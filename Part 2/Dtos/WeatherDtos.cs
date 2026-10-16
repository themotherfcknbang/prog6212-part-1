namespace RaceDay.Api.Dtos;

public record WeatherDto(
    int EventID, string EventName, string Location,
    DateOnly ForecastDate, string ForecastType,
    double Temperature, double? TemperatureMin, double? TemperatureMax,
    double WindSpeed, string Conditions, string? Note);