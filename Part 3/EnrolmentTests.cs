using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace RaceDay.Tests;

public class EnrolmentTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;

    public EnrolmentTests(RaceDayFactory factory) => _factory = factory;

    [Fact]
    public async Task Enrol_AsParticipant_Returns201AndLinksParticipantEventAndCategory()
    {
        var s = await Helpers.CreateScenarioAsync(_factory);

        var response = await s.Participant.Client.PostAsync($"/api/categories/{s.CategoryId}/enrolments", null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(s.Participant.Id, body.GetProperty("participantID").GetInt32());
        Assert.Equal(s.CategoryId, body.GetProperty("categoryID").GetInt32());
        Assert.Equal(s.EventId, body.GetProperty("eventID").GetInt32());
        Assert.Equal("Confirmed", body.GetProperty("status").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("bibNumber").GetString()));
    }

    [Fact]
    public async Task Enrol_IsRecordedInTheDatabase()
    {
        var s = await Helpers.CreateScenarioAsync(_factory);
        await Helpers.EnrolAsync(s.Participant.Client, s.CategoryId);

        var recorded = await _factory.QueryDbAsync(db => db.Enrolments.AnyAsync(e =>
            e.ParticipantID == s.Participant.Id &&
            e.CategoryID == s.CategoryId &&
            e.Status == "Confirmed"));

        Assert.True(recorded);
    }

    [Fact]
    public async Task Enrol_SameCategoryTwice_Returns409()
    {
        var s = await Helpers.CreateScenarioAsync(_factory);
        await Helpers.EnrolAsync(s.Participant.Client, s.CategoryId);

        var again = await s.Participant.Client.PostAsync($"/api/categories/{s.CategoryId}/enrolments", null);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Enrol_CategoryIsFull_Returns409()
    {
        var s = await Helpers.CreateScenarioAsync(_factory, maxParticipants: 1);
        await Helpers.EnrolAsync(s.Participant.Client, s.CategoryId);
        var second = await Helpers.RegisterParticipantAsync(_factory);

        var response = await second.Client.PostAsync($"/api/categories/{s.CategoryId}/enrolments", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_UnknownCategory_Returns404()
    {
        var participant = await Helpers.RegisterParticipantAsync(_factory);

        var response = await participant.Client.PostAsync("/api/categories/999999/enrolments", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ParticipantEnrolments_ListsTheirOwnEnrolment()
    {
        var s = await Helpers.CreateScenarioAsync(_factory);
        await Helpers.EnrolAsync(s.Participant.Client, s.CategoryId);

        var list = await s.Participant.Client
            .GetFromJsonAsync<JsonElement>($"/api/participants/{s.Participant.Id}/enrolments");

        Assert.Equal(1, list.GetArrayLength());
    }

    [Fact]
    public async Task ParticipantEnrolments_AnotherParticipantsId_Returns403()
    {
        var s = await Helpers.CreateScenarioAsync(_factory);
        var other = await Helpers.RegisterParticipantAsync(_factory);

        var response = await other.Client.GetAsync($"/api/participants/{s.Participant.Id}/enrolments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Roster_AsOwningOrganiser_ListsTheParticipant()
    {
        var s = await Helpers.CreateScenarioAsync(_factory);
        await Helpers.EnrolAsync(s.Participant.Client, s.CategoryId);

        var roster = await s.Organiser.Client
            .GetFromJsonAsync<JsonElement>($"/api/categories/{s.CategoryId}/enrolments");

        Assert.Equal(1, roster.GetArrayLength());
        Assert.Equal(s.Participant.Id, roster[0].GetProperty("participantID").GetInt32());
    }

    [Fact]
    public async Task Roster_AsDifferentOrganiser_Returns403()
    {
        var s = await Helpers.CreateScenarioAsync(_factory);
        var other = await Helpers.RegisterOrganiserAsync(_factory);

        var response = await other.Client.GetAsync($"/api/categories/{s.CategoryId}/enrolments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_OwnEnrolment_FreesPlaceAndAllowsReEnrolment()
    {
        var s = await Helpers.CreateScenarioAsync(_factory);
        var enrolmentId = await Helpers.EnrolAsync(s.Participant.Client, s.CategoryId);

        var cancel = await s.Participant.Client.DeleteAsync($"/api/enrolments/{enrolmentId}");
        var roster = await s.Organiser.Client
            .GetFromJsonAsync<JsonElement>($"/api/categories/{s.CategoryId}/enrolments");
        var again = await s.Participant.Client.PostAsync($"/api/categories/{s.CategoryId}/enrolments", null);

        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Equal(0, roster.GetArrayLength());
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
        Assert.Equal(enrolmentId, await Helpers.ReadIntAsync(again, "enrolmentID"));
    }

    [Fact]
    public async Task Cancel_AnotherParticipantsEnrolment_Returns403()
    {
        var s = await Helpers.CreateScenarioAsync(_factory);
        var enrolmentId = await Helpers.EnrolAsync(s.Participant.Client, s.CategoryId);
        var other = await Helpers.RegisterParticipantAsync(_factory);

        var response = await other.Client.DeleteAsync($"/api/enrolments/{enrolmentId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}