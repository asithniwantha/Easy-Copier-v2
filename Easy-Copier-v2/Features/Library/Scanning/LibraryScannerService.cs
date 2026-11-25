using System.Globalization;
using System.Text;
using EasyCopier.Features.Library.Models;
using EasyCopier.Features.Shell.Contracts;
using EasyCopier.Shared.Configuration;

namespace EasyCopier.Features.Library.Scanning;

/// <summary>
/// Service coordinating overall scanning across configured source libraries and duplicate detection.
/// </summary>
public class LibraryScannerService : ILibraryScannerService
{
    private readonly IGameScannerService _gameScannerService;

    /// <summary>
    /// Initializes a new instance of the <see cref="LibraryScannerService"/> class.
    /// </summary>
    public LibraryScannerService(IGameScannerService gameScannerService)
    {
        _gameScannerService = gameScannerService ?? throw new ArgumentNullException(nameof(gameScannerService));
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<GameEntry> Games, IReadOnlyList<GameEntry> Apps, IReadOnlyList<GameEntry> TvAndFilms)> ScanCoreLibrariesAsync(
        AppSettings settings,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        List<GameEntry> allGames = [];
        List<GameEntry> allApps = [];
        List<GameEntry> allTvAndFilms = [];

        if (settings.GameSourceFolders?.Count > 0)
        {
            IReadOnlyList<GameEntry> games = await _gameScannerService.ScanLibraryAsync(
                settings.GameSourceFolders,
                LibraryCategory.Game,
                progress,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            allGames.AddRange(games);
        }

        if (settings.AppSourceFolders?.Count > 0)
        {
            IReadOnlyList<GameEntry> apps = await _gameScannerService.ScanLibraryAsync(
                settings.AppSourceFolders,
                LibraryCategory.App,
                progress,
                appFileExtensions: settings.AppFileExtensions,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            allApps.AddRange(apps);
        }

        if (settings.TvAndFilmSourceFolders?.Count > 0)
        {
            IReadOnlyList<GameEntry> tvAndFilms = await _gameScannerService.ScanLibraryAsync(
                settings.TvAndFilmSourceFolders,
                LibraryCategory.TvAndFilm,
                progress,
                settings.VideoFileExtensions,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            allTvAndFilms.AddRange(tvAndFilms);
        }

        return (allGames, allApps, allTvAndFilms);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<GameEntry> Games, IReadOnlyList<GameEntry> Apps, IReadOnlyList<GameEntry> TvAndFilms, IReadOnlyList<GameEntry> OsImages)> ScanAllLibrariesAsync(
        AppSettings settings,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        (IReadOnlyList<GameEntry> games, IReadOnlyList<GameEntry> apps, IReadOnlyList<GameEntry> tvAndFilms) =
            await ScanCoreLibrariesAsync(settings, progress, cancellationToken).ConfigureAwait(false);

        List<GameEntry> allOsImages = [];
        if (settings.OsImageSourceFolders?.Count > 0)
        {
            IReadOnlyList<GameEntry> osImages = await _gameScannerService.ScanLibraryAsync(
                settings.OsImageSourceFolders,
                LibraryCategory.OsImage,
                progress,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            allOsImages.AddRange(osImages);
        }

        return (games, apps, tvAndFilms, allOsImages);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DuplicateGroup>> GetDuplicateGroupsAsync(
        AppSettings settings,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        (IReadOnlyList<GameEntry> games, IReadOnlyList<GameEntry> apps, IReadOnlyList<GameEntry> tvAndFilms, IReadOnlyList<GameEntry> osImages) =
            await ScanAllLibrariesAsync(settings, progress, cancellationToken).ConfigureAwait(false);

        List<GameEntry> allEntries = [.. games, .. apps, .. tvAndFilms, .. osImages];

        return allEntries
            .GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => new DuplicateGroup(
                g.Key,
                g.Select(e => new DuplicateItemEntry(e.Name, e.FolderPath, e.Category, e.TotalBytes, e.DateCreated)).ToList()))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<string> FindDuplicatesReportAsync(
        AppSettings settings,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<DuplicateGroup> duplicates = await GetDuplicateGroupsAsync(settings, progress, cancellationToken).ConfigureAwait(false);

        if (duplicates.Count == 0)
        {
            return "No duplicates found across source libraries.";
        }

        StringBuilder reportBuilder = new();
        _ = reportBuilder.AppendLine(CultureInfo.InvariantCulture, $"Found {duplicates.Count} duplicated items:");
        _ = reportBuilder.AppendLine();

        foreach (DuplicateGroup group in duplicates)
        {
            _ = reportBuilder.AppendLine(CultureInfo.InvariantCulture, $"- {group.ItemName} ({group.Entries.Count} copies):");
            foreach (DuplicateItemEntry entry in group.Entries)
            {
                _ = reportBuilder.AppendLine(CultureInfo.InvariantCulture, $"  \u2022 [{entry.Category}] {entry.FolderPath}");
            }

            _ = reportBuilder.AppendLine();
        }

        _ = reportBuilder.AppendLine("Cleanup Recommendation: Consider deleting the duplicate folders shown above to save disk space.");
        return reportBuilder.ToString().TrimEnd();
    }
}

