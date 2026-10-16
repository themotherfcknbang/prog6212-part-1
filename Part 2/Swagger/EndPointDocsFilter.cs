using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RaceDay.Api.Swagger;

public class EndpointDocsFilter : IOperationFilter
{
    private sealed record EndpointDoc(string Summary, string Description, (int Code, string Text)[] Responses);

    private static EndpointDoc D(string summary, string purpose, string who, string expects, string returns,
        params (int Code, string Text)[] responses) =>
        new(summary,
            $"{purpose}\n\n**Who can call it:** {who}\n\n**Expects:** {expects}\n\n**Returns:** {returns}",
            responses);

    private static readonly Dictionary<string, EndpointDoc> Docs = new()
    {
        // ---------- Authentication ----------
        ["POST api/auth/register/organiser"] = D("Register an organiser",
            "Creates a new Organiser account and logs the new user in.",
            "Anyone (public).",
            "fullName, email, password (minimum 8 characters), organisationName and an optional phoneNumber.",
            "The new user's id, name, email and role. A session cookie is issued, and the password is stored only as a BCrypt hash.",
            (201, "Created - account created and session started"),
            (400, "Bad Request - a field is missing or invalid"),
            (409, "Conflict - that email is already registered")),

        ["POST api/auth/register/participant"] = D("Register a participant",
            "Creates a new Participant account and logs the new user in.",
            "Anyone (public).",
            "fullName, email, password (minimum 8 characters), dateOfBirth (yyyy-MM-dd, not in the future) and optional gender, emergencyContactName and emergencyContactPhone.",
            "The new user's id, name, email and role. A session cookie is issued, and the password is stored only as a BCrypt hash.",
            (201, "Created - account created and session started"),
            (400, "Bad Request - a field is missing or invalid"),
            (409, "Conflict - that email is already registered")),

        ["POST api/auth/login"] = D("Log in",
            "Checks the email and password and starts a session for the user's role (Organiser or Participant).",
            "Anyone (public).",
            "email and password.",
            "The user's id, name, email and role. A session cookie named RaceDay.Session is set and sent automatically on later requests.",
            (200, "OK - logged in"),
            (401, "Unauthorized - invalid email or password")),

        ["POST api/auth/logout"] = D("Log out",
            "Ends the current session.",
            "Any logged-in user.",
            "No request body.",
            "A confirmation message.",
            (200, "OK - logged out"),
            (401, "Unauthorized - not logged in")),

        // ---------- Profile ----------
        ["GET api/profile"] = D("Get my profile",
            "Returns the logged-in user's own profile.",
            "Any logged-in user.",
            "No request body.",
            "An organiser profile (organisationName, phoneNumber) or a participant profile (dateOfBirth, gender, emergency contact). The password is never returned.",
            (200, "OK - profile returned"),
            (401, "Unauthorized - not logged in")),

        ["PUT api/profile"] = D("Update my profile",
            "Updates the logged-in user's own details. The email address cannot be changed.",
            "Any logged-in user.",
            "fullName (required), plus organisationName and phoneNumber for organisers, or dateOfBirth, gender, emergencyContactName and emergencyContactPhone for participants.",
            "The updated profile.",
            (200, "OK - profile updated"),
            (400, "Bad Request - a field is invalid"),
            (401, "Unauthorized - not logged in")),

        ["PUT api/profile/password"] = D("Change my password",
            "Replaces the logged-in user's password after checking the current one.",
            "Any logged-in user.",
            "currentPassword and newPassword (minimum 8 characters).",
            "A confirmation message. The new password is stored as a BCrypt hash.",
            (200, "OK - password updated"),
            (400, "Bad Request - the new password is too short"),
            (401, "Unauthorized - not logged in, or the current password is wrong")),

        // ---------- Events ----------
        ["GET api/events"] = D("List upcoming events",
            "Lists events happening from today onward, soonest first.",
            "Anyone (public).",
            "Optional query parameters: location and keyword (text match), from and to (yyyy-MM-dd).",
            "An array of event summaries with the organiser's name and the number of categories.",
            (200, "OK - events returned (possibly an empty list)"),
            (400, "Bad Request - 'to' is earlier than the start date")),

        ["GET api/events/{id}"] = D("Get event details",
            "Returns one event with its full details and its categories.",
            "Anyone (public).",
            "The event id in the URL.",
            "The event, including its route description and categories.",
            (200, "OK - event returned"),
            (404, "Not Found - the event does not exist")),

        ["POST api/events"] = D("Create an event",
            "Creates an event owned by the logged-in organiser.",
            "Organiser.",
            "eventName, eventDate (today or later), location, and optional description, routeDescription and startTime (HH:mm:ss).",
            "The created event.",
            (201, "Created - event created"),
            (400, "Bad Request - a field is invalid or the date is in the past"),
            (401, "Unauthorized - not logged in"),
            (403, "Forbidden - participants cannot create events")),

        ["PUT api/events/{id}"] = D("Update an event",
            "Changes an event's details.",
            "The Organiser who owns the event.",
            "The same fields as creating an event.",
            "The updated event.",
            (200, "OK - event updated"),
            (400, "Bad Request - a field is invalid or the date was moved into the past"),
            (401, "Unauthorized - not logged in"),
            (403, "Forbidden - not an organiser, or not the owner of this event"),
            (404, "Not Found - the event does not exist")),

        ["DELETE api/events/{id}"] = D("Delete an event",
            "Deletes an event and its categories. Cancelled enrolments are removed with it.",
            "The Organiser who owns the event.",
            "The event id in the URL.",
            "A confirmation message.",
            (200, "OK - event deleted"),
            (401, "Unauthorized - not logged in"),
            (403, "Forbidden - not an organiser, or not the owner of this event"),
            (404, "Not Found - the event does not exist"),
            (409, "Conflict - the event still has active enrolments")),

        ["GET api/organisers/{id}/events"] = D("List an organiser's events",
            "Lists every event created by one organiser, including past ones.",
            "Anyone (public).",
            "The organiser id in the URL.",
            "An array of event summaries, newest first.",
            (200, "OK - events returned"),
            (404, "Not Found - the organiser does not exist")),

        ["GET api/events/{id}/weather"] = D("Get event weather",
            "Looks up the weather for the event's location and date using the Open-Meteo service.",
            "Anyone (public).",
            "The event id in the URL.",
            "A daily forecast (temperature, min and max, wind speed in km/h, conditions) when the event is within the next 16 days. Otherwise the current conditions at the venue plus a note explaining why.",
            (200, "OK - weather returned"),
            (404, "Not Found - the event does not exist, or its location could not be found on the map"),
            (502, "Bad Gateway - the weather service is unavailable")),

        // ---------- Categories ----------
        ["GET api/events/{eventId}/categories"] = D("List an event's categories",
            "Lists the categories (for example 10km or 21km) offered at an event.",
            "Anyone (public).",
            "The event id in the URL.",
            "An array of categories ordered by distance.",
            (200, "OK - categories returned"),
            (404, "Not Found - the event does not exist")),

        ["GET api/categories/{id}"] = D("Get a category",
            "Returns one category.",
            "Anyone (public).",
            "The category id in the URL.",
            "The category with its distance, entry fee and capacity.",
            (200, "OK - category returned"),
            (404, "Not Found - the category does not exist")),

        ["POST api/events/{eventId}/categories"] = D("Add a category to an event",
            "Defines a distance or age category for an event.",
            "The Organiser who owns the event.",
            "categoryName, distanceKM (0.01 to 999.99), entryFee (0 or more) and an optional maxParticipants.",
            "The created category.",
            (201, "Created - category created"),
            (400, "Bad Request - a field is invalid"),
            (401, "Unauthorized - not logged in"),
            (403, "Forbidden - not an organiser, or not the owner of this event"),
            (404, "Not Found - the event does not exist")),

        ["PUT api/categories/{id}"] = D("Update a category",
            "Changes a category's name, distance, fee or capacity.",
            "The Organiser who owns the event the category belongs to.",
            "The same fields as creating a category.",
            "The updated category.",
            (200, "OK - category updated"),
            (400, "Bad Request - a field is invalid"),
            (401, "Unauthorized - not logged in"),
            (403, "Forbidden - not an organiser, or not the owner of this event"),
            (404, "Not Found - the category does not exist"),
            (409, "Conflict - maxParticipants is lower than the number of active enrolments")),

        ["DELETE api/categories/{id}"] = D("Delete a category",
            "Deletes a category. Cancelled enrolments are removed with it.",
            "The Organiser who owns the event the category belongs to.",
            "The category id in the URL.",
            "A confirmation message.",
            (200, "OK - category deleted"),
            (401, "Unauthorized - not logged in"),
            (403, "Forbidden - not an organiser, or not the owner of this event"),
            (404, "Not Found - the category does not exist"),
            (409, "Conflict - the category still has active enrolments")),

        // ---------- Enrolments ----------
        ["POST api/categories/{categoryId}/enrolments"] = D("Enrol in a category",
            "Enters the logged-in participant into a category. The link between participant, event and category is recorded with a bib number.",
            "Participant.",
            "No request body: the participant is taken from the session.",
            "The enrolment with event and category details, status Confirmed and a bib number.",
            (201, "Created - enrolment recorded"),
            (400, "Bad Request - the event has already taken place"),
            (401, "Unauthorized - not logged in"),
            (403, "Forbidden - only participants can enrol"),
            (404, "Not Found - the category does not exist"),
            (409, "Conflict - already enrolled, or the category is full")),

        ["GET api/participants/{id}/enrolments"] = D("List my enrolments",
            "Lists all of the participant's enrolments, newest event first.",
            "Participant (own id only).",
            "The participant id in the URL, which must match the logged-in participant.",
            "An array of enrolments with event and category details.",
            (200, "OK - enrolments returned"),
            (401, "Unauthorized - not logged in"),
            (403, "Forbidden - not a participant, or not your own id")),

        ["GET api/enrolments/{id}"] = D("Get an enrolment",
            "Returns one enrolment.",
            "The participant who owns it, or the organiser of that event.",
            "The enrolment id in the URL.",
            "The enrolment with event and category details.",
            (200, "OK - enrolment returned"),
            (401, "Unauthorized - not logged in"),
            (403, "Forbidden - the enrolment belongs to someone else"),
            (404, "Not Found - the enrolment does not exist")),

        ["DELETE api/enrolments/{id}"] = D("Cancel an enrolment",
            "Cancels the enrolment by setting its status to Cancelled. The record is kept for history and the place is freed.",
            "The Participant who owns the enrolment.",
            "The enrolment id in the URL.",
            "A confirmation message.",
            (200, "OK - enrolment cancelled"),
            (401, "Unauthorized - not logged in"),
            (403, "Forbidden - not a participant, or not your enrolment"),
            (404, "Not Found - the enrolment does not exist"),
            (409, "Conflict - already cancelled, or the event has already taken place")),

        ["GET api/categories/{categoryId}/enrolments"] = D("View a category's roster",
            "Lists who is enrolled in a category.",
            "The Organiser who owns the event.",
            "The category id in the URL, and an optional includeCancelled query parameter (default false).",
            "An array of participants with their enrolment status and bib number.",
            (200, "OK - roster returned"),
            (401, "Unauthorized - not logged in"),
            (403, "Forbidden - not an organiser, or not the owner of this event"),
            (404, "Not Found - the category does not exist")),

        // ---------- Results ----------
        ["POST api/enrolments/{enrolmentId}/results"] = D("Record a result",
            "Records a participant's finish time and position after the event has taken place.",
            "The Organiser who owns the event.",
            "status (Finished, DNF, DNS or DSQ). A Finished result needs finishTime (HH:mm:ss) and may have a position; other statuses must leave both empty.",
            "The recorded result with participant, event and category details.",
            (201, "Created - result recorded"),
            (400, "Bad Request - invalid values, or the event has not taken place yet"),
            (401, "Unauthorized - not logged in"),
            (403, "Forbidden - not an organiser, or not the owner of this event"),
            (404, "Not Found - the enrolment does not exist"),
            (409, "Conflict - a result already exists, or the enrolment was cancelled")),

        ["PUT api/results/{id}"] = D("Correct a result",
            "Changes an existing result, for example to fix a wrong finish time.",
            "The Organiser who owns the event.",
            "The same fields as recording a result.",
            "The updated result.",
            (200, "OK - result updated"),
            (400, "Bad Request - invalid values"),
            (401, "Unauthorized - not logged in"),
            (403, "Forbidden - not an organiser, or not the owner of this event"),
            (404, "Not Found - the result does not exist")),

        ["GET api/events/{eventId}/results"] = D("View event results",
            "Shows the results leaderboard for an event, ordered by category distance and then by position.",
            "Anyone (public).",
            "The event id in the URL and an optional categoryId query parameter.",
            "An array of results with participant names.",
            (200, "OK - results returned (possibly an empty list)"),
            (404, "Not Found - the event, or the category for that event, does not exist")),

        ["GET api/participants/{id}/results"] = D("View my results",
            "Shows the participant's personal performance history across events.",
            "Participant (own id only).",
            "The participant id in the URL, which must match the logged-in participant.",
            "An array of results, newest event first.",
            (200, "OK - results returned"),
            (401, "Unauthorized - not logged in"),
            (403, "Forbidden - not a participant, or not your own id")),

        ["GET api/results/{id}"] = D("Get a result",
            "Returns one result.",
            "The participant it belongs to, or the organiser of that event.",
            "The result id in the URL.",
            "The result with participant, event and category details.",
            (200, "OK - result returned"),
            (401, "Unauthorized - not logged in"),
            (403, "Forbidden - the result belongs to someone else"),
            (404, "Not Found - the result does not exist")),
    };

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var key = $"{context.ApiDescription.HttpMethod} {context.ApiDescription.RelativePath}";
        if (!Docs.TryGetValue(key, out var doc)) return;

        operation.Summary = doc.Summary;
        operation.Description = doc.Description;

        // Swashbuckle adds a default 200 for these actions; drop it when the real success code is 201
        if (!doc.Responses.Any(r => r.Code == 200))
            operation.Responses.Remove("200");

        foreach (var (code, text) in doc.Responses)
        {
            var status = code.ToString();
            if (operation.Responses.TryGetValue(status, out var existing))
                existing.Description = text;
            else
                operation.Responses[status] = new OpenApiResponse { Description = text };
        }
    }
}