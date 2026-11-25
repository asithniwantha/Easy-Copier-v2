namespace EasyCopier.Features.Library.Metadata;

/// <summary>
/// Defines operations for downloading game metadata, system requirements, covers, and category information for game directories.
/// </summary>
public interface IGameInfoDownloadService
{
    /// <summary>
    /// Asynchronously scans game source directories and downloads missing metadata (system requirements, cover art, genres/categories).
    /// </summary>
    Task DownloadGameInfoAsync(
        IEnumerable<string> sourceFolders,
        IProgress<string>? progress = null,
        bool overwriteExistingCovers = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads or refreshes cover art for a single game directory.
    /// </summary>
    Task<bool> DownloadCoverForGameAsync(
        string gameFolder,
        bool overwrite = true,
        CancellationToken cancellationToken = default);
}

