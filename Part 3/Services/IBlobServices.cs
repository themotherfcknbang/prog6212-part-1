namespace RaceDay.Api.Services;

public interface IBlobService
{
    /// <summary>Returns an error message if the file is not an acceptable image, otherwise null.</summary>
    string? Validate(IFormFile? file);

    /// <summary>Uploads the image to the given container and returns its public URL.</summary>
    Task<string> UploadAsync(string containerName, IFormFile file, CancellationToken ct = default);

    /// <summary>Deletes a previously uploaded blob. Silently ignores null/unknown URLs.</summary>
    Task DeleteAsync(string containerName, string? blobUrl, CancellationToken ct = default);
}