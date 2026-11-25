using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using EasyCopier.Features.Shell.Contracts;
using EasyCopier.Shared.Configuration;
using EasyCopier.Shared.Messages;
using EasyCopier.Shared.Models;
using EasyCopier.Shared.Platform;
using Microsoft.Extensions.Logging;

namespace EasyCopier.Features.Settings;

/// <summary>
/// ViewModel managing application settings, source folder configurations, price tiers, and updates.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject, ICloseableViewModel, IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly IFolderPickerService _folderPickerService;
    private readonly IFilePickerService _filePickerService;
    private readonly ISourceFolderValidator _sourceFolderValidator;
    private readonly IGameMetadataDownloader _gameMetadataDownloader;
    private readonly IRufusExecutableResolver _rufusExecutableResolver;
    private readonly IStartupService _startupService;
    private readonly IDispatcherService _dispatcherService;
    private readonly ILogger<SettingsViewModel> _logger;
    private readonly IUpdateService _updateService;
    private readonly IDialogService _dialogService;
    private readonly IWindowService _windowService;
    private readonly IProcessService _processService;
    private readonly IMessenger _messenger;
    private readonly ObservableCollection<string>[] _allFolderCollections;

    private AppSettings? _lastSavedSettings;
    private CancellationTokenSource? _downloadGameInfoCts;
    private bool _isUpdatingStartOnLogon;

    [ObservableProperty]
    public partial bool AutoScanOnStartup { get; set; } = true;

    [ObservableProperty]
    public partial bool StartOnLogon { get; set; } = false;

    [ObservableProperty]
    public partial bool AutoDownloadUpdates { get; set; } = true;

    [ObservableProperty]
    public partial bool PlayNotificationSounds { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowDesktopNotifications { get; set; } = true;

    [ObservableProperty]
    public partial bool MinimizeToTrayOnClose { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VerificationModeIndex))]
    public partial CopyVerificationMode VerificationMode { get; set; } = CopyVerificationMode.QuickSizeAndCount;

    /// <summary>
    /// Gets or sets the zero-based index representing <see cref="VerificationMode"/> for XAML ComboBox selection.
    /// </summary>
    public int VerificationModeIndex
    {
        get => (int)VerificationMode;
        set
        {
            if (value >= 0 && value <= 3 && (CopyVerificationMode)value != VerificationMode)
            {
                VerificationMode = (CopyVerificationMode)value;
                OnPropertyChanged();
            }
        }
    }

    [ObservableProperty]
    public partial string PriceTier1 { get; set; } = "100";

    [ObservableProperty]
    public partial string PriceTier2 { get; set; } = "200";

    [ObservableProperty]
    public partial string PriceTier3 { get; set; } = "300";

    [ObservableProperty]
    public partial string PriceTier4 { get; set; } = "400";

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool OverwriteExistingCovers { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDownloadCovers))]
    public partial bool IsDownloadingCovers { get; set; } = false;

    /// <summary>Gets a value indicating whether cover downloading can currently be initiated.</summary>
    public bool CanDownloadCovers => !IsDownloadingCovers;

    public ObservableCollection<string> GameSourceFolders { get; } = [];
    public bool HasNoGameSourceFolders => GameSourceFolders.Count == 0;

    public ObservableCollection<string> AppSourceFolders { get; } = [];
    public bool HasNoAppSourceFolders => AppSourceFolders.Count == 0;

    public ObservableCollection<string> TvAndFilmSourceFolders { get; } = [];
    public bool HasNoTvAndFilmSourceFolders => TvAndFilmSourceFolders.Count == 0;

    public ObservableCollection<string> OsImageSourceFolders { get; } = [];
    public bool HasNoOsImageSourceFolders => OsImageSourceFolders.Count == 0;

    [ObservableProperty]
    public partial string RufusExecutablePath { get; set; } = @"%USERPROFILE%\Downloads\Programs\rufus.exe";

    [ObservableProperty]
    public partial string VideoFileExtensions { get; set; } = ".mp4,.mkv,.avi";

    [ObservableProperty]
    public partial string AppFileExtensions { get; set; } = ".exe,.msi,.rar,.zip";

    [ObservableProperty]
    public partial string SelectedNavTag { get; set; } = "General";

    /// <inheritdoc />
    public event EventHandler? CloseRequested;

    /// <inheritdoc />
    public void RequestClose() => CloseRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsViewModel"/> class with required dependencies.
    /// </summary>
    public SettingsViewModel(
        ILogger<SettingsViewModel> logger,
        ISettingsService settingsService,
        IFolderPickerService folderPickerService,
        IFilePickerService filePickerService,
        ISourceFolderValidator sourceFolderValidator,
        IProcessService processService,
        IGameMetadataDownloader gameMetadataDownloader,
        IRufusExecutableResolver rufusExecutableResolver,
        IStartupService startupService,
        IDispatcherService dispatcherService,
        IUpdateService updateService,
        IDialogService dialogService,
        IWindowService windowService,
        IMessenger messenger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(folderPickerService);
        ArgumentNullException.ThrowIfNull(filePickerService);
        ArgumentNullException.ThrowIfNull(sourceFolderValidator);
        ArgumentNullException.ThrowIfNull(processService);
        ArgumentNullException.ThrowIfNull(gameMetadataDownloader);
        ArgumentNullException.ThrowIfNull(rufusExecutableResolver);
        ArgumentNullException.ThrowIfNull(startupService);
        ArgumentNullException.ThrowIfNull(dispatcherService);
        ArgumentNullException.ThrowIfNull(updateService);
        ArgumentNullException.ThrowIfNull(dialogService);
        ArgumentNullException.ThrowIfNull(windowService);
        ArgumentNullException.ThrowIfNull(messenger);

        _logger = logger;
        _settingsService = settingsService;
        _folderPickerService = folderPickerService;
        _filePickerService = filePickerService;
        _sourceFolderValidator = sourceFolderValidator;
        _processService = processService;
        _gameMetadataDownloader = gameMetadataDownloader;
        _rufusExecutableResolver = rufusExecutableResolver;
        _startupService = startupService;
        _dispatcherService = dispatcherService;
        _updateService = updateService;
        _dialogService = dialogService;
        _windowService = windowService;
        _messenger = messenger;

        _allFolderCollections = [GameSourceFolders, AppSourceFolders, TvAndFilmSourceFolders, OsImageSourceFolders];

        GameSourceFolders.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasNoGameSourceFolders));
        AppSourceFolders.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasNoAppSourceFolders));
        TvAndFilmSourceFolders.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasNoTvAndFilmSourceFolders));
        OsImageSourceFolders.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasNoOsImageSourceFolders));
    }

    [RelayCommand]
    private void OpenLogsFolder()
    {
        try
        {
            string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string logFolder = Path.Combine(appDataFolder, "EasyCopier", "Logs");
            if (Directory.Exists(logFolder))
            {
                _processService.OpenInExplorer(logFolder);
                _logger.LogInformation("Logs folder opened successfully.");
            }
            else
            {
                _logger.LogWarning("Logs folder not found.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open logs folder.");
        }
    }

    [RelayCommand]
    private async Task BrowseRufusExecutableAsync()
    {
        StatusMessage = "Opening file picker...";

        string? selectedFile = await _filePickerService.PickOpenFileAsync([".exe"]);

        if (!string.IsNullOrEmpty(selectedFile))
        {
            string resolvedPath = _rufusExecutableResolver.ResolveLatestRufusPath(selectedFile);
            RufusExecutablePath = resolvedPath;
            StatusMessage = $"Selected Rufus executable: {Path.GetFileName(resolvedPath)}";
            await SaveSettingsAsync();
        }
        else
        {
            StatusMessage = "No file selected";
        }
    }

    [RelayCommand]
    private void FindDuplicates()
    {
        _windowService.ShowWindow(AppWindowKind.DuplicateManager);
    }

    [RelayCommand]
    private async Task CheckForUpdatesNowAsync()
    {
        StatusMessage = "Checking for updates...";
        _logger.LogInformation("Manual update check triggered from Settings.");

        try
        {
            bool hasUpdate = await _updateService.CheckForUpdatesAsync();

            if (hasUpdate)
            {
                _logger.LogInformation("Manual update check found an update. Starting download...");
                StatusMessage = "Downloading update...";

                await _updateService.DownloadUpdateAsync();

                _logger.LogInformation("Manual update download completed.");
                StatusMessage = "Update ready! Please restart the app to apply.";

                await _dialogService.ShowMessageDialogAsync(
                    "Update Ready",
                    "The update has been downloaded successfully. Please close and restart the application to apply the update.",
                    "OK");
            }
            else
            {
                _logger.LogInformation("Manual update check found no updates.");
                StatusMessage = "App is up to date.";

                await _dialogService.ShowMessageDialogAsync(
                    "No Updates",
                    "The application is up to date.",
                    "OK");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during manual update check");
            StatusMessage = "Failed to check for updates.";

            await _dialogService.ShowMessageDialogAsync(
                "Error",
                "Failed to check for updates. Please try again later.",
                "OK");
        }
    }

    [RelayCommand]
    private async Task DownloadGameInfoAsync()
    {
        if (GameSourceFolders.Count == 0)
        {
            StatusMessage = "No game folders configured";
            return;
        }

        if (_downloadGameInfoCts is not null)
        {
            await _downloadGameInfoCts.CancelAsync();
            _downloadGameInfoCts.Dispose();
        }

        _downloadGameInfoCts = new CancellationTokenSource();
        CancellationToken token = _downloadGameInfoCts.Token;

        IsDownloadingCovers = true;
        StatusMessage = "Downloading game covers and requirements...";

        try
        {
            Progress<string> progress = new(msg => _ = _dispatcherService.TryEnqueue(() => StatusMessage = msg));
            await _gameMetadataDownloader.DownloadGameInfoAsync(GameSourceFolders, progress, OverwriteExistingCovers, token);
            StatusMessage = "Game info download complete";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Game info download canceled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error downloading game info: {ex.Message}";
        }
        finally
        {
            IsDownloadingCovers = false;
            _downloadGameInfoCts?.Dispose();
            _downloadGameInfoCts = null;
        }
    }

    [RelayCommand]
    private async Task CancelDownloadGameInfoAsync()
    {
        if (_downloadGameInfoCts is not null && !_downloadGameInfoCts.IsCancellationRequested)
        {
            StatusMessage = "Canceling download...";
            await _downloadGameInfoCts.CancelAsync();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _downloadGameInfoCts?.Dispose();
        _downloadGameInfoCts = null;
    }

    /// <summary>
    /// Prompts the user to select and append a new source folder for the specified library category type.
    /// </summary>
    public async Task AddNewSourceFolderAsync(SourceFolderCategory category)
    {
        (ObservableCollection<string>? targetFolders, string categoryLabel) = category switch
        {
            SourceFolderCategory.Game => (GameSourceFolders, "game"),
            SourceFolderCategory.App => (AppSourceFolders, "app"),
            SourceFolderCategory.TvAndFilm => (TvAndFilmSourceFolders, "film/tv"),
            SourceFolderCategory.OsImage => (OsImageSourceFolders, "OS image"),
            _ => (null, string.Empty)
        };

        if (targetFolders is not null)
        {
            await AddSourceFolderAsync(targetFolders, categoryLabel);
        }
    }

    [RelayCommand]
    private async Task AddNewSourceFolderAsync(string folderType)
    {
        if (Enum.TryParse(folderType, true, out SourceFolderCategory category))
        {
            await AddNewSourceFolderAsync(category);
        }
    }

    private async Task AddSourceFolderAsync(ObservableCollection<string> targetFolders, string categoryLabel)
    {
        StatusMessage = "Opening folder picker...";

        string? folderPath = await _folderPickerService.PickFolderAsync();

        if (!string.IsNullOrEmpty(folderPath))
        {
            if (!Directory.Exists(folderPath))
            {
                StatusMessage = $"Selected folder does not exist or is not accessible: {folderPath}";
                await _dialogService.ShowMessageDialogAsync("Folder Inaccessible", $"The selected directory '{folderPath}' cannot be accessed or does not exist.");
                return;
            }

            if (!targetFolders.Contains(folderPath))
            {
                targetFolders.Add(folderPath);
                StatusMessage = $"Added {categoryLabel} folder: {folderPath}";
                await SaveSettingsAsync();
            }
            else
            {
                StatusMessage = "Folder already exists in the list";
            }
        }
        else
        {
            StatusMessage = "No folder selected";
        }
    }

    [RelayCommand]
    private async Task RemoveSourceFolderByPathAsync(string folderPath)
    {
        foreach (ObservableCollection<string> collection in _allFolderCollections)
        {
            if (collection.Remove(folderPath))
            {
                StatusMessage = $"Removed: {folderPath}";
                await SaveSettingsAsync();
                return;
            }
        }
    }

    [RelayCommand]
    private void OpenDataFolder()
    {
        string? folderPath = Path.GetDirectoryName(_settingsService.GetSettingsFilePath());

        if (string.IsNullOrWhiteSpace(folderPath))
        {
            StatusMessage = "Unable to resolve the data folder";
            return;
        }

        try
        {
            _processService.OpenInExplorer(folderPath);
            StatusMessage = $"Opened data folder: {folderPath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unable to open data folder: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        StatusMessage = "Validating folders...";

        AppSettings settings = GetSettings();

        IReadOnlyList<SourceFolder> validatedGame = await _sourceFolderValidator.ValidateSourceFoldersAsync(settings.GameSourceFolders);
        IReadOnlyList<SourceFolder> validatedApp = await _sourceFolderValidator.ValidateSourceFoldersAsync(settings.AppSourceFolders);
        IReadOnlyList<SourceFolder> validatedTv = await _sourceFolderValidator.ValidateSourceFoldersAsync(settings.TvAndFilmSourceFolders ?? []);
        IReadOnlyList<SourceFolder> validatedOs = await _sourceFolderValidator.ValidateSourceFoldersAsync(settings.OsImageSourceFolders ?? []);

        List<string> inaccessibleFolders = validatedGame
            .Concat(validatedApp)
            .Concat(validatedTv)
            .Concat(validatedOs)
            .Where(f => !f.IsValid)
            .Select(f => f.FolderPath)
            .Distinct()
            .ToList();

        if (inaccessibleFolders.Count > 0)
        {
            string folderList = string.Join("\n", inaccessibleFolders.Select(p => $"• {p}"));
            string prompt = $"The following {inaccessibleFolders.Count} configured source folder(s) are currently inaccessible or do not exist:\n\n{folderList}\n\nDo you want to save anyway?";
            bool proceed = await _dialogService.ShowConfirmationDialogAsync("Inaccessible Source Folders", prompt, "Save Anyway", "Cancel");
            if (!proceed)
            {
                StatusMessage = "Save cancelled: resolve inaccessible source folders.";
                return;
            }
        }

        StatusMessage = "Saving settings...";

        await _settingsService.SaveSettingsAsync(settings);
        _startupService.UpdateStartOnLogon(settings.StartOnLogon);

        (bool sourcePathsUpdated, bool priceTiersUpdated) = ComputeSettingsChanges(_lastSavedSettings, settings);
        _lastSavedSettings = settings;
        _messenger.Send(new SettingsChangedMessage(sourcePathsUpdated, priceTiersUpdated));

        StatusMessage = "Settings saved";
    }

    [RelayCommand]
    private async Task SaveAndCloseAsync()
    {
        await SaveSettingsAsync();
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    partial void OnSelectedNavTagChanged(string value)
    {
    }

    partial void OnStartOnLogonChanged(bool value)
    {
        if (_isUpdatingStartOnLogon)
        {
            return;
        }

        if (value)
        {
            string? executablePath = Environment.ProcessPath;
            string expectedFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EasyCopier");

            if (string.IsNullOrEmpty(executablePath) || !executablePath.StartsWith(expectedFolder, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Cannot enable start on logon: Application is not running from {ExpectedFolder}. Current path: {ExecutablePath}",
                    expectedFolder,
                    executablePath);

                _isUpdatingStartOnLogon = true;
                try
                {
                    StartOnLogon = false;
                }
                finally
                {
                    _isUpdatingStartOnLogon = false;
                }

                StatusMessage = "Cannot enable startup. App is not in AppData\\Local\\EasyCopier.";
            }
        }
    }
}
