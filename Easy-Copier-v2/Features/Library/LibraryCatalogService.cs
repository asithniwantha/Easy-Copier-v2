using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using EasyCopier.Features.Library.Models;
using EasyCopier.Features.Library.Scanning;
using EasyCopier.Features.Shell.Contracts;
using EasyCopier.Shared.Configuration;
using EasyCopier.Shared.Messages;
using EasyCopier.Shared.Platform;
using EasyCopier.Shared.UI.Helpers;
using Microsoft.Extensions.Logging;

namespace EasyCopier.Features.Library;

/// <summary>
/// Owns scanned core library collections (Games, Apps, Films/TV), persistent cache validation,
/// and filtering/sorting independently of the top-level Shell ViewModel.
/// </summary>
public sealed partial class LibraryCatalogService
    : ObservableRecipient, ILibraryCatalogService, IRecipient<SettingsChangedMessage>
{
    private readonly ISettingsService _settingsService;
    private readonly ILibraryScannerService _libraryScannerService;
    private readonly ILibraryCacheService _libraryCacheService;
    private readonly ILibraryFilterService _libraryFilterService;
    private readonly IDispatcherService _dispatcherService;
    private readonly ILogger<LibraryCatalogService> _logger;

    private readonly List<GameEntry> _allGames = [];
    private readonly List<GameEntry> _allApps = [];
    private readonly List<GameEntry> _allTvAndFilms = [];

    private CancellationTokenSource? _scanCancellationTokenSource;
    private CancellationTokenSource? _validationCancellationTokenSource;
    private int _isDisposed;

    /// <inheritdoc />
    public ObservableCollection<GameEntry> Games { get; } = new BulkObservableCollection<GameEntry>();

    /// <inheritdoc />
    public ObservableCollection<GameEntry> Apps { get; } = new BulkObservableCollection<GameEntry>();

    /// <inheritdoc />
    public ObservableCollection<GameEntry> TvAndFilms { get; } = new BulkObservableCollection<GameEntry>();

    /// <inheritdoc />
    public IReadOnlyList<GameCategory> AvailableCategories { get; } = Enum.GetValues<GameCategory>();

    /// <inheritdoc />
    public IReadOnlyList<TvAndFilmsSortOption> AvailableTvAndFilmsSortOptions { get; } = Enum.GetValues<TvAndFilmsSortOption>();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGamesEmpty))]
    [NotifyPropertyChangedFor(nameof(IsAppsEmpty))]
    [NotifyPropertyChangedFor(nameof(IsTvAndFilmsEmpty))]
    public partial bool IsScanning { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmptyGamesMessage))]
    [NotifyPropertyChangedFor(nameof(EmptyAppsMessage))]
    [NotifyPropertyChangedFor(nameof(EmptyTvAndFilmsMessage))]
    public partial GameCategory SelectedCategory { get; set; } = GameCategory.All;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmptyGamesMessage))]
    public partial string GamesSearchText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmptyAppsMessage))]
    public partial string AppsSearchText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmptyTvAndFilmsMessage))]
    public partial string TvAndFilmsSearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial TvAndFilmsSortOption SelectedTvAndFilmsSortOption { get; set; } = TvAndFilmsSortOption.DateCreated;

    [ObservableProperty]
    public partial bool IsTvAndFilmsSortAscending { get; set; } = false;

    /// <inheritdoc />
    public bool IsGamesEmpty => !IsScanning && Games.Count == 0;

    /// <inheritdoc />
    public bool IsAppsEmpty => !IsScanning && Apps.Count == 0;

    /// <inheritdoc />
    public bool IsTvAndFilmsEmpty => !IsScanning && TvAndFilms.Count == 0;

    /// <inheritdoc />
    public string EmptyGamesMessage => FormattingHelpers.FormatEmptyLibraryMessage("games", "game", GamesSearchText);

    /// <inheritdoc />
    public string EmptyAppsMessage => FormattingHelpers.FormatEmptyLibraryMessage("apps", "app", AppsSearchText);

    /// <inheritdoc />
    public string EmptyTvAndFilmsMessage => FormattingHelpers.FormatEmptyLibraryMessage("films/TV series", "film/TV", TvAndFilmsSearchText);

    /// <inheritdoc />
    public IRelayCommand ToggleTvAndFilmsSortDirectionCommand => ToggleTvAndFilmsSortDirectionInternalCommand;

    /// <summary>
    /// Initializes a new instance of the <see cref="LibraryCatalogService"/> class.
    /// </summary>
    public LibraryCatalogService(
        ISettingsService settingsService,
        ILibraryScannerService libraryScannerService,
        ILibraryCacheService libraryCacheService,
        ILibraryFilterService libraryFilterService,
        IDispatcherService dispatcherService,
        IMessenger messenger,
        ILogger<LibraryCatalogService> logger)
        : base(messenger)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _libraryScannerService = libraryScannerService ?? throw new ArgumentNullException(nameof(libraryScannerService));
        _libraryCacheService = libraryCacheService ?? throw new ArgumentNullException(nameof(libraryCacheService));
        _libraryFilterService = libraryFilterService ?? throw new ArgumentNullException(nameof(libraryFilterService));
        _dispatcherService = dispatcherService ?? throw new ArgumentNullException(nameof(dispatcherService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        IsActive = true;
    }

    protected override void OnActivated()
    {
        Messenger.Register<SettingsChangedMessage>(this);
    }

    protected override void OnDeactivated()
    {
        Messenger.UnregisterAll(this);
    }

    partial void OnGamesSearchTextChanged(string value) => ApplyGamesFilter();

    partial void OnAppsSearchTextChanged(string value) => ApplyAppsFilter();

    partial void OnTvAndFilmsSearchTextChanged(string value) => ApplyTvAndFilmsFilter();

    partial void OnSelectedCategoryChanged(GameCategory value)
    {
        ApplyGamesFilter();
        ApplyAppsFilter();
    }

    partial void OnSelectedTvAndFilmsSortOptionChanged(TvAndFilmsSortOption value)
    {
        IsTvAndFilmsSortAscending = value switch
        {
            TvAndFilmsSortOption.Name => true,
            TvAndFilmsSortOption.DateCreated => false,
            _ => true
        };
        ApplyTvAndFilmsFilter();
    }

    partial void OnIsTvAndFilmsSortAscendingChanged(bool value) => ApplyTvAndFilmsFilter();

    [RelayCommand]
    private void ToggleTvAndFilmsSortDirectionInternal()
    {
        IsTvAndFilmsSortAscending = !IsTvAndFilmsSortAscending;
    }

    /// <inheritdoc />
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            AppSettings settings = await _settingsService.LoadSettingsAsync().ConfigureAwait(false);
            LibraryCacheSnapshot? cache = await _libraryCacheService.LoadCacheAsync().ConfigureAwait(false);

            if (cache != null)
            {
                _ = _dispatcherService.TryEnqueue(() =>
                {
                    _allGames.Clear();
                    _allGames.AddRange(cache.Games ?? []);
                    _allApps.Clear();
                    _allApps.AddRange(cache.Apps ?? []);
                    _allTvAndFilms.Clear();
                    _allTvAndFilms.AddRange(cache.TvAndFilms ?? []);
                    ApplyAllFilters();
                    StatusMessage = $"Loaded cached library: {_allGames.Count} game(s), {_allApps.Count} app(s), {_allTvAndFilms.Count} film/TV(s)";
                });

                await ValidateAndRefreshCacheAsync(cache, settings, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (settings.AutoScanOnStartup && HasCoreSourceFolders(settings))
            {
                await ScanLibraryAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Library initialization was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during library initialization.");
            _ = _dispatcherService.TryEnqueue(() => StatusMessage = $"Library load error: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task ScanLibraryAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            CancelAndReplaceTokenSource(ref _scanCancellationTokenSource, linkedCts);
            CancellationToken token = linkedCts.Token;

            _ = _dispatcherService.TryEnqueue(() =>
            {
                IsScanning = true;
                _allGames.Clear();
                _allApps.Clear();
                _allTvAndFilms.Clear();
                Games.Clear();
                Apps.Clear();
                TvAndFilms.Clear();
                OnPropertyChanged(nameof(IsGamesEmpty));
                OnPropertyChanged(nameof(IsAppsEmpty));
                OnPropertyChanged(nameof(IsTvAndFilmsEmpty));
            });

            AppSettings settings = await _settingsService.LoadSettingsAsync().ConfigureAwait(false);
            if (!HasCoreSourceFolders(settings))
            {
                await _libraryCacheService.InvalidateCacheAsync().ConfigureAwait(false);
                _ = _dispatcherService.TryEnqueue(() =>
                {
                    StatusMessage = "No source folders configured. Please add folders in Settings.";
                });
                return;
            }

            Progress<string> progress = new(message =>
            {
                _ = _dispatcherService.TryEnqueue(() => StatusMessage = message);
            });

            (IReadOnlyList<GameEntry> games, IReadOnlyList<GameEntry> apps, IReadOnlyList<GameEntry> tvAndFilms) =
                await _libraryScannerService.ScanCoreLibrariesAsync(settings, progress, token).ConfigureAwait(false);

            _ = _dispatcherService.TryEnqueue(() =>
            {
                _allGames.Clear();
                _allGames.AddRange(games);
                _allApps.Clear();
                _allApps.AddRange(apps);
                _allTvAndFilms.Clear();
                _allTvAndFilms.AddRange(tvAndFilms);

                ApplyAllFilters();

                StatusMessage = _allGames.Count == 0 && _allApps.Count == 0 && _allTvAndFilms.Count == 0
                    ? "No items found in configured folders"
                    : $"Found {_allGames.Count} game(s), {_allApps.Count} app(s), {_allTvAndFilms.Count} film/TV(s)";
            });

            settings.LastScanTime = DateTime.Now;
            await _settingsService.SaveSettingsAsync(settings).ConfigureAwait(false);

            List<GameEntry> allEntries = [.. games, .. apps, .. tvAndFilms];
            string? snapshotWarning = await _libraryCacheService.CreateAndSaveSnapshotAsync(allEntries, settings).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(snapshotWarning))
            {
                _ = _dispatcherService.TryEnqueue(() => StatusMessage = snapshotWarning);
            }
        }
        catch (OperationCanceledException)
        {
            _ = _dispatcherService.TryEnqueue(() => StatusMessage = "Scan cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during library scan.");
            _ = _dispatcherService.TryEnqueue(() => StatusMessage = $"Scan error: {ex.Message}");
        }
        finally
        {
            _ = _dispatcherService.TryEnqueue(() =>
            {
                IsScanning = false;
                OnPropertyChanged(nameof(IsGamesEmpty));
                OnPropertyChanged(nameof(IsAppsEmpty));
                OnPropertyChanged(nameof(IsTvAndFilmsEmpty));
            });
        }
    }

    /// <inheritdoc />
    public void Receive(SettingsChangedMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message.SourcePathsUpdated && Volatile.Read(ref _isDisposed) == 0)
        {
            _ = _dispatcherService.TryEnqueue(() => ScanLibraryAsync());
        }
    }

    private async Task ValidateAndRefreshCacheAsync(
        LibraryCacheSnapshot cache,
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        try
        {
            CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            CancelAndReplaceTokenSource(ref _validationCancellationTokenSource, linkedCts);

            CacheValidationOutcome validationResult = await _libraryCacheService
                .ValidateCacheAsync(cache, settings, linkedCts.Token)
                .ConfigureAwait(false);

            if (Volatile.Read(ref _isDisposed) != 0)
            {
                return;
            }

            if (validationResult.Result == CacheValidationResult.Valid)
            {
                _ = _dispatcherService.TryEnqueue(() =>
                {
                    StatusMessage = $"Library is up to date: {_allGames.Count} game(s), {_allApps.Count} app(s), {_allTvAndFilms.Count} film/TV(s)";
                });
                return;
            }

            _ = _dispatcherService.TryEnqueue(() =>
            {
                StatusMessage = "Changes detected - Rescanning library...";
            });

            await ScanLibraryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Library cache validation was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error validating library cache.");
            _ = _dispatcherService.TryEnqueue(() => StatusMessage = $"Validation error: {ex.Message}");
        }
    }

    private static bool HasCoreSourceFolders(AppSettings settings)
        => settings.GameSourceFolders.Count > 0
           || settings.AppSourceFolders.Count > 0
           || (settings.TvAndFilmSourceFolders?.Count ?? 0) > 0;

    private void ApplyAllFilters()
    {
        ApplyGamesFilter();
        ApplyAppsFilter();
        ApplyTvAndFilmsFilter();
    }

    private void ApplyGamesFilter()
    {
        Games.UpdateFrom(_libraryFilterService.FilterEntries(_allGames, GamesSearchText, SelectedCategory));
        OnPropertyChanged(nameof(IsGamesEmpty));
        OnPropertyChanged(nameof(EmptyGamesMessage));
    }

    private void ApplyAppsFilter()
    {
        Apps.UpdateFrom(_libraryFilterService.FilterEntries(_allApps, AppsSearchText, SelectedCategory));
        OnPropertyChanged(nameof(IsAppsEmpty));
        OnPropertyChanged(nameof(EmptyAppsMessage));
    }

    private void ApplyTvAndFilmsFilter()
    {
        TvAndFilms.UpdateFrom(_libraryFilterService.SortTvAndFilms(
            _libraryFilterService.FilterEntries(_allTvAndFilms, TvAndFilmsSearchText, SelectedCategory),
            SelectedTvAndFilmsSortOption,
            IsTvAndFilmsSortAscending));
        OnPropertyChanged(nameof(IsTvAndFilmsEmpty));
        OnPropertyChanged(nameof(EmptyTvAndFilmsMessage));
    }

    private static void CancelAndReplaceTokenSource(ref CancellationTokenSource? field, CancellationTokenSource? next)
    {
        CancellationTokenSource? previous = Interlocked.Exchange(ref field, next);
        if (previous != null)
        {
            previous.Cancel();
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
        CancelAndReplaceTokenSource(ref _scanCancellationTokenSource, null);
        CancelAndReplaceTokenSource(ref _validationCancellationTokenSource, null);
        GC.SuppressFinalize(this);
    }
}
