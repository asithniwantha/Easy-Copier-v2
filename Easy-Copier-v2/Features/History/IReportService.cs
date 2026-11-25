using EasyCopier.Features.History.Models;

namespace EasyCopier.Features.History;

/// <summary>
/// Defines operations for exporting operation history and reporting data to external formats such as CSV.
/// </summary>
public interface IReportService
{
    /// <summary>
    /// Asynchronously exports copy history records to a CSV file at the specified file path.
    /// </summary>
    Task<bool> ExportHistoryToCsvAsync(string filePath, IEnumerable<CopyHistoryRecord> records);
}
