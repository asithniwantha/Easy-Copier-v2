using System.Globalization;
using EasyCopier.Shared.UI.Helpers;

namespace EasyCopier.Features.History.Models;

/// <summary>
/// Represents the tri-fold completion state of a transfer history entry.
/// </summary>
public enum TransferStatus
{
    /// <summary>The item was copied and verified successfully.</summary>
    Completed,

    /// <summary>The transfer encountered an error or verification failure.</summary>
    Failed,

    /// <summary>The item was skipped (for example due to a destination conflict choice).</summary>
    Skipped
}

/// <summary>
/// Represents a persistent database history entry recording a completed transfer.
/// </summary>
public sealed record CopyHistoryRecord(
    int Id,
    DateTime Timestamp,
    string GameName,
    string TargetDriveLetter,
    string TargetDriveLabel,
    long BytesTransferred,
    bool IsSuccess,
    int Amount,
    string SourcePath = "",
    string DestinationPath = "",
    string ErrorLog = "",
    string SubFilesJson = "",
    long DurationMs = 0,
    string StatusText = "")
{
    /// <summary>Gets or sets the total batch price amount for grouped transfer operations.</summary>
    public int BatchAmount { get; set; }

    /// <summary>Gets the formatted human-readable byte size string.</summary>
    public string FormattedSize => FormattingHelpers.FormatBytes(BytesTransferred);

    /// <summary>Gets the formatted timestamp string for single-line table rows.</summary>
    public string FormattedTimestamp => Timestamp.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Gets the transfer duration derived from <see cref="DurationMs"/>.</summary>
    public TimeSpan Duration => TimeSpan.FromMilliseconds(Math.Max(0, DurationMs));

    /// <summary>Gets the formatted transfer duration string (e.g., <c>01:24</c> or <c>1:02:15</c>).</summary>
    public string FormattedDuration
    {
        get
        {
            if (DurationMs <= 0)
            {
                return "\u2014";
            }

            TimeSpan duration = Duration;
            return duration.TotalHours >= 1.0
                ? string.Format(CultureInfo.InvariantCulture, "{0}:{1:mm\\:ss}", (int)duration.TotalHours, duration)
                : duration.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Gets the normalized tri-fold status for this transfer record.</summary>
    public TransferStatus Status => StatusText.Trim().ToUpperInvariant() switch
    {
        "SKIPPED" => TransferStatus.Skipped,
        "FAILED" => TransferStatus.Failed,
        "COMPLETED" => TransferStatus.Completed,
        _ => IsSuccess ? TransferStatus.Completed : TransferStatus.Failed
    };

    /// <summary>Gets the tri-fold status label.</summary>
    public string DisplayStatusText => Status.ToString();

    /// <summary>Gets a value indicating whether the transfer completed successfully.</summary>
    public bool IsCompletedStatus => Status == TransferStatus.Completed;

    /// <summary>Gets a value indicating whether the transfer failed.</summary>
    public bool IsFailedStatus => Status == TransferStatus.Failed;

    /// <summary>Gets a value indicating whether the transfer was skipped.</summary>
    public bool IsSkippedStatus => Status == TransferStatus.Skipped;
}

/// <summary>
/// Represents aggregated summary statistics calculated for a subset of copy history records.
/// </summary>
public sealed record HistoryStats(
    int TotalItems,
    int SuccessfulItems,
    long TotalBytes,
    int TotalAmount,
    string SuccessRate);

/// <summary>
/// Represents aggregated totals for a single reporting period (day or week).
/// </summary>
public sealed record PeriodTotal(
    string Label,
    int TotalItems,
    int FailedItems,
    long TotalBytes,
    double BarRatio)
{
    /// <summary>Gets the failure rate percentage (0 to 100) for the period.</summary>
    public double FailureRate => TotalItems == 0 ? 0 : (double)FailedItems / TotalItems * 100;

    /// <summary>Gets the bar height in device-independent pixels for chart rendering.</summary>
    public double BarHeight => Math.Max(2, BarRatio * 120);
}

/// <summary>
/// Represents an item ranked by the number of times it was copied.
/// </summary>
public sealed record TopCopiedItem(string Name, int CopyCount, long TotalBytes);

/// <summary>
/// Represents dashboard reporting data for a time range.
/// </summary>
public sealed record DashboardStats(
    IReadOnlyList<PeriodTotal> DailyTotals,
    IReadOnlyList<PeriodTotal> WeeklyTotals,
    IReadOnlyList<TopCopiedItem> TopItems,
    double OverallFailureRate);
