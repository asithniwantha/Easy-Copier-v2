using System.Collections.ObjectModel;
using EasyCopier.Features.TransferQueue.Models;

namespace EasyCopier.Features.TransferQueue;

/// <summary>
/// Defines operations for managing the background transfer queue, enqueuing transfer items, and listening for completion events.
/// </summary>
public interface ITransferQueueService
{
    ObservableCollection<TransferQueueItem> QueueItems { get; }

    TransferQueueItem Enqueue(IReadOnlyList<TransferItem> items, RemovableDrive targetDrive, string destinationPath);

    long GetReservedBytes(string driveLetter);

    void ClearFinished();

    void CancelItem(TransferQueueItem item);

    void RetryItem(TransferQueueItem item);

    event EventHandler<TransferQueueItem>? ItemCompleted;

    event EventHandler<(string Title, string Message, bool IsSuccess)>? BatchCompleted;
}

