using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using EasyCopier.Features.Shell.Contracts;
using EasyCopier.Features.TransferQueue.Models;
using EasyCopier.Shared.Messages;
using EasyCopier.Shared.Models;
using EasyCopier.Shared.Platform;
using EasyCopier.Shared.UI.Helpers;
using Microsoft.Extensions.Logging;

namespace EasyCopier.Features.TransferQueue;

/// <summary>
/// ViewModel managing removable drive selection, pre-copy validation, destination folder overrides,
/// conflict prompts, and background queue job orchestration.
/// </summary>
public sealed partial class TransferQueueViewModel
    : ObservableRecipient, IRecipient<EnqueueTransferRequestMessage>, IDisposable
{
    private readonly IDriveCoordinatorService _driveCoordinatorService;
    private readonly ITransferQueueService _transferQueueService;
    private readonly IFileTransferService _fileTransferService;
    private readonly IFolderPickerService _folderPickerService;
    private readonly IFileSystemService _fileSystemService;
    private readonly IConflictPromptService _conflictPromptService;
    private readonly IProcessService _processService;
    private readonly IDispatcherService _dispatcherService;
    private readonly ILogger<TransferQueueViewModel> _logger;

    private int _isDisposed;

    /// <summary>Gets the observable collection of discovered removable drives.</summary>
    public ObservableCollection<RemovableDrive> AvailableDrives { get; } = [];

    /// <summary>Gets the observable collection of validation warnings or errors for the current copy request.</summary>
    public ObservableCollection<ValidationResult> ValidationMessages { get; } = [];

    /// <summary>Gets the live collection of transfer queue jobs.</summary>
    public ObservableCollection<TransferQueueItem> TransferQueue => _transferQueueService.QueueItems;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedDrive))]
    [NotifyPropertyChangedFor(nameof(DriveSpaceSummary))]
    [NotifyPropertyChangedFor(nameof(DriveDetailsSummary))]
    public partial RemovableDrive? SelectedDrive { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCustomDestination))]
    public partial string? CustomDestinationPath { get; set; }

    [ObservableProperty]
    public partial bool IsTransferring { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    /// <summary>Gets a value indicating whether a target removable drive is currently selected.</summary>
    public bool HasSelectedDrive => SelectedDrive is not null;

    /// <summary>Gets a value indicating whether a custom destination directory override is specified.</summary>
    public bool HasCustomDestination => !string.IsNullOrEmpty(CustomDestinationPath);

    /// <summary>Gets a value indicating whether any items exist in the transfer queue.</summary>
    public bool HasQueueItems => TransferQueue.Count > 0;

    /// <summary>Gets a value indicating whether the transfer queue is empty.</summary>
    public bool IsQueueEmpty => TransferQueue.Count == 0;

    /// <summary>Gets the total number of items in the transfer queue.</summary>
    public int QueueCount => TransferQueue.Count;

    /// <summary>Gets a formatted summary of free and total capacity for <see cref="SelectedDrive"/>.</summary>
    public string DriveSpaceSummary => SelectedDrive is null
        ? string.Empty
        : $"{FormattingHelpers.FormatBytes(SelectedDrive.FreeBytes)} free of {FormattingHelpers.FormatBytes(SelectedDrive.TotalBytes)}";

    /// <summary>Gets a formatted summary of drive letter, brand, and file system for <see cref="SelectedDrive"/>.</summary>
    public string DriveDetailsSummary => SelectedDrive is null
        ? string.Empty
        : $"{SelectedDrive.DriveLetter} \u2022 {SelectedDrive.Brand} \u2022 {SelectedDrive.FileSystem}";

    /// <summary>Occurs when a new transfer job is added to the queue.</summary>
    public event EventHandler? ItemQueued;

    /// <summary>Occurs when a drive batch finishes execution.</summary>
    public event EventHandler<(string Title, string Message, bool IsSuccess)>? BatchCompleted;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransferQueueViewModel"/> class.
    /// </summary>
    public TransferQueueViewModel(
        IDriveCoordinatorService driveCoordinatorService,
        ITransferQueueService transferQueueService,
        IFileTransferService fileTransferService,
        IFolderPickerService folderPickerService,
        IFileSystemService fileSystemService,
        IConflictPromptService conflictPromptService,
        IProcessService processService,
        IDispatcherService dispatcherService,
        IMessenger messenger,
        ILogger<TransferQueueViewModel> logger)
        : base(messenger)
    {
        ArgumentNullException.ThrowIfNull(driveCoordinatorService);
        ArgumentNullException.ThrowIfNull(transferQueueService);
        ArgumentNullException.ThrowIfNull(fileTransferService);
        ArgumentNullException.ThrowIfNull(folderPickerService);
        ArgumentNullException.ThrowIfNull(fileSystemService);
        ArgumentNullException.ThrowIfNull(conflictPromptService);
        ArgumentNullException.ThrowIfNull(processService);
        ArgumentNullException.ThrowIfNull(dispatcherService);
        ArgumentNullException.ThrowIfNull(logger);

        _driveCoordinatorService = driveCoordinatorService;
        _transferQueueService = transferQueueService;
        _fileTransferService = fileTransferService;
        _folderPickerService = folderPickerService;
        _fileSystemService = fileSystemService;
        _conflictPromptService = conflictPromptService;
        _processService = processService;
        _dispatcherService = dispatcherService;
        _logger = logger;

        _driveCoordinatorService.DrivesChanged += OnDrivesChanged;
        _transferQueueService.ItemCompleted += OnQueueItemCompleted;
        _transferQueueService.BatchCompleted += OnQueueBatchCompleted;
        TransferQueue.CollectionChanged += OnTransferQueueCollectionChanged;

        IsActive = true;
    }

    private bool IsDisposed => Volatile.Read(ref _isDisposed) != 0;

    /// <summary>Starts hardware drive monitoring and performs initial removable drive enumeration.</summary>
    public async Task InitializeAsync()
    {
        if (IsDisposed)
        {
            return;
        }

        _driveCoordinatorService.StartWatching();
        await RefreshDrivesAsync();
    }

    /// <summary>Asynchronously queries connected removable storage devices and updates <see cref="AvailableDrives"/>.</summary>
    [RelayCommand]
    public async Task RefreshDrivesAsync()
    {
        try
        {
            StatusMessage = "Refreshing drives...";
            IReadOnlyList<RemovableDrive> drives = await _driveCoordinatorService.GetRemovableDrivesAsync();

            AvailableDrives.UpdateFrom(drives);

            if (AvailableDrives.Count == 0)
            {
                StatusMessage = "No removable drives found";
                SelectedDrive = null;
            }
            else
            {
                StatusMessage = $"Found {AvailableDrives.Count} removable drive(s)";
                if (SelectedDrive is null || !AvailableDrives.Any(d => string.Equals(d.DriveLetter, SelectedDrive.DriveLetter, StringComparison.OrdinalIgnoreCase)))
                {
                    SelectedDrive = AvailableDrives[0];
                }
                else
                {
                    // Refresh SelectedDrive reference to latest space metrics
                    RemovableDrive updated = AvailableDrives.First(d => string.Equals(d.DriveLetter, SelectedDrive.DriveLetter, StringComparison.OrdinalIgnoreCase));
                    SelectedDrive = updated;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error refreshing removable drives.");
            StatusMessage = $"Error refreshing drives: {ex.Message}";
        }
    }

    /// <summary>Prompts the user to pick a custom destination directory path on the selected target drive.</summary>
    [RelayCommand]
    private async Task SelectCustomDestinationAsync()
    {
        if (SelectedDrive is null)
        {
            return;
        }

        string? selectedFolder = await _folderPickerService.PickFolderAsync($"{SelectedDrive.DriveLetter}\\");
        if (!string.IsNullOrEmpty(selectedFolder))
        {
            string rootDrive = Path.GetPathRoot(selectedFolder) ?? string.Empty;
            if (!rootDrive.Equals($"{SelectedDrive.DriveLetter}\\", StringComparison.OrdinalIgnoreCase))
            {
                StatusMessage = $"Selected destination must be on the target drive ({SelectedDrive.DriveLetter}).";
                return;
            }

            CustomDestinationPath = selectedFolder;
        }
    }

    /// <summary>Clears the custom destination directory path override.</summary>
    [RelayCommand]
    private void ClearCustomDestination()
    {
        CustomDestinationPath = null;
    }

    /// <summary>
    /// Validates the supplied <paramref name="entries"/> against the target drive, resolves conflicts,
    /// and enqueues them for background copying.
    /// </summary>
    public async Task EnqueueEntriesAsync(IReadOnlyList<GameEntry> entries, string? targetDriveLetter = null)
    {
        ArgumentNullException.ThrowIfNull(entries);

        RemovableDrive? targetDrive = !string.IsNullOrWhiteSpace(targetDriveLetter)
            ? AvailableDrives.FirstOrDefault(d => string.Equals(d.DriveLetter, targetDriveLetter, StringComparison.OrdinalIgnoreCase)) ?? SelectedDrive
            : SelectedDrive;

        if (targetDrive is null || entries.Count == 0)
        {
            return;
        }

        try
        {
            string destinationPath = CustomDestinationPath ?? $"{targetDrive.DriveLetter}\\";
            RemovableDrive driveForValidation = GetDriveForValidation(targetDrive);

            IReadOnlyList<ValidationResult> validation = await _driveCoordinatorService.ValidateTransferAsync(
                entries,
                driveForValidation,
                destinationPath);

            ValidationMessages.UpdateFrom(validation);

            if (validation.Any(v => v.Severity == ValidationSeverity.Error))
            {
                StatusMessage = "Cannot queue transfer: validation failed. See warnings.";
                return;
            }

            List<TransferItem> itemsToQueue = await BuildItemsToQueueAsync(entries, destinationPath);

            if (itemsToQueue.Count > 0)
            {
                _ = _transferQueueService.Enqueue(itemsToQueue, targetDrive, destinationPath);
                StatusMessage = $"Queued {itemsToQueue.Count} item(s) for {targetDrive.DriveLetter} ({TransferQueue.Count} in queue)";
                IsTransferring = TransferQueue.Any(i => i.IsActive);
                ItemQueued?.Invoke(this, EventArgs.Empty);
                ClearCustomDestination();
            }
            else
            {
                StatusMessage = "No items to queue (all skipped).";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Queue error while enqueuing transfer entries.");
            StatusMessage = $"Queue error: {ex.Message}";
        }
    }

    /// <summary>Clears all completed, failed, or cancelled items from the transfer queue.</summary>
    [RelayCommand]
    private void ClearFinishedQueueItems() => _transferQueueService.ClearFinished();

    /// <summary>Cancels a queued or in-progress transfer queue item.</summary>
    [RelayCommand]
    private void CancelQueueItem(TransferQueueItem? item)
    {
        if (item is not null)
        {
            _transferQueueService.CancelItem(item);
        }
    }

    /// <summary>Re-enqueues a failed or cancelled transfer queue item for execution.</summary>
    [RelayCommand]
    private void RetryQueueItem(TransferQueueItem? item)
    {
        if (item is not null)
        {
            _transferQueueService.RetryItem(item);
            IsTransferring = TransferQueue.Any(i => i.IsActive);
        }
    }

    /// <summary>Cancels all active or queued transfers immediately.</summary>
    public void CancelAllActiveTransfers()
    {
        foreach (TransferQueueItem item in TransferQueue.Where(i => i.IsActive).ToList())
        {
            _transferQueueService.CancelItem(item);
        }

        IsTransferring = TransferQueue.Any(i => i.IsActive);
    }

    /// <summary>Opens the root directory of the selected drive in Windows File Explorer.</summary>
    [RelayCommand]
    private void OpenDriveInExplorer()
    {
        if (SelectedDrive is not null)
        {
            _processService.OpenInExplorer($"{SelectedDrive.DriveLetter}\\");
        }
    }

    /// <summary>Opens the Windows native format dialog for the selected drive letter.</summary>
    [RelayCommand]
    private void FormatDrive()
    {
        if (SelectedDrive is not null)
        {
            _processService.OpenFormatDialog(SelectedDrive.DriveLetter);
        }
    }

    /// <inheritdoc />
    public void Receive(EnqueueTransferRequestMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        List<GameEntry> entries = message.Items
            .Select(i => new GameEntry(
                i.Name,
                i.SourcePath,
                i.SizeBytes,
                null,
                DateTime.MinValue,
                i.HasFat32OversizedFiles))
            .ToList();

        _ = EnqueueEntriesAsync(entries, message.TargetDriveLetter);
    }

    private RemovableDrive GetDriveForValidation(RemovableDrive drive)
    {
        long reservedBytes = _transferQueueService.GetReservedBytes(drive.DriveLetter);
        return reservedBytes > 0
            ? drive with { FreeBytes = Math.Max(0, drive.FreeBytes - reservedBytes) }
            : drive;
    }

    private async Task<List<TransferItem>> BuildItemsToQueueAsync(IReadOnlyList<GameEntry> entries, string destinationPath)
    {
        List<TransferItem> itemsToQueue = [];
        bool applyToAll = false;
        CopyAction globalAction = CopyAction.Default;

        foreach (GameEntry game in entries)
        {
            string destItemPath = _fileSystemService.FileExists(game.FolderPath)
                ? Path.Combine(destinationPath, Path.GetFileName(game.FolderPath))
                : Path.Combine(destinationPath, game.Name);

            bool destExists = _fileSystemService.DirectoryExists(destItemPath) || _fileSystemService.FileExists(destItemPath);

            if (destExists)
            {
                if (applyToAll)
                {
                    if (globalAction != CopyAction.Skip)
                    {
                        itemsToQueue.Add(new TransferItem(game, globalAction));
                    }
                }
                else
                {
                    (long srcSize, int srcCount) = await _fileTransferService.GetFolderStatsAsync(game.FolderPath);
                    (long destSize, int destCount) = await _fileTransferService.GetFolderStatsAsync(destItemPath);

                    (CopyAction action, bool applyAll) = await _conflictPromptService.PromptConflictAsync(
                        game.Name,
                        srcSize,
                        srcCount,
                        destSize,
                        destCount);

                    if (applyAll)
                    {
                        applyToAll = true;
                        globalAction = action;
                    }

                    if (action != CopyAction.Skip)
                    {
                        itemsToQueue.Add(new TransferItem(game, action));
                    }
                }
            }
            else
            {
                itemsToQueue.Add(new TransferItem(game, CopyAction.Default));
            }
        }

        return itemsToQueue;
    }

    private void OnTransferQueueCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasQueueItems));
        OnPropertyChanged(nameof(IsQueueEmpty));
        OnPropertyChanged(nameof(QueueCount));
        IsTransferring = TransferQueue.Any(i => i.IsActive);
    }

    private void OnDrivesChanged(object? sender, EventArgs e)
    {
        if (IsDisposed)
        {
            return;
        }

        if (!_dispatcherService.HasThreadAccess)
        {
            _ = _dispatcherService.TryEnqueue(() =>
            {
                if (!IsDisposed)
                {
                    _ = RefreshDrivesAsync();
                }
            });
            return;
        }

        _ = RefreshDrivesAsync();
    }

    private void OnQueueItemCompleted(object? sender, TransferQueueItem completedItem)
    {
        if (IsDisposed)
        {
            return;
        }

        IsTransferring = TransferQueue.Any(i => i.IsActive);

        if (completedItem.Status == TransferQueueItemStatus.Completed)
        {
            StatusMessage = $"Completed: {completedItem.ItemsSummary} \u2192 {completedItem.TargetDrive.DriveLetter}";
            _ = RefreshDrivesAsync();
        }
        else
        {
            StatusMessage = $"Transfer failed: {completedItem.ItemsSummary} - {completedItem.StatusMessage}";
        }
    }

    private void OnQueueBatchCompleted(object? sender, (string Title, string Message, bool IsSuccess) args)
    {
        if (!IsDisposed)
        {
            BatchCompleted?.Invoke(this, args);
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
        _driveCoordinatorService.DrivesChanged -= OnDrivesChanged;
        _transferQueueService.ItemCompleted -= OnQueueItemCompleted;
        _transferQueueService.BatchCompleted -= OnQueueBatchCompleted;
        TransferQueue.CollectionChanged -= OnTransferQueueCollectionChanged;
        _driveCoordinatorService.StopWatching();
    }
}
