using EasyCopier.Features.Shell.Contracts;
using EasyCopier.Shared.Messages;

namespace EasyCopier.Features.TransferQueue.Models;

/// <summary>
/// Specifies the conflict resolution action for file transfer operations.
/// </summary>
public enum CopyAction
{
    Default,
    Replace,
    Merge,
    Skip
}

/// <summary>
/// Represents an individual item in a transfer batch along with its assigned copy action.
/// </summary>
public record TransferItem(
    GameEntry Game,
    CopyAction Action = CopyAction.Default);

/// <summary>
/// Represents the progress of an active file copy or transfer operation.
/// <see cref="BytesPerSecond"/> is the current (recent-window) rate, or <see langword="null"/> when no rate applies (e.g. verification).
/// </summary>
public record TransferProgress(double Percentage, string SpeedText, string RemainingTimeText, double? BytesPerSecond = null);

/// <summary>
/// Represents a request to transfer a batch of items to a designated removable drive.
/// </summary>
public record TransferRequest(
    IReadOnlyList<TransferItem> Items,
    RemovableDrive TargetDrive,
    string DestinationPath);

/// <summary>
/// Represents the outcome of a post-copy data verification check.
/// </summary>
public record VerificationResult(
    bool IsSuccess,
    string? ErrorMessage = null,
    IReadOnlyList<string>? MismatchedFiles = null);

/// <summary>
/// Represents the final outcome of a completed transfer batch.
/// </summary>
public record TransferOutcome(
    bool Success,
    string Message,
    int FilesTransferred,
    long BytesTransferred,
    DateTime CompletedAt,
    TimeSpan Duration = default,
    IReadOnlyList<TransferCompletedItemDescriptor>? ItemResults = null);

