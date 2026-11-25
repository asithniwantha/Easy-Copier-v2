using EasyCopier.Features.SmartAdder;
using EasyCopier.Features.SmartAdder.Models;
using EasyCopier.Shared.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EasyCopier.Tests.Features.SmartAdder;

public sealed class SmartAdderHistoryServiceTests : IDisposable
{
    private readonly string _connectionString;
    private readonly SqliteConnection _anchorConnection;
    private readonly SmartAdderHistoryService _service;

    public SmartAdderHistoryServiceTests()
    {
        string dbName = $"SmartAdderTest_{Guid.NewGuid():N}";
        _connectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared";
        _anchorConnection = new SqliteConnection(_connectionString);
        _anchorConnection.Open();

        SqliteConnectionFactory factory = new(_connectionString);
        _service = new SmartAdderHistoryService(factory, NullLogger<SmartAdderHistoryService>.Instance);
    }

    public void Dispose()
    {
        _anchorConnection.Dispose();
    }

    [Fact]
    public async Task InitializeAddAndRetrieve_PersistsAndOrdersMostRecentFirst()
    {
        await _service.InitializeAsync();

        DateTime firstTime = new(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc);
        DateTime secondTime = new(2026, 5, 1, 10, 5, 0, DateTimeKind.Utc);

        await _service.AddRecordAsync(new SmartAdderHistoryRecord
        {
            Timestamp = firstTime,
            EntriesJson = "[10,20.5]",
            TotalSum = 30.5
        });

        await _service.AddRecordAsync(new SmartAdderHistoryRecord
        {
            Timestamp = secondTime,
            EntriesJson = "[100,-25]",
            TotalSum = 75
        });

        List<SmartAdderHistoryRecord> records = await _service.GetRecentRecordsAsync(10);

        Assert.Equal(2, records.Count);
        Assert.Equal(75, records[0].TotalSum);
        Assert.Equal("[100,-25]", records[0].EntriesJson);
        Assert.Equal(secondTime, records[0].Timestamp);
        Assert.Equal(30.5, records[1].TotalSum);
        Assert.Equal(firstTime, records[1].Timestamp);
    }

    [Fact]
    public async Task GetRecentRecordsAsync_NonPositiveCount_ReturnsEmpty()
    {
        await _service.InitializeAsync();
        await _service.AddRecordAsync(new SmartAdderHistoryRecord
        {
            Timestamp = DateTime.UtcNow,
            EntriesJson = "[5]",
            TotalSum = 5
        });

        Assert.Empty(await _service.GetRecentRecordsAsync(0));
        Assert.Empty(await _service.GetRecentRecordsAsync(-3));
    }

    [Fact]
    public async Task GetRecentRecordsAsync_SkipsCorruptTimestampsWithoutThrowing()
    {
        await _service.InitializeAsync();

        using (SqliteCommand insertBad = _anchorConnection.CreateCommand())
        {
            insertBad.CommandText = "INSERT INTO SmartAdderHistory (Timestamp, EntriesJson, Total) VALUES ('not-a-date', '[1]', 1)";
            _ = await insertBad.ExecuteNonQueryAsync();
        }

        await _service.AddRecordAsync(new SmartAdderHistoryRecord
        {
            Timestamp = new DateTime(2026, 5, 2, 12, 0, 0, DateTimeKind.Utc),
            EntriesJson = "[42]",
            TotalSum = 42
        });

        List<SmartAdderHistoryRecord> records = await _service.GetRecentRecordsAsync(10);

        Assert.Single(records);
        Assert.Equal(42, records[0].TotalSum);
    }
}
