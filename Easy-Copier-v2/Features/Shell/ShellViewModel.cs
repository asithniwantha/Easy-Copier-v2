using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using EasyCopier.Features.Library;
using EasyCopier.Features.Library.Tabs;
using EasyCopier.Features.OsImageLauncher;
using EasyCopier.Features.Settings;
using EasyCopier.Features.Shell.Contracts;
using EasyCopier.Features.SmartAdder;
using EasyCopier.Features.TransferQueue;
using EasyCopier.Features.TransferQueue.Models;
using EasyCopier.Shared.Configuration;
using EasyCopier.Shared.Messages;
using EasyCopier.Shared.Models;
using EasyCopier.Shared.Platform;
using EasyCopier.Shared.UI.Helpers;
using Microsoft.Extensions.Logging;

namespace EasyCopier.Features.Shell;

/// <summary>
/// Slim top-level Shell coordinator hosting tab ViewModels, Action Center transfer ViewModel,
/// SmartAdder overlay ViewModel, global notifications, updates, and window routing without domain logic.
/// </summary>
public sealed partial class ShellViewModel
    : ObservableRecipient, IRecipient<TransferBatchCompletedMessage>, IDisposable
{
    private readonly ILogger<ShellViewModel> _logger;
    private readonly IWindowService _windowService;
    private readonly ISettingsService _settingsService;
    private readonly IUpdateService _updateService;
    private readonly IDispatcherService _dispatcherService;

    private CancellationTokenSource? _notificationCancellationTokenSource;
    private Timer? _updateCheckTimer;
    private DateTimeOffset _lastBatchBannerTimestamp = DateTimeOffset.MinValue;
    private int _isCheckingForUpdates;
    private int _isDisposed;

    /// <summary>Gets the shared selection coordinator managing item selections across library tabs.</summary>
    public ILibrarySelectionCoordinator SelectionCoordinator { get; }

    /// <summary>Gets the autonomous library catalog service owning Games, Apps, and Films/TV collections.</summary>
    public ILibraryCatalogService LibraryCatalog { get; }

    /// <summary>Gets the Games tab ViewModel.</summary>
    public GamesTabViewModel GamesTabViewModel { get; }

    /// <summary>Gets the Apps tab ViewModel.</summary>
    public AppsTabViewModel AppsTabViewModel { get; }

    /// <summary>Gets the Films &amp; TV tab ViewModel.</summary>
    public TvAndFilmsTabViewModel TvAndFilmsTabViewModel { get; }

    /// <summary>Gets the OS Images tab ViewModel.</summary>
    public OsImagesTabViewModel OsImagesTabViewModel { get; }

    /// <summary>Gets the Action Center transfer queue and removable drive ViewModel.</summary>
    public TransferQueueViewModel TransferQueueViewModel { get; }

    /// <summary>Gets the floating SmartAdder calculator ViewModel.</summary>
    public SmartAdderViewModel SmartAdderViewModel { get; }

    [ObservableProperty]
    public partial string GlobalNotificationTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string GlobalNotificationMessage { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDisplayGlobalNotification))]
    public partial bool IsGlobalNotificationVisible { get; set; }

    [ObservableProperty]
    public partial ValidationSeverity GlobalNotificationSeverity { get; set; } = ValidationSeverity.Success;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowUpdateAvailableNotification))]
    [NotifyPropertyChangedFor(nameof(CanDisplayGlobalNotification))]
    public partial bool IsUpdateAvailable { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowUpdateAvailableNotification))]
    [NotifyPropertyChangedFor(nameof(CanDisplayGlobalNotification))]
    public partial bool IsUpdateReadyToInstall { get; set; }

    [ObservableProperty]
    public partial string UpdateMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "Ready";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOsImagesTabActive))]
    public partial int CurrentTabIndex { get; set; }

    /// <summary>Gets a value indicating whether the update-available notification bar should be shown.</summary>
    public bool ShowUpdateAvailableNotification => IsUpdateAvailable && !IsUpdateReadyToInstall;

    /// <summary>Gets a value indicating whether the global notification InfoBar can be shown beneath higher-priority update alerts.</summary>
    public bool CanDisplayGlobalNotification => IsGlobalNotificationVisible && !IsUpdateReadyToInstall && !IsUpdateAvailable;

    /// <summary>Gets a value indicating whether the active tab is the OS Images tab.</summary>
    public bool IsOsImagesTabActive => CurrentTabIndex == (int)LibraryTabIndex.OsImages;

    /// <summary>Gets the count of currently selected items across active library tabs.</summary>
    public int SelectedGamesCount => SelectionCoordinator.SelectedCount;

    /// <summary>Gets a value indicating whether at least one library item is selected.</summary>
    public bool HasSelectedGames => SelectionCoordinator.HasSelection;

    /// <summary>Gets the total byte size of currently selected library items.</summary>
    public long SelectedGamesTotalBytes => SelectionCoordinator.SelectedTotalBytes;

    /// <summary>Gets the total calculated price of currently selected library items.</summary>
    public int SelectedGamesTotalPrice => SelectionCoordinator.SelectedTotalPrice;

    private bool IsDisposed => Volatile.Read(ref _isDisposed) != 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShellViewModel"/> class.
    /// </summary>
    public ShellViewModel(
        ILogger<ShellViewModel> logger,
        IWindowService windowService,
        ISettingsService settingsService,
        IUpdateService updateService,
        IDispatcherService dispatcherService,
        ILibrarySelectionCoordinator selectionCoordinator,
        ILibraryCatalogService libraryCatalog,
        GamesTabViewModel gamesTabViewModel,
        AppsTabViewModel appsTabViewModel,
        TvAndFilmsTabViewModel tvAndFilmsTabViewModel,
        OsImagesTabViewModel osImagesTabViewModel,
        TransferQueueViewModel transferQueueViewModel,
        SmartAdderViewModel smartAdderViewModel,
        IMessenger messenger)
        : base(messenger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(windowService);
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(updateService);
        ArgumentNullException.ThrowIfNull(dispatcherService);
        ArgumentNullException.ThrowIfNull(selectionCoordinator);
        ArgumentNullException.ThrowIfNull(libraryCatalog);
        ArgumentNullException.ThrowIfNull(gamesTabViewModel);
        ArgumentNullException.ThrowIfNull(appsTabViewModel);
        ArgumentNullException.ThrowIfNull(tvAndFilmsTabViewModel);
        ArgumentNullException.ThrowIfNull(osImagesTabViewModel);
        ArgumentNullException.ThrowIfNull(transferQueueViewModel);
        ArgumentNullException.ThrowIfNull(smartAdderViewModel);

        _logger = logger;
        _windowService = windowService;
        _settingsService = settingsService;
        _updateService = updateService;
        _dispatcherService = dispatcherService;

        SelectionCoordinator = selectionCoordinator;
        LibraryCatalog = libraryCatalog;
        GamesTabViewModel = gamesTabViewModel;
        AppsTabViewModel = appsTabViewModel;
        TvAndFilmsTabViewModel = tvAndFilmsTabViewModel;
        OsImagesTabViewModel = osImagesTabViewModel;
        TransferQueueViewModel = transferQueueViewModel;
        SmartAdderViewModel = smartAdderViewModel;

        SelectionCoordinator.SelectionChanged += OnSelectionCoordinatorChanged;
        TransferQueueViewModel.BatchCompleted += OnTransferBatchCompleted;
        TransferQueueViewModel.PropertyChanged += OnTransferQueueViewModelPropertyChanged;
        LibraryCatalog.PropertyChanged += OnLibraryCatalogPropertyChanged;
        OsImagesTabViewModel.PropertyChanged += OnOsImagesTabViewModelPropertyChanged;

        IsActive = true;
    }

    /// <inheritdoc />
    protected override void OnActivated()
    {
        Messenger.Register<TransferBatchCompletedMessage>(this);
    }

    /// <inheritdoc />
    protected override void OnDeactivated()
    {
        Messenger.UnregisterAll(this);
    }

    partial void OnCurrentTabIndexChanged(int value)
    {
        SelectionCoordinator.SetActiveTab(IsOsImagesTabActive);
        CopySelectedGamesCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Initializes the shell coordinator, starting update checks, drive monitoring, and library/ISO discovery.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (IsDisposed)
        {
            return;
        }

        _ = CheckForUpdatesBackgroundAsync();

        _updateCheckTimer ??= new Timer(
            OnUpdateCheckTimerTick,
            null,
            TimeSpan.FromHours(4),
            TimeSpan.FromHours(4));

        await Task.WhenAll(
            TransferQueueViewModel.InitializeAsync(),
            LibraryCatalog.InitializeAsync(),
            OsImagesTabViewModel.ScanOsImagesAsync()).ConfigureAwait(false);
    }

    /// <summary>Scans both standard libraries and OS image folders.</summary>
    [RelayCommand]
    private Task ScanLibraryAsync()
    {
        return Task.WhenAll(
            LibraryCatalog.ScanLibraryAsync(),
            OsImagesTabViewModel.ScanOsImagesAsync());
    }

    /// <summary>
    /// Sends the current library selection to the TransferQueue slice via <see cref="EnqueueTransferRequestMessage"/>.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCopyGames))]
    private void CopySelectedGames()
    {
        RemovableDrive? selectedDrive = TransferQueueViewModel.SelectedDrive;
        IReadOnlyList<GameEntry> selectedEntries = SelectionCoordinator.SelectedEntries;
        if (selectedDrive is null || selectedEntries.Count == 0 || IsOsImagesTabActive)
        {
            return;
        }

        AppSettings settings = _settingsService.LoadSettingsSync();
        List<TransferSourceItem> items = new(selectedEntries.Count);
        foreach (GameEntry entry in selectedEntries)
        {
            int price = FormattingHelpers.CalculatePrice(entry.TotalBytes, settings);
            items.Add(new TransferSourceItem(
                entry.Name,
                entry.FolderPath,
                entry.TotalBytes,
                price,
                entry.HasLargeFiles));
        }

        Messenger.Send(new EnqueueTransferRequestMessage(items, selectedDrive.DriveLetter));
    }

    private bool CanCopyGames()
    {
        return !IsOsImagesTabActive
            && SelectionCoordinator.HasSelection
            && TransferQueueViewModel.SelectedDrive is not null;
    }

    /// <summary>Clears all active item selections across library tabs.</summary>
    [RelayCommand]
    private void ClearSelection()
    {
        SelectionCoordinator.ClearAll();
    }

    /// <summary>Launches Rufus with the currently selected OS image via the OsImageLauncher slice.</summary>
    [RelayCommand]
    private Task OpenInRufusAsync()
    {
        return OsImagesTabViewModel.OpenInRufusCommand.ExecuteAsync(null);
    }

    /// <summary>Opens the application Settings window.</summary>
    [RelayCommand]
    private void OpenSettings()
    {
        _windowService.ShowWindow(AppWindowKind.Settings);
    }

    /// <summary>Opens the transfer History window.</summary>
    [RelayCommand]
    private void OpenHistory()
    {
        _windowService.ShowWindow(AppWindowKind.History);
    }

    /// <summary>Opens the application About dialog window.</summary>
    [RelayCommand]
    private void OpenAbout()
    {
        _windowService.ShowWindow(AppWindowKind.About);
    }

    /// <summary>
    /// Opens the Settings window positioned on adding a source folder matching the currently selected library tab.
    /// </summary>
    [RelayCommand]
    private void AddSourceFolder()
    {
        SettingsOpenAction openAction = (LibraryTabIndex)CurrentTabIndex switch
        {
            LibraryTabIndex.Games => SettingsOpenAction.AddGameFolder,
            LibraryTabIndex.Apps => SettingsOpenAction.AddAppFolder,
            LibraryTabIndex.TvAndFilms => SettingsOpenAction.AddTvAndFilmFolder,
            LibraryTabIndex.OsImages => SettingsOpenAction.AddOsImageFolder,
            _ => SettingsOpenAction.AddOsImageFolder
        };

        _windowService.ShowWindow(AppWindowKind.Settings, openAction);
    }

    /// <summary>Displays the global in-app notification InfoBar and auto-dismisses it after 60 seconds.</summary>
    public void ShowGlobalNotification(string title, string message, bool isSuccess = true)
    {
        if (IsDisposed)
        {
            return;
        }

        _ = _dispatcherService.TryEnqueue(async () =>
        {
            if (IsDisposed)
            {
                return;
            }

            GlobalNotificationTitle = title;
            GlobalNotificationMessage = message;
            IsGlobalNotificationVisible = true;
            GlobalNotificationSeverity = isSuccess ? ValidationSeverity.Success : ValidationSeverity.Error;

            CancelAndDisposeTokenSource(ref _notificationCancellationTokenSource);
            _notificationCancellationTokenSource = new CancellationTokenSource();
            CancellationToken token = _notificationCancellationTokenSource.Token;

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(60), token).ConfigureAwait(false);
                if (!token.IsCancellationRequested && !IsDisposed)
                {
                    _ = _dispatcherService.TryEnqueue(() =>
                    {
                        if (!IsDisposed)
                        {
                            IsGlobalNotificationVisible = false;
                        }
                    });
                }
            }
            catch (TaskCanceledException)
            {
                // Replaced by a newer notification or torn down during disposal.
            }
        });
    }

    /// <inheritdoc />
    public void Receive(TransferBatchCompletedMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (IsDisposed)
        {
            return;
        }

        bool isSuccess = message.FailureCount == 0;
        string summary = isSuccess
            ? $"Completed {message.SuccessCount} item(s) \u2192 {message.TargetDriveLetter} ({FormattingHelpers.FormatBytes(message.TotalBytesTransferred)})"
            : $"Transfer finished on {message.TargetDriveLetter}: {message.SuccessCount} succeeded, {message.FailureCount} failed";

        _ = _dispatcherService.TryEnqueue(() =>
        {
            if (!IsDisposed)
            {
                StatusMessage = summary;
            }
        });

        if ((DateTimeOffset.UtcNow - _lastBatchBannerTimestamp) > TimeSpan.FromSeconds(2))
        {
            string title = isSuccess
                ? $"Transfer Complete ({message.TargetDriveLetter})"
                : $"Transfer Finished With Errors ({message.TargetDriveLetter})";

            ShowGlobalNotification(title, summary, isSuccess);
        }
    }

    /// <summary>Initiates manual download of an available application update.</summary>
    [RelayCommand]
    private async Task DownloadUpdateAsync()
    {
        try
        {
            IsUpdateAvailable = true;
            UpdateMessage = "Downloading update...";

            await _updateService.DownloadUpdateAsync().ConfigureAwait(false);

            _ = _dispatcherService.TryEnqueue(() =>
            {
                if (!IsDisposed)
                {
                    IsUpdateAvailable = false;
                    IsUpdateReadyToInstall = true;
                    UpdateMessage = "Update ready to install. Restart to apply.";
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error downloading application update manually.");
            _ = _dispatcherService.TryEnqueue(() =>
            {
                if (!IsDisposed)
                {
                    UpdateMessage = "Failed to download update.";
                }
            });
        }
    }

    /// <summary>Restarts the application to apply a downloaded update package.</summary>
    [RelayCommand]
    private void RestartAndApplyUpdate()
    {
        _updateService.RestartAndApplyUpdate();
    }

    private void OnSelectionCoordinatorChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(SelectedGamesCount));
        OnPropertyChanged(nameof(SelectedGamesTotalBytes));
        OnPropertyChanged(nameof(SelectedGamesTotalPrice));
        OnPropertyChanged(nameof(HasSelectedGames));
        CopySelectedGamesCommand.NotifyCanExecuteChanged();
    }

    private void OnTransferBatchCompleted(object? sender, (string Title, string Message, bool IsSuccess) args)
    {
        if (IsDisposed)
        {
            return;
        }

        _lastBatchBannerTimestamp = DateTimeOffset.UtcNow;
        ShowGlobalNotification(args.Title, args.Message, args.IsSuccess);
    }

    private void OnTransferQueueViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(TransferQueueViewModel.SelectedDrive):
                CopySelectedGamesCommand.NotifyCanExecuteChanged();
                break;
            case nameof(TransferQueueViewModel.StatusMessage) when !string.IsNullOrEmpty(TransferQueueViewModel.StatusMessage):
                StatusMessage = TransferQueueViewModel.StatusMessage;
                break;
        }
    }

    private void OnLibraryCatalogPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ILibraryCatalogService.StatusMessage) && !string.IsNullOrEmpty(LibraryCatalog.StatusMessage))
        {
            StatusMessage = LibraryCatalog.StatusMessage;
        }
    }

    private void OnOsImagesTabViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OsImagesTabViewModel.StatusMessage) && !string.IsNullOrEmpty(OsImagesTabViewModel.StatusMessage))
        {
            StatusMessage = OsImagesTabViewModel.StatusMessage;
        }
    }

    private void OnUpdateCheckTimerTick(object? state)
    {
        if (!IsDisposed)
        {
            _ = _dispatcherService.TryEnqueue(() => CheckForUpdatesBackgroundAsync());
        }
    }

    private async Task CheckForUpdatesBackgroundAsync()
    {
        if (IsDisposed || Interlocked.CompareExchange(ref _isCheckingForUpdates, 1, 0) != 0)
        {
            return;
        }

        try
        {
            bool hasUpdate = await _updateService.CheckForUpdatesAsync().ConfigureAwait(false);
            if (!hasUpdate || IsDisposed)
            {
                return;
            }

            AppSettings settings = await _settingsService.LoadSettingsAsync().ConfigureAwait(false);
            if (settings.AutoDownloadUpdates)
            {
                _ = _dispatcherService.TryEnqueue(() =>
                {
                    if (!IsDisposed)
                    {
                        IsUpdateAvailable = true;
                        UpdateMessage = "Downloading update in background...";
                    }
                });

                await _updateService.DownloadUpdateAsync().ConfigureAwait(false);

                _ = _dispatcherService.TryEnqueue(() =>
                {
                    if (!IsDisposed)
                    {
                        IsUpdateAvailable = false;
                        IsUpdateReadyToInstall = true;
                        UpdateMessage = "Update ready to install. Restart to apply.";
                    }
                });
            }
            else
            {
                _ = _dispatcherService.TryEnqueue(() =>
                {
                    if (!IsDisposed)
                    {
                        IsUpdateAvailable = true;
                        UpdateMessage = "A new update is available!";
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking for application updates in background.");
        }
        finally
        {
            _ = Interlocked.Exchange(ref _isCheckingForUpdates, 0);
        }
    }

    private static void CancelAndDisposeTokenSource(ref CancellationTokenSource? cts)
    {
        CancellationTokenSource? previous = Interlocked.Exchange(ref cts, null);
        if (previous is not null)
        {
            try
            {
                previous.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Ignore if already disposed.
            }

            previous.Dispose();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        IsActive = false;

        SelectionCoordinator.SelectionChanged -= OnSelectionCoordinatorChanged;
        TransferQueueViewModel.BatchCompleted -= OnTransferBatchCompleted;
        TransferQueueViewModel.PropertyChanged -= OnTransferQueueViewModelPropertyChanged;
        LibraryCatalog.PropertyChanged -= OnLibraryCatalogPropertyChanged;
        OsImagesTabViewModel.PropertyChanged -= OnOsImagesTabViewModelPropertyChanged;

        _ = (_updateCheckTimer?.Change(Timeout.Infinite, Timeout.Infinite));
        _updateCheckTimer?.Dispose();
        _updateCheckTimer = null;

        CancelAndDisposeTokenSource(ref _notificationCancellationTokenSource);
    }
}

