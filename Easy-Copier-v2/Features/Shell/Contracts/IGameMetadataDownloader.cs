namespace EasyCopier.Features.Shell.Contracts;

/// <summary>
/// Decoupled contract allowing Settings to initiate bulk game cover and system requirements downloads
/// without directly depending on the <c>Library</c> slice.
/// </summary>
public interface IGameMetadataDownloader
{
    /// <summary>
    /// Downloads missing or overwritten cover art and hardware requirements metadata across the specified game folders.
    /// </summary>
    Task DownloadGameInfoAsync(
        IEnumerable<string> gameFolders,
        IProgress<string>? progress = null,
        bool overwriteExisting = false,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// No-op fallback used before the <c>Library</c> slice registers the metadata downloader implementation.
/// </summary>
public sealed class NullGameMetadataDownloader : IGameMetadataDownloader
{
    /// <inheritdoc />
    public Task DownloadGameInfoAsync(
        IEnumerable<string> gameFolders,
        IProgress<string>? progress = null,
        bool overwriteExisting = false,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
