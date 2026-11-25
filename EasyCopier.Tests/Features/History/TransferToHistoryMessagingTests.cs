using CommunityToolkit.Mvvm.Messaging;
using EasyCopier.Features.History;
using EasyCopier.Features.History.Models;
using EasyCopier.Features.Shell.Contracts;
using EasyCopier.Features.TransferQueue;
using EasyCopier.Features.TransferQueue.Models;
using EasyCopier.Shared.Configuration;
using EasyCopier.Shared.Messages;
using EasyCopier.Shared.Models;
using EasyCopier.Shared.Persistence;
using EasyCopier.Shared.Platform;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace EasyCopier.Tests.Features.History;

public sealed class TransferToHistoryMessagingTests : IAsyncLifetime
{
    private readonly string _dbName = $"TransferHistoryMsg_{Guid.NewGuid():N}";
    private SqliteConnection _anchorConnection = null!;
    private SqliteConnectionFactory _connectionFactory = null!;

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
    public async Task TransferQueueService_CompletedBatch_PersistsItemRecordsIntoCopyHistoryServiceViaMessenger()
    {
        StrongReferenceMessenger messenger = new();
        using CopyHistoryService historyService = new(_connectionFactory, messenger, NullLogger<CopyHistoryService>.Instance);
        await historyService.InitializeAsync();

        TaskCompletionSource historyPersistedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        historyService.HistoryChanged += (_, _) => historyPersistedTcs.TrySetResult();

        Mock<ISettingsService> settingsMock = new();
        settingsMock.Setup(s => s.LoadSettingsSync()).Returns(new AppSettings
        {
            PriceTier1 = 100,
            PriceTier2 = 200
        });

        TransferCompletedItemDescriptor item1 = new(
            "Game Alpha",
            @"C:\Games\Alpha",
            @"E:\Alpha",
            2_000_000_000L,
            IsSuccess: true,
            Amount: 100,
            BatchAmount: 300,
            ErrorLog: string.Empty,
            SubFilesJson: "[\"alpha.bin\"]",
            Duration: TimeSpan.FromSeconds(45),
            StatusText: "Completed");

        TransferCompletedItemDescriptor item2 = new(
            "Game Beta",
            @"C:\Games\Beta",
            @"E:\Beta",
            0L,
            IsSuccess: false,
            Amount: 200,
            BatchAmount: 300,
            ErrorLog: "Target write error",
            SubFilesJson: "[]",
            Duration: TimeSpan.FromSeconds(12),
            StatusText: "Failed");

        Mock<IFileTransferService> fileTransferMock = new();
        fileTransferMock
            .Setup(f => f.TransferGamesAsync(It.IsAny<TransferRequest>(), It.IsAny<IProgress<TransferProgress>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TransferOutcome(false, "Completed with errors", 1, 2_000_000_000L, DateTime.UtcNow, TimeSpan.FromSeconds(57), [item1, item2]));

        using TransferQueueService queueService = new(
            fileTransferMock.Object,
            NullLogger<TransferQueueService>.Instance,
            CreateSyncDispatcher(),
            settingsMock.Object,
            Mock.Of<IBatchNotificationService>(),
            messenger);

        RemovableDrive drive = new("E:", "GAMES_USB", "exFAT", 128_000_000_000L, 64_000_000_000L, 50.0);
        GameEntry alphaEntry = new("Game Alpha", @"C:\Games\Alpha", 2_000_000_000L, null, DateTime.UtcNow, false);
        GameEntry betaEntry = new("Game Beta", @"C:\Games\Beta", 6_000_000_000L, null, DateTime.UtcNow, true);

        _ = queueService.Enqueue([new TransferItem(alphaEntry), new TransferItem(betaEntry)], drive, @"E:\");

        await historyPersistedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        PagedResult<CopyHistoryRecord> page = await historyService.GetPagedRecordsAsync(page: 1, pageSize: 10);
        Assert.Equal(2, page.TotalCount);

        CopyHistoryRecord persistedAlpha = Assert.Single(page.Items, r => r.GameName == "Game Alpha");
        Assert.True(persistedAlpha.IsSuccess);
        Assert.Equal("E:", persistedAlpha.TargetDriveLetter);
        Assert.Equal("GAMES_USB", persistedAlpha.TargetDriveLabel);
        Assert.Equal(2_000_000_000L, persistedAlpha.BytesTransferred);
        Assert.Equal(100, persistedAlpha.Amount);
        Assert.Equal(45_000L, persistedAlpha.DurationMs);
        Assert.Equal("Completed", persistedAlpha.StatusText);

        CopyHistoryRecord persistedBeta = Assert.Single(page.Items, r => r.GameName == "Game Beta");
        Assert.False(persistedBeta.IsSuccess);
        Assert.Equal("Target write error", persistedBeta.ErrorLog);
        Assert.Equal("Failed", persistedBeta.StatusText);
        Assert.Equal(12_000L, persistedBeta.DurationMs);
    }

    [Fact]
    public async Task CopyHistoryService_BatchCompletedWithoutItemResults_PersistsSummaryRecord()
    {
        StrongReferenceMessenger messenger = new();
        using CopyHistoryService historyService = new(_connectionFactory, messenger, NullLogger<CopyHistoryService>.Instance);
        await historyService.InitializeAsync();

        TaskCompletionSource historyPersistedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        historyService.HistoryChanged += (_, _) => historyPersistedTcs.TrySetResult();

        messenger.Send(new TransferBatchCompletedMessage(
            Guid.NewGuid(),
            "F:",
            SuccessCount: 3,
            FailureCount: 0,
            TotalBytesTransferred: 12_345_678L,
            Duration: TimeSpan.FromSeconds(30),
            TargetDriveLabel: "ARCHIVE",
            ItemResults: null));

        await historyPersistedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        PagedResult<CopyHistoryRecord> page = await historyService.GetPagedRecordsAsync(page: 1, pageSize: 10);
        CopyHistoryRecord summary = Assert.Single(page.Items);
        Assert.Equal("Transfer Batch (3 item(s))", summary.GameName);
        Assert.Equal("F:", summary.TargetDriveLetter);
        Assert.Equal("ARCHIVE", summary.TargetDriveLabel);
        Assert.Equal(12_345_678L, summary.BytesTransferred);
        Assert.True(summary.IsSuccess);
        Assert.Equal(30_000L, summary.DurationMs);
        Assert.Equal("Completed", summary.StatusText);
    }
}

