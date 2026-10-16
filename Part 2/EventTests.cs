using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace RaceDay.Tests;

public class EventTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;

    public EventTests(RaceDayFactory factory) => _factory = factory;

    [Fact]
    public async Task GetEvents_WithoutLogin_Returns200()
    {
        var response = await _factory.NewClient().GetAsync("/api/events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetEvent_UnknownId_Returns404()
    {
        var response = await _factory.NewClient().GetAsync("/api/events/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_AsOrganiser_Returns201()
    {
        var organiser = await Helpers.RegisterOrganiserAsync(_factory);

        var response = await organiser.Client.PostAsJsonAsync("/api/events", Helpers.EventBody("Soweto Marathon"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task CreateEvent_DateInThePast_Returns400()
    {
        var organiser = await Helpers.RegisterOrganiserAsync(_factory);
        var yesterday = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd");

        var response = await organiser.Client.PostAsJsonAsync("/api/events",
            Helpers.EventBody(date: yesterday));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_MissingName_Returns400()
    {
        var organiser = await Helpers.RegisterOrganiserAsync(_factory);

        var response = await organiser.Client.PostAsJsonAsync("/api/events", Helpers.EventBody(name: ""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateEvent_AsOwner_Returns200AndChangesAreSaved()
    {
        var organiser = await Helpers.RegisterOrganiserAsync(_factory);
        var eventId = await Helpers.CreateEventAsync(organiser.Client);

        var update = await organiser.Client.PutAsJsonAsync($"/api/events/{eventId}",
            Helpers.EventBody("Renamed Marathon"));
        var read = await _factory.NewClient().GetFromJsonAsync<JsonElement>($"/api/events/{eventId}");

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("Renamed Marathon", read.GetProperty("eventName").GetString());
    }

    [Fact]
    public async Task UpdateEvent_AsDifferentOrganiser_Returns403()
    {
        var owner = await Helpers.RegisterOrganiserAsync(_factory);
        var other = await Helpers.RegisterOrganiserAsync(_factory);
        var eventId = await Helpers.CreateEventAsync(owner.Client);

        var response = await other.Client.PutAsJsonAsync($"/api/events/{eventId}",
            Helpers.EventBody("Hijacked"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_AsDifferentOrganiser_Returns403()
    {
        var owner = await Helpers.RegisterOrganiserAsync(_factory);
        var other = await Helpers.RegisterOrganiserAsync(_factory);
        var eventId = await Helpers.CreateEventAsync(owner.Client);

        var response = await other.Client.DeleteAsync($"/api/events/{eventId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_AsOwner_Returns200ThenEventIsGone()
    {
        var organiser = await Helpers.RegisterOrganiserAsync(_factory);
        var eventId = await Helpers.CreateEventAsync(organiser.Client);

        var delete = await organiser.Client.DeleteAsync($"/api/events/{eventId}");
        var read = await _factory.NewClient().GetAsync($"/api/events/{eventId}");

        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_WithActiveEnrolment_Returns409()
    {
        var s = await Helpers.CreateScenarioAsync(_factory);
        await Helpers.EnrolAsync(s.Participant.Client, s.CategoryId);

        var response = await s.Organiser.Client.DeleteAsync($"/api/events/{s.EventId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GetEvents_LocationFilter_ReturnsOnlyMatchingEvents()
    {
        var organiser = await Helpers.RegisterOrganiserAsync(_factory);
        var location = $"Loc{Guid.NewGuid():N}";
        await organiser.Client.PostAsJsonAsync("/api/events", Helpers.EventBody(location: location));
        await organiser.Client.PostAsJsonAsync("/api/events", Helpers.EventBody(location: "Elsewhere"));

        var list = await _factory.NewClient().GetFromJsonAsync<JsonElement>($"/api/events?location={location}");

        Assert.Equal(1, list.GetArrayLength());
    }
}