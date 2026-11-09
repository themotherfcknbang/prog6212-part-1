using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace RaceDay.Web.Services;

/// <summary>What every API call gives back: either data or a friendly error message.</summary>
public class ApiResult<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string Error { get; init; } = string.Empty;
    public HttpStatusCode? StatusCode { get; init; }

    public bool IsUnauthorized => StatusCode == HttpStatusCode.Unauthorized;
    public bool IsForbidden => StatusCode == HttpStatusCode.Forbidden;
    public bool IsNotFound => StatusCode == HttpStatusCode.NotFound;
}

/// <summary>The { "message": "..." } body the API returns for simple confirmations.</summary>
public record MessageDto(string Message);

/// <summary>
/// The ONLY place the MVC app talks to the API. It sends the API's session cookie
/// (kept in the MVC session) with every request, so the user stays logged in.
/// </summary>
public class ApiClient
{
    public const string ApiCookieName = "RaceDay.Session";
    private const string SessionKey = "ApiCookie";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _accessor;
    private readonly ILogger<ApiClient> _logger;

    public ApiClient(HttpClient http, IHttpContextAccessor accessor, ILogger<ApiClient> logger)
    {
        _http = http;
        _accessor = accessor;
        _logger = logger;
    }

    private ISession? Session => _accessor.HttpContext?.Session;

    // Use relative URLs without a leading slash, e.g. "api/events"
    public Task<ApiResult<T>> GetAsync<T>(string url) =>
        SendAsync<T>(HttpMethod.Get, url, null);

    public Task<ApiResult<T>> PostAsync<T>(string url, object? body = null) =>
        SendAsync<T>(HttpMethod.Post, url, ToJson(body));

    public Task<ApiResult<T>> PutAsync<T>(string url, object body) =>
        SendAsync<T>(HttpMethod.Put, url, ToJson(body));

    public Task<ApiResult<T>> DeleteAsync<T>(string url) =>
        SendAsync<T>(HttpMethod.Delete, url, null);

    /// <summary>Sends an image to the API as multipart form data (field name "file").</summary>
    public Task<ApiResult<T>> UploadAsync<T>(string url, IFormFile file)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(file.OpenReadStream());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
        content.Add(fileContent, "file", file.FileName);
        return SendAsync<T>(HttpMethod.Post, url, content);
    }

    private static HttpContent? ToJson(object? body) =>
        body == null ? null : JsonContent.Create(body, body.GetType(), options: Json);

    private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string url, HttpContent? content)
    {
        try
        {
            using var request = new HttpRequestMessage(method, url) { Content = content };

            var cookie = Session?.GetString(SessionKey);
            if (!string.IsNullOrEmpty(cookie))
                request.Headers.TryAddWithoutValidation("Cookie", cookie);

            using var response = await _http.SendAsync(request);
            CaptureCookie(response);

            if (response.IsSuccessStatusCode)
            {
                T? data = default;
                var body = await response.Content.ReadAsStringAsync();
                if (!string.IsNullOrWhiteSpace(body))
                    data = JsonSerializer.Deserialize<T>(body, Json);

                return new ApiResult<T> { Success = true, Data = data, StatusCode = response.StatusCode };
            }

            return new ApiResult<T>
            {
                Success = false,
                StatusCode = response.StatusCode,
                Error = await ReadErrorAsync(response)
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                      or JsonException or NotSupportedException)
        {
            // API is down, timed out, or sent something unexpected: never show the raw error
            _logger.LogError(ex, "API call failed: {Method} {Url}", method, url);
            return new ApiResult<T>
            {
                Success = false,
                Error = "RaceDay could not reach the service right now. Please try again in a moment."
            };
        }
    }

    // Keeps the API's session cookie in the MVC session so we can send it back next time
    private void CaptureCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values)) return;

        foreach (var header in values)
        {
            if (!header.StartsWith(ApiCookieName + "=", StringComparison.Ordinal)) continue;

            var pair = header.Split(';')[0];                       // RaceDay.Session=value
            var value = pair[(ApiCookieName.Length + 1)..];
            var expired = header.Contains("1970", StringComparison.OrdinalIgnoreCase);

            if (string.IsNullOrEmpty(value) || expired)
                Session?.Remove(SessionKey);
            else
                Session?.SetString(SessionKey, pair);
        }
    }

    // Turns an API error body into one readable sentence
    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                // Our own errors: { "message": "..." }
                if (root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                    return message.GetString()!;

                // Validation errors: { "errors": { "Field": ["..."] } }
                if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                {
                    var messages = errors.EnumerateObject()
                        .Where(p => p.Value.ValueKind == JsonValueKind.Array)
                        .SelectMany(p => p.Value.EnumerateArray())
                        .Select(x => x.GetString())
                        .Where(s => !string.IsNullOrWhiteSpace(s));
                    var joined = string.Join(" ", messages);
                    if (joined.Length > 0) return joined;
                }
            }
        }
        catch (JsonException) { /* fall through to the generic messages */ }

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Please log in to continue.",
            HttpStatusCode.Forbidden => "You do not have permission to do that.",
            HttpStatusCode.NotFound => "We could not find what you were looking for.",
            _ => "Something went wrong. Please try again."
        };
    }
}