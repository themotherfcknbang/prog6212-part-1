using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;
using RaceDay.Api.Services;

namespace RaceDay.Tests;

// Starts the real API in memory, with an in-memory database instead of SQL Server
public class RaceDayFactory : WebApplicationFactory<Program>
{
    private static readonly InMemoryDatabaseRoot Root = new();
    private readonly string _dbName = $"RaceDayTests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.AddDbContext<RaceDayDbContext>(o => o.UseInMemoryDatabase(_dbName, Root));
        });
    }

    // Each client keeps its own cookies, so each one acts as a separate logged-in user
    public HttpClient NewClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    public async Task<T> QueryDbAsync<T>(Func<RaceDayDbContext, Task<T>> query)
    {
        using var scope = Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<RaceDayDbContext>());
    }

    public async Task ModifyDbAsync(Func<RaceDayDbContext, Task> change)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
        await change(db);
        await db.SaveChangesAsync();
    }

    public Task MakeEventPastAsync(int eventId) => ModifyDbAsync(async db =>
    {
        var ev = await db.Events.FindAsync(eventId);
        ev!.EventDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));
    });
}

// A weather service that returns whatever the test tells it to, so tests never touch the internet
public class FakeWeatherService : IWeatherService
{
    private readonly Func<WeatherInfo?> _behaviour;
    public FakeWeatherService(Func<WeatherInfo?> behaviour) => _behaviour = behaviour;

    public Task<WeatherInfo?> GetWeatherAsync(string location, DateOnly date, CancellationToken ct = default) =>
        Task.FromResult(_behaviour());
}

public record TestUser(HttpClient Client, int Id, string Email);

public record Scenario(TestUser Organiser, TestUser Participant, int EventId, int CategoryId);

public static class Helpers
{
    public static string Email(string prefix) => $"{prefix}-{Guid.NewGuid():N}@test.com";

    public static string FutureDate(int days = 30) =>
        DateTime.Today.AddDays(days).ToString("yyyy-MM-dd");

    public static object EventBody(string name = "Test Marathon", string? date = null,
        string location = "Johannesburg") => new
        {
            eventName = name,
            eventDate = date ?? FutureDate(),
            location,
            description = "Test event",
            routeDescription = "Test route",
            startTime = "06:00:00"
        };

    public static async Task<int> ReadIntAsync(HttpResponseMessage response, string property) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty(property).GetInt32();

    // Registering also logs the client in, so the returned client is ready to use
    public static async Task<TestUser> RegisterOrganiserAsync(RaceDayFactory factory)
    {
        var client = factory.NewClient();
        var email = Email("organiser");
        var response = await client.PostAsJsonAsync("/api/auth/register/organiser", new
        {
            fullName = "Test Organiser",
            email,
            password = "Password123",
            organisationName = "Test Events",
            phoneNumber = "0821234567"
        });
        response.EnsureSuccessStatusCode();
        return new TestUser(client, await ReadIntAsync(response, "userId"), email);
    }

    public static async Task<TestUser> RegisterParticipantAsync(RaceDayFactory factory)
    {
        var client = factory.NewClient();
        var email = Email("participant");
        var response = await client.PostAsJsonAsync("/api/auth/register/participant", new
        {
            fullName = "Test Participant",
            email,
            password = "Password123",
            dateOfBirth = "1995-06-15",
            gender = "Female",
            emergencyContactName = "Test Contact",
            emergencyContactPhone = "0711112222"
        });
        response.EnsureSuccessStatusCode();
        return new TestUser(client, await ReadIntAsync(response, "userId"), email);
    }

    public static async Task<int> CreateEventAsync(HttpClient organiser, string name = "Test Marathon")
    {
        var response = await organiser.PostAsJsonAsync("/api/events", EventBody(name));
        response.EnsureSuccessStatusCode();
        return await ReadIntAsync(response, "eventID");
    }

    public static async Task<int> CreateCategoryAsync(HttpClient organiser, int eventId, int? maxParticipants = null)
    {
        var response = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories", new
        {
            categoryName = "10km Fun Run",
            distanceKM = 10,
            entryFee = 150,
            maxParticipants
        });
        response.EnsureSuccessStatusCode();
        return await ReadIntAsync(response, "categoryID");
    }

    public static async Task<int> EnrolAsync(HttpClient participant, int categoryId)
    {
        var response = await participant.PostAsync($"/api/categories/{categoryId}/enrolments", null);
        response.EnsureSuccessStatusCode();
        return await ReadIntAsync(response, "enrolmentID");
    }

    // An organiser with an event and a category, plus a participant, all logged in
    public static async Task<Scenario> CreateScenarioAsync(RaceDayFactory factory, int? maxParticipants = null)
    {
        var organiser = await RegisterOrganiserAsync(factory);
        var participant = await RegisterParticipantAsync(factory);
        var eventId = await CreateEventAsync(organiser.Client);
        var categoryId = await CreateCategoryAsync(organiser.Client, eventId, maxParticipants);
        return new Scenario(organiser, participant, eventId, categoryId);
    }
}