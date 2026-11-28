using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CommunityToolkit.Mvvm.Messaging;
using EasyCopier.Features.History.Models;
using EasyCopier.Shared.Messages;
using EasyCopier.Shared.Models;
using EasyCopier.Shared.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace EasyCopier.Features.History;

/// <summary>
/// Provides functionality for persisting and querying file copy operation history stored in a local SQLite database file,
/// automatically recording completed transfers arriving via <see cref="TransferBatchCompletedMessage"/>.
/// </summary>
public sealed class CopyHistoryService : ICopyHistoryService, IRecipient<TransferBatchCompletedMessage>, IDisposable
{
    private const string SelectColumns =
        "Id, Timestamp, GameName, TargetDriveLetter, TargetDriveLabel, BytesTransferred, IsSuccess, Amount, SourcePath, DestinationPath, ErrorLog, SubFilesJson, DurationMs, StatusText";

    private readonly ISqliteConnectionFactory _connectionFactory;
    private readonly IMessenger _messenger;
    private readonly ILogger<CopyHistoryService> _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private int _isDisposed;

    /// <inheritdoc />
    public event EventHandler? HistoryChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="CopyHistoryService"/> class.
    /// </summary>
    public CopyHistoryService(
        ISqliteConnectionFactory connectionFactory,
        IMessenger messenger,
        ILogger<CopyHistoryService> logger)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _messenger.Register(this);
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        try
        {
            await using SqliteConnection connection = await _connectionFactory.CreateOpenConnectionAsync().ConfigureAwait(false);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS CopyHistory (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Timestamp TEXT NOT NULL,
                    GameName TEXT NOT NULL,
                    TargetDriveLetter TEXT NOT NULL,
                    TargetDriveLabel TEXT NOT NULL,
                    BytesTransferred INTEGER NOT NULL,
                    IsSuccess INTEGER NOT NULL,
                    Amount INTEGER NOT NULL DEFAULT 0,
                    SourcePath TEXT NOT NULL DEFAULT '',
                    DestinationPath TEXT NOT NULL DEFAULT '',
                    ErrorLog TEXT NOT NULL DEFAULT '',
                    SubFilesJson TEXT NOT NULL DEFAULT '',
                    DurationMs INTEGER NOT NULL DEFAULT 0,
                    StatusText TEXT NOT NULL DEFAULT ''
                )";

            _ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);

            command.CommandText = "PRAGMA table_info(CopyHistory)";
            HashSet<string> existingColumns = new(StringComparer.OrdinalIgnoreCase);
            await using (SqliteDataReader reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
            {
                while (await reader.ReadAsync().ConfigureAwait(false))
                {
                    _ = existingColumns.Add(reader.GetString(1));
                }
            }

            if (!existingColumns.Contains("Amount"))
            {
                command.CommandText = "ALTER TABLE CopyHistory ADD COLUMN Amount INTEGER NOT NULL DEFAULT 0";
                _ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            if (!existingColumns.Contains("SourcePath"))
            {
                command.CommandText = "ALTER TABLE CopyHistory ADD COLUMN SourcePath TEXT NOT NULL DEFAULT ''";
                _ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                command.CommandText = "ALTER TABLE CopyHistory ADD COLUMN DestinationPath TEXT NOT NULL DEFAULT ''";
                _ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                command.CommandText = "ALTER TABLE CopyHistory ADD COLUMN ErrorLog TEXT NOT NULL DEFAULT ''";
                _ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                command.CommandText = "ALTER TABLE CopyHistory ADD COLUMN SubFilesJson TEXT NOT NULL DEFAULT ''";
                _ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            if (!existingColumns.Contains("DurationMs"))
            {
                command.CommandText = "ALTER TABLE CopyHistory ADD COLUMN DurationMs INTEGER NOT NULL DEFAULT 0";
                _ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                _logger.LogInformation("Added 'DurationMs' column to CopyHistory table via schema migration.");
            }

            if (!existingColumns.Contains("StatusText"))
            {
                command.CommandText = "ALTER TABLE CopyHistory ADD COLUMN StatusText TEXT NOT NULL DEFAULT ''";
                _ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                _logger.LogInformation("Added 'StatusText' column to CopyHistory table via schema migration.");
            }

            command.CommandText = "CREATE INDEX IF NOT EXISTS idx_copyhistory_timestamp ON CopyHistory (Timestamp DESC);";
            _ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);

            _logger.LogInformation("CopyHistory DB initialized at {Path}", _connectionFactory.DatabasePath);
        }
        catch (Exception ex) when (ex is SqliteException or IOException or InvalidOperationException or FormatException)
        {
            _logger.LogError(ex, "Failed to initialize CopyHistory DB at {Path}", _connectionFactory.DatabasePath);
        }
    }

    /// <inheritdoc />
    public void Receive(TransferBatchCompletedMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (Volatile.Read(ref _isDisposed) != 0)
        {
            return;
        }

        _ = PersistBatchAsync(message);
    }

    /// <summary>
    /// Persists all item outcomes carried by a <see cref="TransferBatchCompletedMessage"/>.
    /// </summary>
    public async Task PersistBatchAsync(TransferBatchCompletedMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message.ItemResults is { Count: > 0 })
        {
            foreach (TransferCompletedItemDescriptor item in message.ItemResults)
            {
                long durationMs = (long)Math.Max(0, (item.Duration == TimeSpan.Zero ? message.Duration : item.Duration).TotalMilliseconds);
                string statusText = !string.IsNullOrWhiteSpace(item.StatusText)
                    ? item.StatusText
                    : item.IsSuccess ? "Completed" : "Failed";

                CopyHistoryRecord record = new(
                    Id: 0,
                    Timestamp: item.Timestamp == default ? DateTime.Now : item.Timestamp,
                    GameName: item.ItemName,
                    TargetDriveLetter: message.TargetDriveLetter,
                    TargetDriveLabel: message.TargetDriveLabel,
                    BytesTransferred: item.BytesTransferred,
                    IsSuccess: item.IsSuccess,
                    Amount: item.Amount,
                    SourcePath: item.SourcePath,
                    DestinationPath: item.DestinationPath,
                    ErrorLog: item.ErrorLog,
                    SubFilesJson: item.SubFilesJson,
                    DurationMs: durationMs,
                    StatusText: statusText)
                {
                    BatchAmount = item.BatchAmount
                };

                await AddRecordAsync(record).ConfigureAwait(false);
            }
        }
        else
        {
            int totalItems = message.SuccessCount + message.FailureCount;
            bool isSuccess = message.FailureCount == 0;
            CopyHistoryRecord summary = new(
                Id: 0,
                Timestamp: DateTime.Now,
                GameName: totalItems > 0 ? $"Transfer Batch ({totalItems} item(s))" : "Transfer Batch",
                TargetDriveLetter: message.TargetDriveLetter,
                TargetDriveLabel: message.TargetDriveLabel,
                BytesTransferred: message.TotalBytesTransferred,
                IsSuccess: isSuccess,
                Amount: 0,
                DurationMs: (long)Math.Max(0, message.Duration.TotalMilliseconds),
                StatusText: isSuccess ? "Completed" : "Failed");

            await AddRecordAsync(summary).ConfigureAwait(false);
        }

        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public async Task AddRecordAsync(CopyHistoryRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        string normalizedStatus = !string.IsNullOrWhiteSpace(record.StatusText)
            ? record.StatusText
            : record.IsSuccess ? "Completed" : "Failed";

        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await using SqliteConnection connection = await _connectionFactory.CreateOpenConnectionAsync().ConfigureAwait(false);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO CopyHistory (Timestamp, GameName, TargetDriveLetter, TargetDriveLabel, BytesTransferred, IsSuccess, Amount, SourcePath, DestinationPath, ErrorLog, SubFilesJson, DurationMs, StatusText)
                VALUES ($timestamp, $gameName, $targetDriveLetter, $targetDriveLabel, $bytesTransferred, $isSuccess, $amount, $sourcePath, $destinationPath, $errorLog, $subFilesJson, $durationMs, $statusText)";

            _ = command.Parameters.AddWithValue("$timestamp", record.Timestamp.ToString("O", CultureInfo.InvariantCulture));
            _ = command.Parameters.AddWithValue("$gameName", record.GameName);
            _ = command.Parameters.AddWithValue("$targetDriveLetter", record.TargetDriveLetter);
            _ = command.Parameters.AddWithValue("$targetDriveLabel", record.TargetDriveLabel);
            _ = command.Parameters.AddWithValue("$bytesTransferred", record.BytesTransferred);
            _ = command.Parameters.AddWithValue("$isSuccess", record.IsSuccess ? 1 : 0);
            _ = command.Parameters.AddWithValue("$amount", record.Amount);
            _ = command.Parameters.AddWithValue("$sourcePath", record.SourcePath);
            _ = command.Parameters.AddWithValue("$destinationPath", record.DestinationPath);
            _ = command.Parameters.AddWithValue("$errorLog", record.ErrorLog);
            _ = command.Parameters.AddWithValue("$subFilesJson", record.SubFilesJson);
            _ = command.Parameters.AddWithValue("$durationMs", Math.Max(0, record.DurationMs));
            _ = command.Parameters.AddWithValue("$statusText", normalizedStatus);

            _ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is SqliteException or IOException or InvalidOperationException or FormatException)
        {
            _logger.LogError(ex, "Failed to add copy history record for {GameName}", record.GameName);
        }
        finally
        {
            _ = _writeLock.Release();
        }
    }

    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Internal helper receiving compile-time constant where-clause fragments with parameterized values.")]
    private async Task<List<CopyHistoryRecord>> QueryRecordsInternalAsync(string? whereClause = null, Action<SqliteCommand>? parameterBinder = null)
    {
        List<CopyHistoryRecord> records = [];
        try
        {
            await using SqliteConnection connection = await _connectionFactory.CreateOpenConnectionAsync().ConfigureAwait(false);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = whereClause != null
                ? $"SELECT {SelectColumns} FROM CopyHistory WHERE {whereClause} ORDER BY Timestamp DESC"
                : $"SELECT {SelectColumns} FROM CopyHistory ORDER BY Timestamp DESC";

            parameterBinder?.Invoke(command);

            await using SqliteDataReader reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                CopyHistoryRecord? rec = ParseRecord(reader);
                if (rec != null)
                {
                    records.Add(rec);
                }
            }
        }
        catch (Exception ex) when (ex is SqliteException or IOException or InvalidOperationException or FormatException)
        {
            _logger.LogError(ex, "Failed to execute query on copy history records");
        }

        return records;
    }

    /// <inheritdoc />
    public async Task<List<CopyHistoryRecord>> GetRecordsByWeekAsync(DateTime startOfWeek, DateTime endOfWeek)
    {
        DateTime start = startOfWeek.Date;
        DateTime end = endOfWeek.Date.AddDays(1);

        return await QueryRecordsInternalAsync(
            "Timestamp >= $start AND Timestamp < $end",
            cmd =>
            {
                _ = cmd.Parameters.AddWithValue("$start", start.ToString("O", CultureInfo.InvariantCulture));
                _ = cmd.Parameters.AddWithValue("$end", end.ToString("O", CultureInfo.InvariantCulture));
            }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<List<DateTime>> GetAvailableWeeksAsync()
    {
        List<CopyHistoryRecord> allRecords = await QueryRecordsInternalAsync().ConfigureAwait(false);
        HashSet<DateTime> startOfWeeks = [];

        foreach (CopyHistoryRecord record in allRecords)
        {
            DateTime date = record.Timestamp.Date;
            int diff = (7 + (date.DayOfWeek - DayOfWeek.Sunday)) % 7;
            DateTime startOfWeek = date.AddDays(-diff).Date;
            _ = startOfWeeks.Add(startOfWeek);
        }

        List<DateTime> sortedWeeks = [.. startOfWeeks];
        sortedWeeks.Sort((a, b) => b.CompareTo(a));
        return sortedWeeks;
    }

    /// <inheritdoc />
    public async Task<List<CopyHistoryRecord>> GetRecordsByMonthAsync(int year, int month)
    {
        DateTime start = new(year, month, 1);
        DateTime end = start.AddMonths(1);

        return await QueryRecordsInternalAsync(
            "Timestamp >= $start AND Timestamp < $end",
            cmd =>
            {
                _ = cmd.Parameters.AddWithValue("$start", start.ToString("O", CultureInfo.InvariantCulture));
                _ = cmd.Parameters.AddWithValue("$end", end.ToString("O", CultureInfo.InvariantCulture));
            }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<List<(int Year, int Month)>> GetAvailableMonthsAsync()
    {
        List<CopyHistoryRecord> allRecords = await QueryRecordsInternalAsync().ConfigureAwait(false);
        HashSet<(int Year, int Month)> months = [];

        foreach (CopyHistoryRecord record in allRecords)
        {
            _ = months.Add((record.Timestamp.Year, record.Timestamp.Month));
        }

        List<(int Year, int Month)> sortedMonths = [.. months];
        sortedMonths.Sort((a, b) => b.CompareTo(a));
        return sortedMonths;
    }

    /// <inheritdoc />
    public async Task<(int TotalItems, int SuccessfulItems, long TotalBytes, int TotalAmount)> GetStatsAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            await using SqliteConnection connection = await _connectionFactory.CreateOpenConnectionAsync().ConfigureAwait(false);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = @"
                SELECT 
                    COUNT(*), 
                    COALESCE(SUM(CASE WHEN IsSuccess = 1 THEN 1 ELSE 0 END), 0),
                    COALESCE(SUM(BytesTransferred), 0),
                    COALESCE(SUM(Amount), 0)
                FROM CopyHistory 
                WHERE Timestamp >= $start AND Timestamp < $end";

            _ = command.Parameters.AddWithValue("$start", startDate.ToString("O", CultureInfo.InvariantCulture));
            _ = command.Parameters.AddWithValue("$end", endDate.ToString("O", CultureInfo.InvariantCulture));

            await using SqliteDataReader reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            if (await reader.ReadAsync().ConfigureAwait(false))
            {
                return (reader.GetInt32(0), reader.GetInt32(1), reader.GetInt64(2), reader.GetInt32(3));
            }
        }
        catch (Exception ex) when (ex is SqliteException or IOException or InvalidOperationException or FormatException)
        {
            _logger.LogError(ex, "Failed to compute stats for date range {Start} - {End}", startDate, endDate);
        }

        return (0, 0, 0L, 0);
    }

    private static (string WhereClause, List<SqliteParameter> Parameters) BuildWhereClause(
        DateTime? startDate,
        DateTime? endDate,
        TransferStatus? statusFilter,
        string? searchPattern)
    {
        List<string> conditions = [];
        List<SqliteParameter> parameters = [];

        if (startDate.HasValue)
        {
            conditions.Add("Timestamp >= $startDate");
            parameters.Add(new SqliteParameter("$startDate", startDate.Value.ToString("O", CultureInfo.InvariantCulture)));
        }

        if (endDate.HasValue)
        {
            conditions.Add("Timestamp < $endDate");
            parameters.Add(new SqliteParameter("$endDate", endDate.Value.ToString("O", CultureInfo.InvariantCulture)));
        }

        if (statusFilter.HasValue)
        {
            switch (statusFilter.Value)
            {
                case TransferStatus.Completed:
                    conditions.Add("(LOWER(StatusText) = 'completed' OR (StatusText = '' AND IsSuccess = 1))");
                    break;
                case TransferStatus.Failed:
                    conditions.Add("(LOWER(StatusText) = 'failed' OR (StatusText = '' AND IsSuccess = 0))");
                    break;
                case TransferStatus.Skipped:
                    conditions.Add("LOWER(StatusText) = 'skipped'");
                    break;
            }
        }

        if (!string.IsNullOrWhiteSpace(searchPattern))
        {
            conditions.Add("(GameName LIKE $search OR TargetDriveLetter LIKE $search OR TargetDriveLabel LIKE $search)");
            parameters.Add(new SqliteParameter("$search", $"%{searchPattern.Trim()}%"));
        }

        string whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : string.Empty;
        return (whereClause, parameters);
    }

    private CopyHistoryRecord? ParseRecord(SqliteDataReader reader)
    {
        string timestampStr = reader.GetString(1);
        if (DateTime.TryParse(timestampStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime timestamp))
        {
            return new CopyHistoryRecord(
                reader.GetInt32(0),
                timestamp,
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetInt64(5),
                reader.GetInt32(6) == 1,
                reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                reader.IsDBNull(11) ? string.Empty : reader.GetString(11),
                reader.IsDBNull(12) ? 0L : reader.GetInt64(12),
                reader.IsDBNull(13) ? string.Empty : reader.GetString(13));
        }

        _logger.LogWarning("Failed to parse Timestamp '{TimestampStr}' in CopyHistory", timestampStr);
        return null;
    }

    /// <inheritdoc />
    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Parameterized query using internally constructed WHERE clauses with parameter objects.")]
    public async Task<PagedResult<CopyHistoryRecord>> GetPagedRecordsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        TransferStatus? statusFilter = null,
        string? searchPattern = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Max(1, pageSize);
        int offset = (page - 1) * pageSize;

        (string whereClause, List<SqliteParameter> parameters) = BuildWhereClause(startDate, endDate, statusFilter, searchPattern);

        try
        {
            await using SqliteConnection connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);

            int totalCount = 0;
            await using (SqliteCommand countCmd = connection.CreateCommand())
            {
                countCmd.CommandText = $"SELECT COUNT(*) FROM CopyHistory {whereClause}";
                foreach (SqliteParameter p in parameters)
                {
                    _ = countCmd.Parameters.AddWithValue(p.ParameterName, p.Value);
                }

                object? scalar = await countCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                if (scalar is long countLong)
                {
                    totalCount = (int)countLong;
                }
                else if (scalar is int countInt)
                {
                    totalCount = countInt;
                }
            }

            List<CopyHistoryRecord> records = [];
            await using (SqliteCommand queryCmd = connection.CreateCommand())
            {
                queryCmd.CommandText = $@"
                    SELECT {SelectColumns}
                    FROM CopyHistory
                    {whereClause}
                    ORDER BY Timestamp DESC
                    LIMIT $pageSize OFFSET $offset";

                foreach (SqliteParameter p in parameters)
                {
                    _ = queryCmd.Parameters.AddWithValue(p.ParameterName, p.Value);
                }

                _ = queryCmd.Parameters.AddWithValue("$pageSize", pageSize);
                _ = queryCmd.Parameters.AddWithValue("$offset", offset);

                await using SqliteDataReader reader = await queryCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    CopyHistoryRecord? rec = ParseRecord(reader);
                    if (rec != null)
                    {
                        records.Add(rec);
                    }
                }
            }

            return new PagedResult<CopyHistoryRecord>(records, totalCount, page, pageSize);
        }
        catch (Exception ex) when (ex is SqliteException or IOException or InvalidOperationException or FormatException)
        {
            _logger.LogError(ex, "Failed to fetch paginated copy history records (page {Page}, size {Size})", page, pageSize);
            return new PagedResult<CopyHistoryRecord>([], 0, page, pageSize);
        }
    }

    /// <inheritdoc />
    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Parameterized query using internally constructed WHERE clauses with parameter objects.")]
    public async Task<(int TotalItems, int SuccessfulItems, long TotalBytes, int TotalAmount)> GetFilteredStatsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        TransferStatus? statusFilter = null,
        string? searchPattern = null,
        CancellationToken cancellationToken = default)
    {
        (string whereClause, List<SqliteParameter> parameters) = BuildWhereClause(startDate, endDate, statusFilter, searchPattern);

        try
        {
            await using SqliteConnection connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $@"
                SELECT 
                    COUNT(*), 
                    COALESCE(SUM(CASE WHEN IsSuccess = 1 THEN 1 ELSE 0 END), 0),
                    COALESCE(SUM(CASE WHEN IsSuccess = 1 THEN BytesTransferred ELSE 0 END), 0),
                    COALESCE(SUM(CASE WHEN IsSuccess = 1 THEN Amount ELSE 0 END), 0)
                FROM CopyHistory
                {whereClause}";

            foreach (SqliteParameter p in parameters)
            {
                _ = command.Parameters.AddWithValue(p.ParameterName, p.Value);
            }

            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return (reader.GetInt32(0), reader.GetInt32(1), reader.GetInt64(2), reader.GetInt32(3));
            }
        }
        catch (Exception ex) when (ex is SqliteException or IOException or InvalidOperationException or FormatException)
        {
            _logger.LogError(ex, "Failed to compute filtered history stats");
        }

        return (0, 0, 0L, 0);
    }

    /// <inheritdoc />
    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Parameterized query using internally constructed WHERE clauses with parameter objects.")]
    public async Task<List<CopyHistoryRecord>> GetAllFilteredRecordsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        TransferStatus? statusFilter = null,
        string? searchPattern = null,
        CancellationToken cancellationToken = default)
    {
        (string whereClause, List<SqliteParameter> parameters) = BuildWhereClause(startDate, endDate, statusFilter, searchPattern);
        List<CopyHistoryRecord> records = [];

        try
        {
            await using SqliteConnection connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $@"
                SELECT {SelectColumns}
                FROM CopyHistory
                {whereClause}
                ORDER BY Timestamp DESC";

            foreach (SqliteParameter p in parameters)
            {
                _ = command.Parameters.AddWithValue(p.ParameterName, p.Value);
            }

            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                CopyHistoryRecord? rec = ParseRecord(reader);
                if (rec != null)
                {
                    records.Add(rec);
                }
            }
        }
        catch (Exception ex) when (ex is SqliteException or IOException or InvalidOperationException or FormatException)
        {
            _logger.LogError(ex, "Failed to fetch all filtered history records");
        }

        return records;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        _messenger.UnregisterAll(this);
        _writeLock.Dispose();
    }
}
