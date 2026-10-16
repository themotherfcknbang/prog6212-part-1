using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RaceDay.Tests;

public class RoleAccessTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;

    public RoleAccessTests(RaceDayFactory factory) => _factory = factory;

    [Fact]
    public async Task Unauthenticated_CannotCreateEvent_Returns401()
    {
        var response = await _factory.NewClient().PostAsJsonAsync("/api/events", Helpers.EventBody());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotCreateEvent_Returns403()
    {
        var participant = await Helpers.RegisterParticipantAsync(_factory);

        var response = await participant.Client.PostAsJsonAsync("/api/events", Helpers.EventBody());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotAddCategory_Returns403()
    {
        var s = await Helpers.CreateScenarioAsync(_factory);

        var response = await s.Participant.Client.PostAsJsonAsync($"/api/events/{s.EventId}/categories",
            new { categoryName = "5km", distanceKM = 5, entryFee = 50 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_CannotEnrol_Returns401()
    {
        var s = await Helpers.CreateScenarioAsync(_factory);

        var response = await _factory.NewClient().PostAsync($"/api/categories/{s.CategoryId}/enrolments", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotEnrol_Returns403()
    {
        var s = await Helpers.CreateScenarioAsync(_factory);

        var response = await s.Organiser.Client.PostAsync($"/api/categories/{s.CategoryId}/enrolments", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotViewRoster_Returns403()
    {
        var s = await Helpers.CreateScenarioAsync(_factory);

        var response = await s.Participant.Client.GetAsync($"/api/categories/{s.CategoryId}/enrolments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotViewParticipantEnrolments_Returns403()
    {
        var s = await Helpers.CreateScenarioAsync(_factory);

        var response = await s.Organiser.Client.GetAsync($"/api/participants/{s.Participant.Id}/enrolments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotRecordResult_Returns403()
    {
        var s = await Helpers.CreateScenarioAsync(_factory);
        var enrolmentId = await Helpers.EnrolAsync(s.Participant.Client, s.CategoryId);

        var response = await s.Participant.Client.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/results",
            new { status = "Finished", finishTime = "01:52:30", position = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}