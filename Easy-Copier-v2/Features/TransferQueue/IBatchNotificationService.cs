using EasyCopier.Features.TransferQueue.Models;

namespace EasyCopier.Features.TransferQueue;

/// <summary>
/// Defines operations for evaluating completed drive transfer batches, executing audio alerts, and presenting desktop notifications.
/// </summary>
public interface IBatchNotificationService
{
    /// <summary>
    /// Evaluates completed batch status for a target drive letter and triggers configured audio alerts and desktop toast notifications.
    /// </summary>
    void EvaluateAndNotifyBatchCompletion(string driveLetter, IEnumerable<TransferQueueItem> queueItems);

    /// <summary>
    /// Event raised when an entire drive transfer batch finishes processing.
    /// </summary>
    event EventHandler<(string Title, string Message, bool IsSuccess)>? BatchCompleted;
}
