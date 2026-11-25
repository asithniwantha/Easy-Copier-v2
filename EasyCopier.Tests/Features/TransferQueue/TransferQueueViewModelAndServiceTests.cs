using CommunityToolkit.Mvvm.Messaging;
using EasyCopier.Features.Shell.Contracts;
using EasyCopier.Features.TransferQueue;
using EasyCopier.Features.TransferQueue.Models;
using EasyCopier.Shared.Configuration;
using EasyCopier.Shared.Messages;
using EasyCopier.Shared.Platform;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace EasyCopier.Tests.Features.TransferQueue;

public sealed class TransferQueueViewModelAndServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"EasyCopierQueueTest_{Guid.NewGuid():N}");

    public TransferQueueViewModelAndServiceTests()
    {
        _ = Directory.CreateDirectory(_tempRoot);
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
        return mock.Object;
    }

    [Fact]
    public async Task TransferQueueService_ProcessesEnqueuedItemAndPublishesBatchCompletedMessage()
    {
        Mock<ISettingsService> settingsMock = new();
        settingsMock.Setup(s => s.LoadSettingsSync()).Returns(new AppSettings { PriceTier1 = 100 });

        Mock<IFileTransferService> fileTransferMock = new();
        Mock<IBatchNotificationService> notifyMock = new();
        StrongReferenceMessenger messenger = new();

        TaskCompletionSource<TransferBatchCompletedMessage> messageReceivedTcs = new();
        messenger.Register<TransferQueueViewModelAndServiceTests, TransferBatchCompletedMessage>(
            this,
            (recipient, msg) =>
            {
                messageReceivedTcs.TrySetResult(msg);
            });

        TaskCompletionSource itemCompletedTcs = new();

        TransferCompletedItemDescriptor itemDesc = new(
            "Game A",
            @"D:\Games\Game A",
            @"E:\Game A",
            2048,
            IsSuccess: true,
            Amount: 100,
            BatchAmount: 100,
            ErrorLog: string.Empty,
            SubFilesJson: "[]",
            Duration: TimeSpan.FromSeconds(3),
            StatusText: "Completed");

        fileTransferMock
            .Setup(f => f.TransferGamesAsync(It.IsAny<TransferRequest>(), It.IsAny<IProgress<TransferProgress>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TransferOutcome(true, "Copied 1 item", 1, 2048, DateTime.UtcNow, TimeSpan.FromSeconds(3), [itemDesc]));

        using TransferQueueService service = new(
            fileTransferMock.Object,
            NullLogger<TransferQueueService>.Instance,
            CreateSyncDispatcher(),
            settingsMock.Object,
            notifyMock.Object,
            messenger);
        service.ItemCompleted += (sender, completedItem) =>
        {
            itemCompletedTcs.TrySetResult();
        };

        using TransferActivityMonitor monitor = new(service);

        RemovableDrive drive = new("E:", "USB_DRIVE", "exFAT", 10_000_000_000L, 5_000_000_000L, 10.0);
        GameEntry game = new("Game A", @"D:\Games\Game A", 2048, null, DateTime.UtcNow, false);
        TransferQueueItem queued = service.Enqueue([new TransferItem(game)], drive, @"E:\");

        TransferBatchCompletedMessage msg = await messageReceivedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await itemCompletedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("E:", msg.TargetDriveLetter);
        Assert.Equal("USB_DRIVE", msg.TargetDriveLabel);
        Assert.Equal(1, msg.SuccessCount);
        Assert.Equal(0, msg.FailureCount);
        Assert.Equal(2048L, msg.TotalBytesTransferred);
        Assert.Single(msg.ItemResults!);
        Assert.Equal("Completed", msg.ItemResults![0].StatusText);

        Assert.Equal(TransferQueueItemStatus.Completed, queued.Status);
        Assert.False(monitor.HasActiveTransfers);

        service.ClearFinished();
        Assert.Empty(service.QueueItems);
    }

    [Fact]
    public async Task TransferQueueViewModel_ResolvesConflictsAndEnqueuesViaMessenger()
    {
        string gameFolder = Path.Combine(_tempRoot, "Source", "ExistingGame");
        string destDriveRoot = Path.Combine(_tempRoot, "DestDrive");
        string destExisting = Path.Combine(destDriveRoot, "ExistingGame");
        Directory.CreateDirectory(gameFolder);
        Directory.CreateDirectory(destExisting);

        Mock<IDriveDiscoveryService> discoveryMock = new();
        RemovableDrive drive = new("E:", "USB", "exFAT", 10_000_000_000L, 5_000_000_000L, 10.0);
        discoveryMock.Setup(d => d.GetRemovableDrivesAsync()).ReturnsAsync([drive]);

        DriveCoordinatorService coordinator = new(discoveryMock.Object, new DriveValidationService(NullLogger<DriveValidationService>.Instance));

        Mock<ITransferQueueService> queueMock = new();
        System.Collections.ObjectModel.ObservableCollection<TransferQueueItem> queueCollection = [];
        queueMock.SetupGet(q => q.QueueItems).Returns(queueCollection);

        List<TransferItem>? enqueuedItems = null;
        TaskCompletionSource enqueuedTcs = new();
        queueMock
            .Setup(q => q.Enqueue(It.IsAny<IReadOnlyList<TransferItem>>(), It.IsAny<RemovableDrive>(), It.IsAny<string>()))
            .Callback<IReadOnlyList<TransferItem>, RemovableDrive, string>((items, d, p) =>
            {
                enqueuedItems = [.. items];
                _ = enqueuedTcs.TrySetResult();
            })
            .Returns((IReadOnlyList<TransferItem> items, RemovableDrive d, string p) => new TransferQueueItem(items, d, p, 100));

        Mock<IFileTransferService> transferMock = new();
        transferMock.Setup(t => t.GetFolderStatsAsync(It.IsAny<string>())).ReturnsAsync((1024L, 2));

        Mock<IConflictPromptService> conflictMock = new();
        conflictMock
            .Setup(c => c.PromptConflictAsync("ExistingGame", 1024L, 2, 1024L, 2))
            .ReturnsAsync((CopyAction.Replace, true));

        StrongReferenceMessenger messenger = new();

        using TransferQueueViewModel vm = new(
            coordinator,
            queueMock.Object,
            transferMock.Object,
            new Mock<IFolderPickerService>().Object,
            new FileSystemService(),
            conflictMock.Object,
            new Mock<IProcessService>().Object,
            CreateSyncDispatcher(),
            messenger,
            NullLogger<TransferQueueViewModel>.Instance);

        await vm.InitializeAsync();
        vm.CustomDestinationPath = destDriveRoot;

        // Bypass root drive letter match just to point at temp test folder for conflict prompt check
        await vm.EnqueueEntriesAsync([new GameEntry("ExistingGame", gameFolder, 1024L, null, DateTime.UtcNow, false)]);

        await enqueuedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(enqueuedItems);
        Assert.Single(enqueuedItems!);
        Assert.Equal(CopyAction.Replace, enqueuedItems![0].Action);
        Assert.Null(vm.CustomDestinationPath);
    }
}
