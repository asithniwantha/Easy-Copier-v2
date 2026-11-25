using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using EasyCopier.Features.Shell.Contracts;
using EasyCopier.Shared.Configuration;
using EasyCopier.Shared.Messages;
using EasyCopier.Shared.Platform;
using EasyCopier.Shared.UI.Helpers;
using Microsoft.Extensions.Logging;

namespace EasyCopier.Features.OsImageLauncher;

/// <summary>
/// Autonomous ViewModel for the OS Images library tab, owning ISO discovery, filtering,
/// sorting, and Rufus launching without cross-slice coupling.
/// </summary>
public sealed partial class OsImagesTabViewModel
    : LibraryTabViewModelBase, ISearchableTabViewModel, IRecipient<SettingsChangedMessage>
{
    private const long Fat32MaxFileSizeBytes = 4_294_967_295L;

    private readonly ISettingsService _settingsService;
    private readonly IIsoBootstrapperService _isoBootstrapperService;
    private readonly IDispatcherService _dispatcherService;
    private readonly ILogger<OsImagesTabViewModel> _logger;
    private readonly List<GameEntry> _allOsImages = [];

    /// <inheritdoc />
    public override LibraryCategory Category => LibraryCategory.OsImage;

    /// <summary>Gets the filtered and sorted OS Image entries.</summary>
    public ObservableCollection<GameEntry> OsImages { get; } = new BulkObservableCollection<GameEntry>();

    /// <summary>Gets the available sorting options for OS Images.</summary>
    public IReadOnlyList<OsImageSortOption> AvailableOsImageSortOptions { get; } = Enum.GetValues<OsImageSortOption>();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmptyOsImagesMessage))]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial OsImageSortOption SelectedOsImageSortOption { get; set; } = OsImageSortOption.DateCreated;

    [ObservableProperty]
    public partial bool IsOsImageSortAscending { get; set; } = false;

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    /// <summary>Gets a value indicating whether the OS Images list is empty and not currently scanning.</summary>
    public bool IsOsImagesEmpty => !IsScanning && OsImages.Count == 0;

    /// <summary>Gets the message displayed when no OS Images are present or match search criteria.</summary>
    public string EmptyOsImagesMessage => FormattingHelpers.FormatEmptyLibraryMessage("OS images", "OS image", SearchText);

    /// <summary>
    /// Initializes a new instance of the <see cref="OsImagesTabViewModel"/> class.
    /// </summary>
    public OsImagesTabViewModel(
        ILibrarySelectionCoordinator selectionCoordinator,
        ISettingsService settingsService,
        IIsoBootstrapperService isoBootstrapperService,
        IDispatcherService dispatcherService,
        IMessenger messenger,
        ILogger<OsImagesTabViewModel> logger)
        : base(selectionCoordinator, messenger)
    {
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(isoBootstrapperService);
        ArgumentNullException.ThrowIfNull(dispatcherService);
        ArgumentNullException.ThrowIfNull(logger);

        _settingsService = settingsService;
        _isoBootstrapperService = isoBootstrapperService;
        _dispatcherService = dispatcherService;
        _logger = logger;

        IsActive = true;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilterAndSort();

    partial void OnSelectedOsImageSortOptionChanged(OsImageSortOption value)
    {
        IsOsImageSortAscending = value switch
        {
            OsImageSortOption.Name => true,
            OsImageSortOption.DateCreated => false,
            OsImageSortOption.Size => false,
            _ => false
        };

        ApplyFilterAndSort();
    }

    partial void OnIsOsImageSortAscendingChanged(bool value) => ApplyFilterAndSort();

    /// <summary>Toggles sort direction between ascending and descending.</summary>
    [RelayCommand]
    private void ToggleOsImageSortDirection()
    {
        IsOsImageSortAscending = !IsOsImageSortAscending;
    }

    /// <summary>Scans configured OS image source folders for <c>.iso</c> disk images.</summary>
    [RelayCommand]
    public async Task ScanOsImagesAsync(CancellationToken cancellationToken = default)
    {
        _ = _dispatcherService.TryEnqueue(() =>
        {
            IsScanning = true;
            OnPropertyChanged(nameof(IsOsImagesEmpty));
        });

        try
        {
            AppSettings settings = await _settingsService.LoadSettingsAsync();
            IReadOnlyList<string> folders = settings.OsImageSourceFolders ?? [];
            List<GameEntry> discovered = await Task.Run(() => ScanFoldersForIsoFiles(folders, cancellationToken), cancellationToken);

            _ = _dispatcherService.TryEnqueue(() =>
            {
                _allOsImages.Clear();
                _allOsImages.AddRange(discovered);
                ApplyFilterAndSort();
            });
        }
        catch (OperationCanceledException)
        {
            // Scan cancelled; keep existing entries intact.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scan OS image source folders.");
        }
        finally
        {
            _ = _dispatcherService.TryEnqueue(() =>
            {
                IsScanning = false;
                OnPropertyChanged(nameof(IsOsImagesEmpty));
                OnPropertyChanged(nameof(EmptyOsImagesMessage));
            });
        }
    }

    /// <summary>Replaces the current in-memory OS image collection (for example when seeded from cache).</summary>
    public void SetEntries(IEnumerable<GameEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        _allOsImages.Clear();
        _allOsImages.AddRange(entries.Where(e => e.Category == LibraryCategory.OsImage));
        ApplyFilterAndSort();
    }

    /// <summary>Launches Rufus with the currently selected OS image.</summary>
    [RelayCommand]
    private async Task OpenInRufusAsync()
    {
        List<GameEntry> selectedImages = SelectionCoordinator.SelectedEntries
            .Where(e => e.Category == LibraryCategory.OsImage)
            .ToList();

        if (selectedImages.Count != 1)
        {
            StatusMessage = "Please select exactly one OS image.";
            return;
        }

        (_, string message) = await _isoBootstrapperService.LaunchWithIsoAsync(selectedImages[0].FolderPath);
        StatusMessage = message;
    }

    /// <inheritdoc />
    public IEnumerable<string> GetSuggestions(string query) => GetSuggestionsFromEntries(OsImages, query);

    /// <inheritdoc />
    public void Receive(SettingsChangedMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.SourcePathsUpdated)
        {
            _ = ScanOsImagesAsync();
        }
    }

    private void ApplyFilterAndSort()
    {
        string query = SearchText?.Trim() ?? string.Empty;
        IEnumerable<GameEntry> filtered = string.IsNullOrEmpty(query)
            ? _allOsImages
            : _allOsImages.Where(e => e.Name.Contains(query, StringComparison.OrdinalIgnoreCase));

        IEnumerable<GameEntry> sorted = SelectedOsImageSortOption switch
        {
            OsImageSortOption.Name => IsOsImageSortAscending
                ? filtered.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                : filtered.OrderByDescending(e => e.Name, StringComparer.OrdinalIgnoreCase),
            OsImageSortOption.DateCreated => IsOsImageSortAscending
                ? filtered.OrderBy(e => e.DateCreated).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                : filtered.OrderByDescending(e => e.DateCreated).ThenByDescending(e => e.Name, StringComparer.OrdinalIgnoreCase),
            OsImageSortOption.Size => IsOsImageSortAscending
                ? filtered.OrderBy(e => e.TotalBytes).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                : filtered.OrderByDescending(e => e.TotalBytes).ThenByDescending(e => e.Name, StringComparer.OrdinalIgnoreCase),
            _ => filtered
        };

        OsImages.UpdateFrom(sorted);
        OnPropertyChanged(nameof(IsOsImagesEmpty));
        OnPropertyChanged(nameof(EmptyOsImagesMessage));
    }

    private List<GameEntry> ScanFoldersForIsoFiles(IReadOnlyList<string> sourceFolders, CancellationToken cancellationToken)
    {
        List<GameEntry> entries = [];
        HashSet<string> processedPaths = new(StringComparer.OrdinalIgnoreCase);

        foreach (string sourceFolder in sourceFolders)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(sourceFolder) || !Directory.Exists(sourceFolder))
            {
                _logger.LogWarning("OS image source folder does not exist: {Path}", sourceFolder);
                continue;
            }

            try
            {
                foreach (string file in Directory.GetFiles(sourceFolder, "*.iso", SearchOption.TopDirectoryOnly))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    if (!processedPaths.Add(file))
                    {
                        continue;
                    }

                    FileInfo fi = new(file);
                    long totalSize = fi.Length;
                    DateTime dateCreated = fi.LastWriteTime < fi.CreationTime && fi.LastWriteTime != DateTime.MinValue
                        ? fi.LastWriteTime
                        : fi.CreationTime;

                    entries.Add(new GameEntry(
                        Path.GetFileNameWithoutExtension(fi.Name),
                        file,
                        totalSize,
                        null,
                        dateCreated,
                        totalSize > Fat32MaxFileSizeBytes,
                        LibraryCategory.OsImage));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error scanning OS image folder: {Path}", sourceFolder);
            }
        }

        return entries;
    }
}

