using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace RaceDay.Api.Services;

public class BlobService : IBlobService
{
    public const string BannerContainer = "event-banners";
    public const string ProfileContainer = "profile-pictures";

    private const long MaxBytes = 5 * 1024 * 1024; // 5 MB

    private static readonly Dictionary<string, string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp"
    };

    private readonly BlobServiceClient _client;

    // Optional. When the API reaches storage by a name the browser can't use (e.g. "azurite" inside
    // Docker), set AzureBlob:PublicBaseUrl so saved image links use an address the browser can open.
    private readonly string? _publicBaseUrl;

    public BlobService(BlobServiceClient client, IConfiguration configuration)
    {
        _client = client;
        _publicBaseUrl = configuration["AzureBlob:PublicBaseUrl"]?.TrimEnd('/');
    }

    private string PublicUrl(Uri blobUri)
    {
        if (string.IsNullOrWhiteSpace(_publicBaseUrl)) return blobUri.ToString();

        // "<storage endpoint>/event-banners/abc.png" -> "<public base>/event-banners/abc.png"
        var endpoint = _client.Uri.AbsoluteUri.TrimEnd('/');
        var rest = blobUri.AbsoluteUri[endpoint.Length..];
        return _publicBaseUrl + rest;
    }

    public string? Validate(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return "Please choose an image file to upload.";
        if (file.Length > MaxBytes)
            return "The image must be 5 MB or smaller.";
        if (!AllowedTypes.ContainsKey(file.ContentType))
            return "Only JPEG, PNG or WebP images are allowed.";
        return null;
    }

    public async Task<string> UploadAsync(string containerName, IFormFile file, CancellationToken ct = default)
    {
        var container = _client.GetBlobContainerClient(containerName);
        // Public read access for blobs only, so the event page can show the image by URL
        await container.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: ct);

        // Random name: stops clashes and ignores whatever filename the user sent
        var blobName = $"{Guid.NewGuid():N}{AllowedTypes[file.ContentType]}";
        var blob = container.GetBlobClient(blobName);

        await using var stream = file.OpenReadStream();
        await blob.UploadAsync(stream,
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = file.ContentType } },
            ct);

        return PublicUrl(blob.Uri);
    }

    public async Task DeleteAsync(string containerName, string? blobUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(blobUrl) || !Uri.TryCreate(blobUrl, UriKind.Absolute, out var uri))
            return;

        var blobName = Path.GetFileName(uri.LocalPath);
        await _client.GetBlobContainerClient(containerName)
            .DeleteBlobIfExistsAsync(blobName, cancellationToken: ct);
    }
}