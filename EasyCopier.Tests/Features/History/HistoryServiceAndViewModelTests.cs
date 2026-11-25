using CommunityToolkit.Mvvm.Messaging;
using EasyCopier.Features.History;
using EasyCopier.Features.History.Models;
using EasyCopier.Shared.Messages;
using EasyCopier.Shared.Models;
using EasyCopier.Shared.Persistence;
using EasyCopier.Shared.Platform;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace EasyCopier.Tests.Features.History;

public sealed class HistoryServiceAndViewModelTests : IAsyncLifetime, IDisposable
{
    private readonly string _dbName = $"HistoryTest_{Guid.NewGuid():N}";
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"EasyCopierHistoryTest_{Guid.NewGuid():N}");
    private SqliteConnection _anchorConnection = null!;
    private ISqliteConnectionFactory _connectionFactory = null!;

    public HistoryServiceAndViewModelTests()
    {
        _ = Directory.CreateDirectory(_tempRoot);
    }

    public async Task InitializeAsync()
    {
        string connStr = $"Data Source={_dbName};Mode=Memory;Cache=Shared";
        _connectionFactory = new SqliteConnectionFactory(connStr, ":memory:");
        _anchorConnection = _connectionFactory.CreateConnection();
        await _anchorConnection.OpenAsync();
    }

    public async Task DisposeAsync()
    {
        await _anchorConnection.DisposeAsync();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private static IDispatcherService CreateSyncDispatcher()
    {
        Mock<IDispatcherService> mock = new();
        mock.Setup(d => d.HasThreadAccess).Returns(true);
        mock.Setup(d => d.TryEnqueue(It.IsAny<Action>())).Returns((Action a) =>
        {
            a();
            return true;
        });
        mock.Setup(d => d.TryEnqueue(It.IsAny<Func<Task>>())).Returns((Func<Task> f) =>
        {
            _ = f();
            return true;
        });
        return mock.Object;
    }

    [Fact]
    public async Task CopyHistoryService_InitializesQueriesAndExportsCsv()
    {
        StrongReferenceMessenger messenger = new();
        using CopyHistoryService historyService = new(_connectionFactory, messenger, NullLogger<CopyHistoryService>.Instance);
        await historyService.InitializeAsync();

        DateTime today = DateTime.Now;
        await historyService.AddRecordAsync(new CopyHistoryRecord(0, today.AddMinutes(-10), "Elden Ring", "E:", "USB_1", 1024, true, 200, @"C:\Games\Elden", @"E:\Elden"));
        await historyService.AddRecordAsync(new CopyHistoryRecord(0, today.AddMinutes(-5), "Cyberpunk", "E:", "USB_1", 2048, false, 300, @"C:\Games\Cyber", @"E:\Cyber", "Access denied"));

        PagedResult<CopyHistoryRecord> page = await historyService.GetPagedRecordsAsync(page: 1, pageSize: 10);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal("Cyberpunk", page.Items[0].GameName);

        (int total, int success, long bytes, int amount) = await historyService.GetFilteredStatsAsync();
        Assert.Equal(2, total);
        Assert.Equal(1, success);
        Assert.Equal(1024, bytes);
        Assert.Equal(200, amount);

        ReportService reportService = new(NullLogger<ReportService>.Instance);
        string csvPath = Path.Combine(_tempRoot, "history.csv");
        bool exported = await reportService.ExportHistoryToCsvAsync(csvPath, page.Items);
        Assert.True(exported);
        string csv = await File.ReadAllTextAsync(csvPath);
        Assert.Contains("Elden Ring", csv, StringComparison.Ordinal);
        Assert.Contains("Cyberpunk", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void HistoryAnalysisService_ClustersBatchesAndBuildsDashboard()
    {
        HistoryAnalysisService service = new();
        DateTime now = DateTime.Now;
        List<CopyHistoryRecord> records =
        [
            new(1, now.AddMinutes(-2), "Game A", "E:", "USB", 1000, true, 100),
            new(2, now, "Game B", "E:", "USB", 2000, true, 200),
            new(3, now.AddMinutes(-30), "Game C", "E:", "USB", 500, false, 150)
        ];

        service.ApplyBatchClustering(records);
        Assert.Equal(300, records[0].BatchAmount);
        Assert.Equal(300, records[1].BatchAmount);
        Assert.Equal(150, records[2].BatchAmount);

        HistoryStats stats = service.CalculateStats(records);
        Assert.Equal(3, stats.TotalItems);
        Assert.Equal(2, stats.SuccessfulItems);

        DashboardStats dashboard = service.BuildDashboard(records, rangeDays: 7);
        Assert.NotEmpty(dashboard.DailyTotals);
        Assert.Equal(3, dashboard.TopItems.Count);
    }

    [Fact]
    public async Task HistoryViewModel_InitializesFiltersAndDeactivatesRecipient()
    {
        StrongReferenceMessenger messenger = new();
        using CopyHistoryService historyService = new(_connectionFactory, messenger, NullLogger<CopyHistoryService>.Instance);
        await historyService.InitializeAsync();
        await historyService.AddRecordAsync(new CopyHistoryRecord(0, DateTime.Now, "Doom", "E:", "USB", 4096, true, 100, SubFilesJson: "[\"a.pak\",\"b.pak\"]"));

        HistoryViewModel vm = new(
            historyService,
            new ReportService(NullLogger<ReportService>.Instance),
            Mock.Of<IFilePickerService>(),
            new HistoryAnalysisService(),
            CreateSyncDispatcher(),
            messenger,
            NullLogger<HistoryViewModel>.Instance);

        await vm.InitializeAsync();
        Assert.True(vm.IsActive);
        Assert.True(messenger.IsRegistered<TransferBatchCompletedMessage>(vm));
        Assert.Single(vm.Records);
        Assert.False(vm.HasNoRecords);

        vm.SelectedRecord = vm.Records[0];
        Assert.True(vm.IsDrawerOpen);
        Assert.Equal(2, vm.SelectedRecordSubFiles.Count);

        vm.CloseDrawerCommand.Execute(null);
        Assert.False(vm.IsDrawerOpen);

        vm.IsActive = false;
        Assert.False(messenger.IsRegistered<TransferBatchCompletedMessage>(vm));
    }

    [Fact]
    public async Task CopyHistoryService_MigratesLegacySchemaSupportsSkippedStatusAndDensityToggle()
    {
        await using (SqliteCommand legacyCreate = _anchorConnection.CreateCommand())
        {
            legacyCreate.CommandText = @"
                CREATE TABLE CopyHistory (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Timestamp TEXT NOT NULL,
                    GameName TEXT NOT NULL,
                    TargetDriveLetter TEXT NOT NULL,
                    TargetDriveLabel TEXT NOT NULL,
                    BytesTransferred INTEGER NOT NULL,
                    IsSuccess INTEGER NOT NULL
                )";
            _ = await legacyCreate.ExecuteNonQueryAsync();
        }

        StrongReferenceMessenger messenger = new();
        using CopyHistoryService historyService = new(_connectionFactory, messenger, NullLogger<CopyHistoryService>.Instance);
        await historyService.InitializeAsync();

        DateTime now = DateTime.Now;
        await historyService.AddRecordAsync(new CopyHistoryRecord(0, now, "SkippedGame", "E:", "USB", 0, false, 0, DurationMs: 84_000, StatusText: "Skipped"));
        await historyService.AddRecordAsync(new CopyHistoryRecord(0, now.AddMinutes(-1), "DoneGame", "E:", "USB", 1024, true, 100, DurationMs: 3_665_000, StatusText: "Completed"));

        PagedResult<CopyHistoryRecord> skipped = await historyService.GetPagedRecordsAsync(statusFilter: TransferStatus.Skipped);
        Assert.Single(skipped.Items);
        Assert.True(skipped.Items[0].IsSkippedStatus);
        Assert.Equal("01:24", skipped.Items[0].FormattedDuration);

        PagedResult<CopyHistoryRecord> completed = await historyService.GetPagedRecordsAsync(statusFilter: TransferStatus.Completed);
        Assert.Single(completed.Items);
        Assert.Equal("1:01:05", completed.Items[0].FormattedDuration);

        HistoryViewModel vm = new(
            historyService,
            new ReportService(NullLogger<ReportService>.Instance),
            Mock.Of<IFilePickerService>(),
            new HistoryAnalysisService(),
            CreateSyncDispatcher(),
            messenger,
            NullLogger<HistoryViewModel>.Instance);

        await vm.InitializeAsync();
        Assert.NotEqual("\u2014", vm.LastUpdatedText);
        Assert.False(vm.IsCompactDensity);
        Assert.Equal("Comfortable", vm.DensityLabel);

        vm.ToggleDensityCommand.Execute(null);
        Assert.True(vm.IsCompactDensity);
        Assert.Equal("Compact", vm.DensityLabel);

        vm.SelectedStatusFilterString = HistoryViewModel.StatusSkipped;
        DateTime deadline = DateTime.UtcNow.AddSeconds(2);
        while (vm.Records.Count != 1 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        Assert.Single(vm.Records);
        Assert.Equal("SkippedGame", vm.Records[0].GameName);
    }
}
