using EasyCopier.Features.Library.Models;
using EasyCopier.Features.Shell.Contracts;
using EasyCopier.Shared.Configuration;

namespace EasyCopier.Features.Library.Scanning;

/// <summary>
/// Defines high-level library scanning operations across configured source paths and duplicate detection.
/// </summary>
public interface ILibraryScannerService
{
    /// <summary>
    /// Asynchronously scans game, app, and film/TV source directories.
    /// </summary>
    Task<(IReadOnlyList<GameEntry> Games, IReadOnlyList<GameEntry> Apps, IReadOnlyList<GameEntry> TvAndFilms)> ScanCoreLibrariesAsync(
        AppSettings settings,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously scans all configured source directories (games, apps, film/TV, and OS images).
    /// </summary>
    Task<(IReadOnlyList<GameEntry> Games, IReadOnlyList<GameEntry> Apps, IReadOnlyList<GameEntry> TvAndFilms, IReadOnlyList<GameEntry> OsImages)> ScanAllLibrariesAsync(
        AppSettings settings,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Scans all source libraries and returns grouped duplicate item entries.
    /// </summary>
    Task<IReadOnlyList<DuplicateGroup>> GetDuplicateGroupsAsync(
        AppSettings settings,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Scans all source libraries and generates a formatted text report of duplicate items.
    /// </summary>
    Task<string> FindDuplicatesReportAsync(
        AppSettings settings,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);
}

