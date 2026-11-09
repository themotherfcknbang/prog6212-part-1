using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RaceDay.Api.Services;
using Xunit;

namespace RaceDay.Tests;

public class ResultTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;

    public ResultTests(RaceDayFactory factory) => _factory = factory;

    private static object Finished(int position = 1) =>
        new { status = "Finished", finishTime = "01:52:30", position };

    [Fact]
    public async Task RecordResult_BeforeTheEvent_Returns400()
    {
        var s = await Helpers.CreateScenarioAsync(_factory);
        var enrolmentId = await Helpers.EnrolAsync(s.Participant.Client, s.CategoryId);

        var response = await s.Organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/results", Finished());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RecordResult_AfterTheEvent_Returns201()
    {
        var s = await Helpers.CreateScenarioAsync(_factory);
        var enrolmentId = await Helpers.EnrolAsync(s.Participant.Client, s.CategoryId);
        await _factory.MakeEventPastAsync(s.EventId);

        var response = await s.Organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/results", Finished());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task RecordResult_Twice_Returns409()
    {
        var s = await Helpers.CreateScenarioAsync(_factory);
        var enrolmentId = await Helpers.EnrolAsync(s.Participant.Client, s.CategoryId);
        await _factory.MakeEventPastAsync(s.EventId);
        await s.Organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/results", Finished());

        var again = await s.Organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/results", Finished());

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task RecordResult_FinishedWithoutFinishTime_Returns400()
    {
        var s = await Helpers.CreateScenarioAsync(_factory);
        var enrolmentId = await Helpers.EnrolAsync(s.Participant.Client, s.CategoryId);
        await _factory.MakeEventPastAsync(s.EventId);

        var response = await s.Organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/results",
            new { status = "Finished" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RecordResult_AsDifferentOrganiser_Returns403()
    {
        var s = await Helpers.CreateScenarioAsync(_factory);
        var enrolmentId = await Helpers.EnrolAsync(s.Participant.Client, s.CategoryId);
        await _factory.MakeEventPastAsync(s.EventId);
        var other = await Helpers.RegisterOrganiserAsync(_factory);

        var response = await other.Client.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/results", Finished());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CanViewOwnResultsButNotSomeoneElses()
    {
        var s = await Helpers.CreateScenarioAsync(_factory);
        var enrolmentId = await Helpers.EnrolAsync(s.Participant.Client, s.CategoryId);
        await _factory.MakeEventPastAsync(s.EventId);
        await s.Organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/results", Finished(7));
        var other = await Helpers.RegisterParticipantAsync(_factory);

        var own = await s.Participant.Client
            .GetFromJsonAsync<JsonElement>($"/api/participants/{s.Participant.Id}/results");
        var forbidden = await other.Client.GetAsync($"/api/participants/{s.Participant.Id}/results");

        Assert.Equal(1, own.GetArrayLength());
        Assert.Equal(7, own[0].GetProperty("position").GetInt32());
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task EventResults_ArePublic()
    {
        var s = await Helpers.CreateScenarioAsync(_factory);
        var enrolmentId = await Helpers.EnrolAsync(s.Participant.Client, s.CategoryId);
        await _factory.MakeEventPastAsync(s.EventId);
        await s.Organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/results", Finished());

        var results = await _factory.NewClient().GetFromJsonAsync<JsonElement>($"/api/events/{s.EventId}/results");

        Assert.Equal(1, results.GetArrayLength());
    }
}

public class WeatherTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;

    public WeatherTests(RaceDayFactory factory) => _factory = factory;

    // A client whose API uses a fake weather service instead of calling the internet
    private HttpClient ClientWith(Func<WeatherInfo?> behaviour) =>
        _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IWeatherService>();
            services.AddSingleton<IWeatherService>(new FakeWeatherService(behaviour));
        })).CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

    private async Task<int> NewEventAsync()
    {
        var organiser = await Helpers.RegisterOrganiserAsync(_factory);
        return await Helpers.CreateEventAsync(organiser.Client);
    }

    [Fact]
    public async Task GetWeather_ForecastAvailable_Returns200()
    {
        var eventId = await NewEventAsync();
        var client = ClientWith(() => new WeatherInfo(
            DateOnly.FromDateTime(DateTime.Today.AddDays(5)), "forecast", 22.5, 15, 30, 12, "Clear sky", null));

        var response = await client.GetAsync($"/api/events/{eventId}/weather");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Clear sky", body.GetProperty("conditions").GetString());
    }

    [Fact]
    public async Task GetWeather_LocationNotFound_Returns404()
    {
        var eventId = await NewEventAsync();
        var client = ClientWith(() => null);

        var response = await client.GetAsync($"/api/events/{eventId}/weather");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetWeather_ServiceDown_Returns502()
    {
        var eventId = await NewEventAsync();
        var client = ClientWith(() => throw new WeatherUnavailableException("down"));

        var response = await client.GetAsync($"/api/events/{eventId}/weather");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task GetWeather_UnknownEvent_Returns404()
    {
        var client = ClientWith(() => null);

        var response = await client.GetAsync("/api/events/999999/weather");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}