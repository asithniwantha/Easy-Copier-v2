namespace EasyCopier.Features.Library.Scanning;

/// <summary>
/// Defines operations for extracting and caching visual thumbnails from media files.
/// </summary>
public interface IThumbnailService
{
    /// <summary>
    /// Asynchronously extracts a thumbnail image from the specified media file and stores it in the cache directory.
    /// </summary>
    Task<string?> ExtractThumbnailAsync(
        string sourceFilePath,
        string cacheDirectoryPath,
        CancellationToken cancellationToken = default);
}

