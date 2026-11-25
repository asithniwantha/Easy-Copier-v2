using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyCopier.Features.Library.Models;
using EasyCopier.Features.Library.Scanning;
using EasyCopier.Features.Shell.Contracts;
using EasyCopier.Shared.Configuration;
using EasyCopier.Shared.Platform;
using EasyCopier.Shared.UI.Helpers;
using Microsoft.Extensions.Logging;

namespace EasyCopier.Features.Library.Duplicates;

/// <summary>
/// Represents an observable view model for a single duplicate file or folder candidate.
/// </summary>
public sealed partial class DuplicateItemEntryViewModel : ObservableObject
{
    private readonly Action _onSelectionChanged;

    public string Name { get; }
    public string FolderPath { get; }
    public LibraryCategory Category { get; }
    public long TotalBytes { get; }
    public DateTime DateCreated { get; }

    public string FormattedSize => FormattingHelpers.FormatBytes(TotalBytes);
    public string FormattedDate => DateCreated.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    public string CategoryGlyph => Category switch
    {
        LibraryCategory.Game => "\uE7FC",
        LibraryCategory.App => "\uE71D",
        LibraryCategory.TvAndFilm => "\uE714",
        LibraryCategory.OsImage => "\uE7C5",
        _ => "\uE8B7"
    };

    [ObservableProperty]
    public partial bool IsMarkedForDeletion { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DuplicateItemEntryViewModel"/> class.
    /// </summary>
    public DuplicateItemEntryViewModel(DuplicateItemEntry entry, Action onSelectionChanged)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _onSelectionChanged = onSelectionChanged ?? throw new ArgumentNullException(nameof(onSelectionChanged));

        Name = entry.Name;
        FolderPath = entry.FolderPath;
        Category = entry.Category;
        TotalBytes = entry.TotalBytes;
        DateCreated = entry.DateCreated;
    }

    partial void OnIsMarkedForDeletionChanged(bool value) => _onSelectionChanged();
}

/// <summary>
/// Represents an observable grouping of duplicate item entries sharing the same name.
/// </summary>
public sealed partial class DuplicateGroupViewModel : ObservableObject
{
    public string ItemName { get; }
    public ObservableCollection<DuplicateItemEntryViewModel> Entries { get; }

    public long TotalBytes => Entries.Sum(e => e.TotalBytes);
    public string FormattedTotalBytes => FormattingHelpers.FormatBytes(TotalBytes);
    public int CopiesCount => Entries.Count;
    public string CopiesCountText => $"{CopiesCount} copies";

    /// <summary>
    /// Initializes a new instance of the <see cref="DuplicateGroupViewModel"/> class.
    /// </summary>
    public DuplicateGroupViewModel(string itemName, IEnumerable<DuplicateItemEntryViewModel> entries)
    {
        ItemName = itemName;
        Entries = new ObservableCollection<DuplicateItemEntryViewModel>(entries);
    }

    /// <summary>
    /// Marks all copies in this group for deletion except the newest created one.
    /// </summary>
    public void SelectOlderCopies()
    {
        DuplicateItemEntryViewModel? newest = Entries.OrderByDescending(e => e.DateCreated).FirstOrDefault();
        foreach (DuplicateItemEntryViewModel entry in Entries)
        {
            entry.IsMarkedForDeletion = entry != newest;
        }
    }

    /// <summary>
    /// Marks all copies in this group for deletion except the oldest created one.
    /// </summary>
    public void SelectNewerCopies()
    {
        DuplicateItemEntryViewModel? oldest = Entries.OrderBy(e => e.DateCreated).FirstOrDefault();
        foreach (DuplicateItemEntryViewModel entry in Entries)
        {
            entry.IsMarkedForDeletion = entry != oldest;
        }
    }

    /// <summary>
    /// Unmarks all copies in this group.
    /// </summary>
    public void DeselectAll()
    {
        foreach (DuplicateItemEntryViewModel entry in Entries)
        {
            entry.IsMarkedForDeletion = false;
        }
    }
}

/// <summary>
/// ViewModel driving the interactive duplicate item cleanup dialog.
/// </summary>
public sealed partial class DuplicateManagerViewModel : CloseableViewModelBase, IDisposable
{
    private readonly ILibraryScannerService _libraryScannerService;
    private readonly ISafeDeletionService _safeDeletionService;
    private readonly ISettingsService _settingsService;
    private readonly IDialogService _dialogService;
    private readonly IDispatcherService _dispatcherService;
    private readonly ILogger<DuplicateManagerViewModel> _logger;
    private CancellationTokenSource? _scanCts;

    public ObservableCollection<DuplicateGroupViewModel> DuplicateGroups { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoDuplicates))]
    [NotifyPropertyChangedFor(nameof(CanDelete))]
    public partial bool IsScanning { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDelete))]
    public partial bool IsDeleting { get; set; }

    [ObservableProperty]
    public partial double DeletionProgress { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "Ready to scan for duplicates.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyPropertyChangedFor(nameof(CanDelete))]
    public partial int MarkedForDeletionCount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedRecoverableBytes))]
    public partial long TotalRecoverableBytes { get; set; }

    public string FormattedRecoverableBytes => FormattingHelpers.FormatBytes(TotalRecoverableBytes);
    public bool HasSelection => MarkedForDeletionCount > 0;
    public bool CanDelete => !IsDeleting && !IsScanning && HasSelection;
    public bool HasDuplicates => DuplicateGroups.Count > 0;
    public bool HasNoDuplicates => !IsScanning && DuplicateGroups.Count == 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="DuplicateManagerViewModel"/> class.
    /// </summary>
    public DuplicateManagerViewModel(
        ILibraryScannerService libraryScannerService,
        ISafeDeletionService safeDeletionService,
        ISettingsService settingsService,
        IDialogService dialogService,
        IDispatcherService dispatcherService,
        ILogger<DuplicateManagerViewModel> logger)
    {
        _libraryScannerService = libraryScannerService ?? throw new ArgumentNullException(nameof(libraryScannerService));
        _safeDeletionService = safeDeletionService ?? throw new ArgumentNullException(nameof(safeDeletionService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _dispatcherService = dispatcherService ?? throw new ArgumentNullException(nameof(dispatcherService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Asynchronously initiates a scan across all configured source library directories to locate duplicates.
    /// </summary>
    [RelayCommand]
    public async Task ScanDuplicatesAsync()
    {
        if (IsScanning || IsDeleting)
        {
            return;
        }

        if (_scanCts != null)
        {
            await _scanCts.CancelAsync().ConfigureAwait(false);
            _scanCts.Dispose();
        }

        _scanCts = new CancellationTokenSource();
        CancellationToken token = _scanCts.Token;

        IsScanning = true;
        StatusMessage = "Scanning library folders for duplicate entries...";
        DuplicateGroups.Clear();
        RecalculateSelectionMetrics();

        try
        {
            AppSettings settings = _settingsService.LoadSettingsSync();

            Progress<string> progress = new(msg =>
            {
                _ = _dispatcherService.TryEnqueue(() => StatusMessage = msg);
            });

            IReadOnlyList<DuplicateGroup> duplicateGroups = await _libraryScannerService
                .GetDuplicateGroupsAsync(settings, progress, token)
                .ConfigureAwait(false);

            _ = _dispatcherService.TryEnqueue(() =>
            {
                foreach (DuplicateGroup group in duplicateGroups)
                {
                    List<DuplicateItemEntryViewModel> entries = group.Entries
                        .Select(e => new DuplicateItemEntryViewModel(e, RecalculateSelectionMetrics))
                        .ToList();

                    DuplicateGroups.Add(new DuplicateGroupViewModel(group.ItemName, entries));
                }

                OnPropertyChanged(nameof(HasDuplicates));
                OnPropertyChanged(nameof(HasNoDuplicates));

                StatusMessage = DuplicateGroups.Count > 0
                    ? $"Found {DuplicateGroups.Count} duplicate items ({DuplicateGroups.Sum(g => g.CopiesCount)} total copies)."
                    : "No duplicate files or folders found across monitored libraries.";

                RecalculateSelectionMetrics();
            });
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Duplicate scan canceled.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scan for duplicate library entries.");
            StatusMessage = "An error occurred during duplicate scanning.";
        }
        finally
        {
            _ = _dispatcherService.TryEnqueue(() =>
            {
                IsScanning = false;
                OnPropertyChanged(nameof(HasNoDuplicates));
            });
        }
    }

    [RelayCommand]
    public void SelectOlderCopies()
    {
        foreach (DuplicateGroupViewModel group in DuplicateGroups)
        {
            group.SelectOlderCopies();
        }

        RecalculateSelectionMetrics();
    }

    [RelayCommand]
    public void SelectNewerCopies()
    {
        foreach (DuplicateGroupViewModel group in DuplicateGroups)
        {
            group.SelectNewerCopies();
        }

        RecalculateSelectionMetrics();
    }

    [RelayCommand]
    public void DeselectAll()
    {
        foreach (DuplicateGroupViewModel group in DuplicateGroups)
        {
            group.DeselectAll();
        }

        RecalculateSelectionMetrics();
    }

    [RelayCommand]
    public async Task DeleteSelectedAsync()
    {
        if (!CanDelete)
        {
            return;
        }

        List<DuplicateItemEntryViewModel> itemsToDelete = DuplicateGroups
            .SelectMany(g => g.Entries)
            .Where(e => e.IsMarkedForDeletion)
            .ToList();

        if (itemsToDelete.Count == 0)
        {
            return;
        }

        string formattedBytes = FormattingHelpers.FormatBytes(itemsToDelete.Sum(i => i.TotalBytes));
        string message = string.Format(
            CultureInfo.InvariantCulture,
            "Are you sure you want to move {0} duplicate item(s) ({1}) to the Windows Recycle Bin?\n\nYou can restore them from the Recycle Bin if needed.",
            itemsToDelete.Count,
            formattedBytes);

        bool confirmed = await _dialogService.ShowConfirmationDialogAsync(
            "Confirm Recycle Bin Cleanup",
            message,
            primaryButtonText: "Recycle Items",
            closeButtonText: "Cancel").ConfigureAwait(false);

        if (!confirmed)
        {
            return;
        }

        IsDeleting = true;
        DeletionProgress = 0;
        StatusMessage = "Moving selected duplicates to the Windows Recycle Bin...";

        try
        {
            List<(string Path, long Bytes)> targets = itemsToDelete
                .Select(e => (e.FolderPath, e.TotalBytes))
                .ToList();

            Progress<double> progress = new(p =>
            {
                _ = _dispatcherService.TryEnqueue(() => DeletionProgress = p);
            });

            SafeDeletionResult result = await _safeDeletionService
                .BatchDeleteToRecycleBinAsync(targets, progress)
                .ConfigureAwait(false);

            _ = _dispatcherService.TryEnqueue(() =>
            {
                List<DuplicateGroupViewModel> groupsToRemove = [];

                foreach (DuplicateGroupViewModel group in DuplicateGroups)
                {
                    List<DuplicateItemEntryViewModel> entriesToRemove = group.Entries
                        .Where(e => e.IsMarkedForDeletion && targets.Any(t => string.Equals(t.Path, e.FolderPath, StringComparison.OrdinalIgnoreCase)))
                        .ToList();

                    foreach (DuplicateItemEntryViewModel entry in entriesToRemove)
                    {
                        _ = group.Entries.Remove(entry);
                    }

                    if (group.Entries.Count <= 1)
                    {
                        groupsToRemove.Add(group);
                    }
                }

                foreach (DuplicateGroupViewModel emptyGroup in groupsToRemove)
                {
                    _ = DuplicateGroups.Remove(emptyGroup);
                }

                OnPropertyChanged(nameof(HasDuplicates));
                OnPropertyChanged(nameof(HasNoDuplicates));
                RecalculateSelectionMetrics();

                StatusMessage = string.Format(
                    CultureInfo.InvariantCulture,
                    "Recycled {0} item(s) successfully ({1} recovered).",
                    result.SucceededCount,
                    FormattingHelpers.FormatBytes(result.RecoveredBytes));
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete batch Recycle Bin deletion.");
            StatusMessage = "An error occurred during cleanup.";
        }
        finally
        {
            _ = _dispatcherService.TryEnqueue(() =>
            {
                IsDeleting = false;
                DeletionProgress = 0;
            });
        }
    }

    private void RecalculateSelectionMetrics()
    {
        List<DuplicateItemEntryViewModel> marked = DuplicateGroups
            .SelectMany(g => g.Entries)
            .Where(e => e.IsMarkedForDeletion)
            .ToList();

        MarkedForDeletionCount = marked.Count;
        TotalRecoverableBytes = marked.Sum(e => e.TotalBytes);

        OnPropertyChanged(nameof(FormattedRecoverableBytes));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(CanDelete));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _scanCts?.Dispose();
        _scanCts = null;
    }
}

