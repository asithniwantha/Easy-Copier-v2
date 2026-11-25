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
/// Immutable per-item outcome descriptor published when a transfer batch finishes so History logs each item decoupled from TransferQueue.
/// </summary>
public readonly record struct TransferCompletedItemDescriptor(
    string ItemName,
    string SourcePath,
    string DestinationPath,
    long BytesTransferred,
    bool IsSuccess,
    int Amount,
    int BatchAmount,
    string ErrorLog,
    string SubFilesJson,
    TimeSpan Duration,
    string StatusText = "Completed",
    DateTime Timestamp = default);

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
    TimeSpan Duration,
    string TargetDriveLabel = "",
    IReadOnlyList<TransferCompletedItemDescriptor>? ItemResults = null);
