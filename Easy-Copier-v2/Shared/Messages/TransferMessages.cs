namespace EasyCopier.Shared.Messages;

/// <summary>
/// Immutable item descriptor passed across slices when requesting file transfers.
/// </summary>
public readonly record struct TransferSourceItem(
    string Name,
    string SourcePath,
    long SizeBytes,
    decimal CalculatedPrice,
    bool HasFat32OversizedFiles);

/// <summary>
/// Cross-slice message requesting the TransferQueue slice to enqueue one or more items for copying.
/// </summary>
public sealed record EnqueueTransferRequestMessage(
    IReadOnlyList<TransferSourceItem> Items,
    string? TargetDriveLetter = null);

/// <summary>
/// Cross-slice message published when a transfer batch finishes execution.
/// </summary>
public sealed record TransferBatchCompletedMessage(
    Guid BatchId,
    string TargetDriveLetter,
    int SuccessCount,
    int FailureCount,
    long TotalBytesTransferred,
    TimeSpan Duration);
