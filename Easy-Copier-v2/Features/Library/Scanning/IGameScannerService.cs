using EasyCopier.Features.Shell.Contracts;

namespace EasyCopier.Features.Library.Scanning;

/// <summary>
/// Defines operations for scanning game and content directories, computing folder sizes, and locating metadata assets.
/// </summary>
public interface IGameScannerService
{
    /// <summary>
    /// Asynchronously scans specified source folders for items belonging to the given library category.
    /// </summary>
    Task<IReadOnlyList<GameEntry>> ScanLibraryAsync(
        IEnumerable<string> sourceFolders,
        LibraryCategory category,
        IProgress<string>? progress = null,
        string? videoExtensions = null,
        string? appFileExtensions = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates the total byte size and determines if any contained file exceeds the specified size limit.
    /// </summary>
    Task<(long TotalSize, bool HasLargeFiles)> GetFolderStatsAsync(
        string folderPath,
        long sizeLimit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Locates a cover image file within a game folder if available.
    /// </summary>
    string? FindCoverImage(string gameFolderPath);

    /// <summary>
    /// Reads genre categories for a game from its local <c>categories.txt</c> file.
    /// </summary>
    IReadOnlyList<GameCategory> GetCategories(string gameFolderPath);
}

