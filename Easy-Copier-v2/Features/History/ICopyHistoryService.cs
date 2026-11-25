using EasyCopier.Features.History.Models;
using EasyCopier.Shared.Models;

namespace EasyCopier.Features.History;

/// <summary>
/// Service interface for recording and querying copy operation history in a local SQLite database.
/// </summary>
public interface ICopyHistoryService
{
    /// <summary>
    /// Occurs when a transfer batch record has finished persisting to storage.
    /// </summary>
    event EventHandler? HistoryChanged;

    /// <summary>
    /// Initializes the underlying SQLite database schema and applies any pending migrations.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Inserts a new copy operation history record into the database.
    /// </summary>
    Task AddRecordAsync(CopyHistoryRecord record);

    /// <summary>
    /// Retrieves all copy history records that occurred within a specific week range.
    /// </summary>
    Task<List<CopyHistoryRecord>> GetRecordsByWeekAsync(DateTime startOfWeek, DateTime endOfWeek);

    /// <summary>
    /// Retrieves distinct start dates of weeks for which history records exist.
    /// </summary>
    Task<List<DateTime>> GetAvailableWeeksAsync();

    /// <summary>
    /// Retrieves all copy history records for a specified year and month.
    /// </summary>
    Task<List<CopyHistoryRecord>> GetRecordsByMonthAsync(int year, int month);

    /// <summary>
    /// Asynchronously retrieves distinct year and month pairs for which history records exist.
    /// </summary>
    Task<List<(int Year, int Month)>> GetAvailableMonthsAsync();

    /// <summary>
    /// Calculates aggregated statistics for history records within a specified date range.
    /// </summary>
    Task<(int TotalItems, int SuccessfulItems, long TotalBytes, int TotalAmount)> GetStatsAsync(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Retrieves a paginated slice of copy history records matching the specified filters, ordered by timestamp descending.
    /// </summary>
    Task<PagedResult<CopyHistoryRecord>> GetPagedRecordsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        bool? isSuccess = null,
        string? searchPattern = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates aggregated summary statistics for history records matching the specified filters directly in SQLite.
    /// </summary>
    Task<(int TotalItems, int SuccessfulItems, long TotalBytes, int TotalAmount)> GetFilteredStatsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        bool? isSuccess = null,
        string? searchPattern = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all copy history records matching the specified filters without pagination, used for CSV export.
    /// </summary>
    Task<List<CopyHistoryRecord>> GetAllFilteredRecordsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        bool? isSuccess = null,
        string? searchPattern = null,
        CancellationToken cancellationToken = default);
}
