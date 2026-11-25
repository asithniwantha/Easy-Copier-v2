using EasyCopier.Features.History.Models;

namespace EasyCopier.Features.History;

/// <summary>
/// Defines operations for history record clustering and statistics summary calculations.
/// </summary>
public interface IHistoryAnalysisService
{
    /// <summary>
    /// Clusters records targeting the same destination drive within a 15-minute time window and populates batch totals.
    /// </summary>
    void ApplyBatchClustering(IEnumerable<CopyHistoryRecord> records);

    /// <summary>
    /// Calculates summary statistics (totals, bytes, amount, and success rate) for a set of history records.
    /// </summary>
    HistoryStats CalculateStats(IEnumerable<CopyHistoryRecord> records);

    /// <summary>
    /// Builds dashboard reporting data (daily/weekly totals, most-copied items, failure rates) for the trailing range.
    /// </summary>
    DashboardStats BuildDashboard(IEnumerable<CopyHistoryRecord> records, int rangeDays, int topCount = 5);
}

