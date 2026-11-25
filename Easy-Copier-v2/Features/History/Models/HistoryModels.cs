using EasyCopier.Shared.UI.Helpers;

namespace EasyCopier.Features.History.Models;

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
    string SubFilesJson = "")
{
    /// <summary>Gets or sets the total batch price amount for grouped transfer operations.</summary>
    public int BatchAmount { get; set; }

    /// <summary>Gets the formatted human-readable byte size string.</summary>
    public string FormattedSize => FormattingHelpers.FormatBytes(BytesTransferred);
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
