using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace RaceDay.Tests;

public class AuthTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;

    public AuthTests(RaceDayFactory factory) => _factory = factory;

    [Fact]
    public async Task RegisterOrganiser_ValidDetails_Returns201AndOrganiserRole()
    {
        var response = await _factory.NewClient().PostAsJsonAsync("/api/auth/register/organiser", new
        {
            fullName = "Test Organiser",
            email = Helpers.Email("org"),
            password = "Password123",
            organisationName = "Test Events"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Organiser", body.GetProperty("role").GetString());
    }

    [Fact]
    public async Task RegisterParticipant_ValidDetails_Returns201AndParticipantRole()
    {
        var response = await _factory.NewClient().PostAsJsonAsync("/api/auth/register/participant", new
        {
            fullName = "Test Participant",
            email = Helpers.Email("part"),
            password = "Password123",
            dateOfBirth = "1995-06-15"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Participant", body.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Register_EmailAlreadyUsed_Returns409ForBothRoles()
    {
        var organiser = await Helpers.RegisterOrganiserAsync(_factory);

        var sameRole = await _factory.NewClient().PostAsJsonAsync("/api/auth/register/organiser", new
        {
            fullName = "Copy",
            email = organiser.Email,
            password = "Password123",
            organisationName = "Copy"
        });
        var otherRole = await _factory.NewClient().PostAsJsonAsync("/api/auth/register/participant", new
        {
            fullName = "Copy",
            email = organiser.Email,
            password = "Password123",
            dateOfBirth = "1995-06-15"
        });

        Assert.Equal(HttpStatusCode.Conflict, sameRole.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, otherRole.StatusCode);
    }

    [Fact]
    public async Task Register_PasswordTooShort_Returns400()
    {
        var response = await _factory.NewClient().PostAsJsonAsync("/api/auth/register/organiser", new
        {
            fullName = "Test Organiser",
            email = Helpers.Email("org"),
            password = "abc",
            organisationName = "Test Events"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_StoresPasswordAsHashNotPlainText()
    {
        var user = await Helpers.RegisterOrganiserAsync(_factory);

        var stored = await _factory.QueryDbAsync(db =>
            db.Organisers.Where(o => o.Email == user.Email).Select(o => o.PasswordHash).SingleAsync());

        Assert.NotEqual("Password123", stored);
        Assert.StartsWith("$2", stored);   // BCrypt hashes start with $2a$ or $2b$
    }

    [Fact]
    public async Task Login_CorrectCredentials_Returns200AndRole()
    {
        var user = await Helpers.RegisterOrganiserAsync(_factory);

        var response = await _factory.NewClient().PostAsJsonAsync("/api/auth/login",
            new { email = user.Email, password = "Password123" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Organiser", body.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var user = await Helpers.RegisterOrganiserAsync(_factory);

        var response = await _factory.NewClient().PostAsJsonAsync("/api/auth/login",
            new { email = user.Email, password = "WrongPassword1" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401()
    {
        var response = await _factory.NewClient().PostAsJsonAsync("/api/auth/login",
            new { email = Helpers.Email("nobody"), password = "Password123" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedRoute_WithoutLogin_Returns401()
    {
        var response = await _factory.NewClient().GetAsync("/api/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_KeepsSessionForLaterRequests()
    {
        var user = await Helpers.RegisterParticipantAsync(_factory);
        var client = _factory.NewClient();
        await client.PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = "Password123" });

        var profile = await client.GetAsync("/api/profile");

        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        var body = await profile.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Participant", body.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Logout_EndsTheSession()
    {
        var user = await Helpers.RegisterOrganiserAsync(_factory);

        var logout = await user.Client.PostAsync("/api/auth/logout", null);
        var profileAfter = await user.Client.GetAsync("/api/profile");

        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, profileAfter.StatusCode);
    }
}