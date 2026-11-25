using System.Globalization;
using EasyCopier.Features.SmartAdder.Models;
using EasyCopier.Shared.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace EasyCopier.Features.SmartAdder;

/// <summary>
/// Service for persisting Smart Adder calculator history to the local SQLite database (<c>SmartAdderHistory</c> table).
/// </summary>
public sealed class SmartAdderHistoryService : ISmartAdderHistoryService
{
    private readonly ISqliteConnectionFactory _connectionFactory;
    private readonly ILogger<SmartAdderHistoryService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmartAdderHistoryService"/> class.
    /// </summary>
    /// <param name="connectionFactory">Factory for creating SQLite connections.</param>
    /// <param name="logger">Logger instance for operational diagnostic output.</param>
    public SmartAdderHistoryService(
        ISqliteConnectionFactory connectionFactory,
        ILogger<SmartAdderHistoryService> logger)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(logger);

        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        try
        {
            using SqliteConnection connection = await _connectionFactory.CreateOpenConnectionAsync();

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS SmartAdderHistory (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Timestamp TEXT NOT NULL,
                    EntriesJson TEXT NOT NULL,
                    Total REAL NOT NULL
                )
                """;

            _ = await command.ExecuteNonQueryAsync();
            _logger.LogInformation("SmartAdderHistory table initialized at {Path}", _connectionFactory.DatabasePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize SmartAdderHistory table at {Path}", _connectionFactory.DatabasePath);
        }
    }

    /// <inheritdoc />
    public async Task AddRecordAsync(SmartAdderHistoryRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        try
        {
            using SqliteConnection connection = await _connectionFactory.CreateOpenConnectionAsync();

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO SmartAdderHistory (Timestamp, EntriesJson, Total)
                VALUES ($timestamp, $entriesJson, $total)
                """;

            _ = command.Parameters.AddWithValue("$timestamp", record.Timestamp.ToString("O"));
            _ = command.Parameters.AddWithValue("$entriesJson", record.EntriesJson);
            _ = command.Parameters.AddWithValue("$total", record.TotalSum);

            _ = await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add SmartAdder history record with total {Total}", record.Total);
        }
    }

    /// <inheritdoc />
    public async Task<List<SmartAdderHistoryRecord>> GetRecentRecordsAsync(int count)
    {
        List<SmartAdderHistoryRecord> records = [];
        if (count <= 0)
        {
            return records;
        }

        try
        {
            using SqliteConnection connection = await _connectionFactory.CreateOpenConnectionAsync();

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT Id, Timestamp, EntriesJson, Total
                FROM SmartAdderHistory
                ORDER BY Id DESC
                LIMIT $count
                """;
            _ = command.Parameters.AddWithValue("$count", count);

            using SqliteDataReader reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string timestampStr = reader.GetString(1);
                if (!DateTime.TryParse(timestampStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime timestamp))
                {
                    _logger.LogWarning("Failed to parse Timestamp '{TimestampStr}' in SmartAdderHistory", timestampStr);
                    continue;
                }

                records.Add(new SmartAdderHistoryRecord
                {
                    Id = reader.GetInt32(0),
                    Timestamp = timestamp,
                    EntriesJson = await reader.IsDBNullAsync(2) ? "[]" : reader.GetString(2),
                    Total = await reader.IsDBNullAsync(3) ? 0 : reader.GetDouble(3)
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get recent SmartAdder history records");
        }

        return records;
    }
}
