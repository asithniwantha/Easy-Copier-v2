using System.Globalization;
using EasyCopier.Shared.Configuration;
using EasyCopier.Shared.Models;
using EasyCopier.Shared.UI.Helpers;

namespace EasyCopier.Features.Settings;

/// <summary>
/// Partial class implementation of <see cref="SettingsViewModel"/> providing settings persistence and change diffing.
/// </summary>
public sealed partial class SettingsViewModel
{
    /// <summary>
    /// Asynchronously loads settings from persistent storage and validates configured source library folders.
    /// </summary>
    public async Task LoadSettingsAsync()
    {
        AppSettings settings = await _settingsService.LoadSettingsAsync();
        LoadSettings(settings);

        string latestRufusPath = _rufusExecutableResolver.ResolveLatestRufusPath(RufusExecutablePath);
        if (!string.Equals(latestRufusPath, RufusExecutablePath, StringComparison.OrdinalIgnoreCase))
        {
            RufusExecutablePath = latestRufusPath;
            await SaveSettingsAsync();
        }

        IReadOnlyList<SourceFolder> validatedGameFolders = await _sourceFolderValidator.ValidateSourceFoldersAsync(settings.GameSourceFolders);
        IReadOnlyList<SourceFolder> validatedAppFolders = await _sourceFolderValidator.ValidateSourceFoldersAsync(settings.AppSourceFolders);
        IReadOnlyList<SourceFolder> validatedTvFolders = await _sourceFolderValidator.ValidateSourceFoldersAsync(settings.TvAndFilmSourceFolders ?? []);
        IReadOnlyList<SourceFolder> validatedOsFolders = await _sourceFolderValidator.ValidateSourceFoldersAsync(settings.OsImageSourceFolders ?? []);

        int invalidCount = validatedGameFolders.Count(f => !f.IsValid)
            + validatedAppFolders.Count(f => !f.IsValid)
            + validatedTvFolders.Count(f => !f.IsValid)
            + validatedOsFolders.Count(f => !f.IsValid);

        if (invalidCount > 0)
        {
            StatusMessage = $"Warning: {invalidCount} source folder(s) are not accessible";
        }
    }

    /// <summary>
    /// Populates the view model properties and collections from the provided <see cref="AppSettings"/> instance.
    /// </summary>
    public void LoadSettings(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _lastSavedSettings = settings;

        AutoScanOnStartup = settings.AutoScanOnStartup;
        StartOnLogon = settings.StartOnLogon;
        AutoDownloadUpdates = settings.AutoDownloadUpdates;
        PlayNotificationSounds = settings.PlayNotificationSounds;
        ShowDesktopNotifications = settings.ShowDesktopNotifications;
        MinimizeToTrayOnClose = settings.MinimizeToTrayOnClose;
        VerificationMode = settings.VerificationMode;
        RufusExecutablePath = settings.RufusExecutablePath ?? @"%USERPROFILE%\Downloads\Programs\rufus.exe";
        VideoFileExtensions = settings.VideoFileExtensions ?? ".mp4,.mkv,.avi";
        AppFileExtensions = settings.AppFileExtensions ?? ".exe,.msi,.rar,.zip";
        PriceTier1 = settings.PriceTier1.ToString(CultureInfo.InvariantCulture);
        PriceTier2 = settings.PriceTier2.ToString(CultureInfo.InvariantCulture);
        PriceTier3 = settings.PriceTier3.ToString(CultureInfo.InvariantCulture);
        PriceTier4 = settings.PriceTier4.ToString(CultureInfo.InvariantCulture);

        GameSourceFolders.UpdateFrom(settings.GameSourceFolders);
        AppSourceFolders.UpdateFrom(settings.AppSourceFolders);
        TvAndFilmSourceFolders.UpdateFrom(settings.TvAndFilmSourceFolders ?? []);
        OsImageSourceFolders.UpdateFrom(settings.OsImageSourceFolders ?? []);

        OnPropertyChanged(nameof(HasNoGameSourceFolders));
        OnPropertyChanged(nameof(HasNoAppSourceFolders));
        OnPropertyChanged(nameof(HasNoTvAndFilmSourceFolders));
        OnPropertyChanged(nameof(HasNoOsImageSourceFolders));
    }

    /// <summary>
    /// Constructs a new <see cref="AppSettings"/> snapshot from the current view model state.
    /// </summary>
    public AppSettings GetSettings()
    {
        return new AppSettings
        {
            AutoScanOnStartup = AutoScanOnStartup,
            StartOnLogon = StartOnLogon,
            AutoDownloadUpdates = AutoDownloadUpdates,
            PlayNotificationSounds = PlayNotificationSounds,
            ShowDesktopNotifications = ShowDesktopNotifications,
            MinimizeToTrayOnClose = MinimizeToTrayOnClose,
            VerificationMode = VerificationMode,
            RufusExecutablePath = RufusExecutablePath,
            VideoFileExtensions = VideoFileExtensions,
            AppFileExtensions = AppFileExtensions,
            PriceTier1 = int.TryParse(PriceTier1, out int p1) ? p1 : 100,
            PriceTier2 = int.TryParse(PriceTier2, out int p2) ? p2 : 200,
            PriceTier3 = int.TryParse(PriceTier3, out int p3) ? p3 : 300,
            PriceTier4 = int.TryParse(PriceTier4, out int p4) ? p4 : 400,
            GameSourceFolders = [.. GameSourceFolders],
            AppSourceFolders = [.. AppSourceFolders],
            TvAndFilmSourceFolders = [.. TvAndFilmSourceFolders],
            OsImageSourceFolders = [.. OsImageSourceFolders]
        };
    }

    private static (bool SourcePathsUpdated, bool PriceTiersUpdated) ComputeSettingsChanges(AppSettings? previous, AppSettings current)
    {
        if (previous is null)
        {
            return (true, true);
        }

        bool pathsChanged =
            !PathsEqual(previous.GameSourceFolders, current.GameSourceFolders)
            || !PathsEqual(previous.AppSourceFolders, current.AppSourceFolders)
            || !PathsEqual(previous.TvAndFilmSourceFolders, current.TvAndFilmSourceFolders)
            || !PathsEqual(previous.OsImageSourceFolders, current.OsImageSourceFolders)
            || !string.Equals(previous.VideoFileExtensions, current.VideoFileExtensions, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(previous.AppFileExtensions, current.AppFileExtensions, StringComparison.OrdinalIgnoreCase);

        bool pricesChanged =
            previous.PriceTier1 != current.PriceTier1
            || previous.PriceTier2 != current.PriceTier2
            || previous.PriceTier3 != current.PriceTier3
            || previous.PriceTier4 != current.PriceTier4;

        return (pathsChanged, pricesChanged);
    }

    private static bool PathsEqual(IReadOnlyList<string>? left, IReadOnlyList<string>? right)
    {
        IReadOnlyList<string> a = left ?? [];
        IReadOnlyList<string> b = right ?? [];
        return a.Count == b.Count && a.SequenceEqual(b, StringComparer.OrdinalIgnoreCase);
    }
}

