using CommunityToolkit.Mvvm.ComponentModel;

namespace EasyCopier.Features.TransferQueue.Models;

/// <summary>
/// Specifies the execution status of an item or batch in the transfer queue.
/// </summary>
public enum TransferQueueItemStatus
{
    Queued,
    InProgress,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Represents a queued transfer job batch in the queue UI with progress monitoring and status tracking.
/// </summary>
public sealed partial class TransferQueueItem(
    IReadOnlyList<TransferItem> items,
    RemovableDrive targetDrive,
    string destinationPath,
    int totalPrice) : ObservableObject, IDisposable
{
    private CancellationTokenSource? _cancellationTokenSource;

    /// <summary>Gets the unique identifier for this queue item job.</summary>
    public string Id { get; } = Guid.NewGuid().ToString();

    /// <summary>Gets the list of transfer items included in this batch.</summary>
    public IReadOnlyList<TransferItem> Items { get; } = items;

    /// <summary>Gets the target drive destination details.</summary>
    public RemovableDrive TargetDrive { get; } = targetDrive;

    /// <summary>Gets the destination directory path on the target volume.</summary>
    public string DestinationPath { get; } = destinationPath;

    /// <summary>Gets the timestamp when this batch was added to the transfer queue.</summary>
    public DateTime EnqueuedAt { get; } = DateTime.Now;

    /// <summary>Gets the total byte size of all items in this transfer batch.</summary>
    public long TotalBytes { get; } = items.Sum(i => i.Game.TotalBytes);

    /// <summary>Gets the total calculated price for this batch transfer.</summary>
    public int TotalPrice { get; } = totalPrice;

    /// <summary>Gets the cancellation token associated with this queue item transfer operation.</summary>
    public CancellationToken CancellationToken => (_cancellationTokenSource ??= new CancellationTokenSource()).Token;

    /// <summary>
    /// Gets or sets the current execution status of the queue item.
    /// </summary>
    [ObservableProperty]
    public partial TransferQueueItemStatus Status { get; set; } = TransferQueueItemStatus.Queued;

    /// <summary>
    /// Gets or sets a user-visible message describing the current queue status or error reason.
    /// </summary>
    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "Waiting in queue...";

    /// <summary>
    /// Gets or sets the overall completion percentage of the active transfer (0 to 100).
    /// </summary>
    [ObservableProperty]
    public partial double ProgressPercentage { get; set; }

    /// <summary>
    /// Gets or sets the formatted real-time transfer speed string (e.g. "25 MB/s").
    /// </summary>
    [ObservableProperty]
    public partial string SpeedText { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the formatted estimated remaining transfer time string.
    /// </summary>
    [ObservableProperty]
    public partial string RemainingTimeText { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the transfer completed or failed.
    /// </summary>
    [ObservableProperty]
    public partial DateTime? CompletedAt { get; set; }

    /// <summary>Gets a summary text describing the items in the batch.</summary>
    public string ItemsSummary => Items.Count == 1 ? Items[0].Game.Name : $"{Items.Count} items";

    /// <summary>Gets the Segoe MDL2 Assets icon glyph matching the current transfer status.</summary>
    public string StatusGlyph => Status switch
    {
        TransferQueueItemStatus.Queued => "\uE823",
        TransferQueueItemStatus.InProgress => "\uE895",
        TransferQueueItemStatus.Completed => "\uE73E",
        TransferQueueItemStatus.Failed => "\uE783",
        TransferQueueItemStatus.Cancelled => "\uE711",
        _ => "\uE9CE"
    };

    /// <summary>Gets a short human-readable label for the current status.</summary>
    public string StatusLabel => Status switch
    {
        TransferQueueItemStatus.Queued => "Queued",
        TransferQueueItemStatus.InProgress => "Copying",
        TransferQueueItemStatus.Completed => "Completed",
        TransferQueueItemStatus.Failed => "Failed",
        TransferQueueItemStatus.Cancelled => "Cancelled",
        _ => string.Empty
    };

    /// <summary>Gets a value indicating whether this item is currently queued or in progress.</summary>
    public bool IsActive => Status is TransferQueueItemStatus.Queued or TransferQueueItemStatus.InProgress;

    /// <summary>Gets a value indicating whether this item can be retried (only when failed or cancelled).</summary>
    public bool CanRetry => Status is TransferQueueItemStatus.Failed or TransferQueueItemStatus.Cancelled;

    /// <summary>Cancels this queue item transfer operation if currently queued or in-progress.</summary>
    public void Cancel()
    {
        if (Status is TransferQueueItemStatus.Queued or TransferQueueItemStatus.InProgress)
        {
            _cancellationTokenSource?.Cancel();
            Status = TransferQueueItemStatus.Cancelled;
            StatusMessage = "Transfer cancelled by user";
            CompletedAt = DateTime.Now;
        }
    }

    /// <summary>Resets the queue item to a fresh queued state with a renewed cancellation token for retry execution.</summary>
    public void ResetForRetry()
    {
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = new CancellationTokenSource();
        Status = TransferQueueItemStatus.Queued;
        StatusMessage = "Waiting in queue...";
        ProgressPercentage = 0;
        SpeedText = string.Empty;
        RemainingTimeText = string.Empty;
        CompletedAt = null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
    }

    partial void OnStatusChanged(TransferQueueItemStatus value)
    {
        OnPropertyChanged(nameof(StatusGlyph));
        OnPropertyChanged(nameof(StatusLabel));
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(CanRetry));
    }
}

