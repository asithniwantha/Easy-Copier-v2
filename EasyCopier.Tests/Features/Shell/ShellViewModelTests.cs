using CommunityToolkit.Mvvm.Messaging;
using EasyCopier.Features.Library;
using EasyCopier.Features.Library.Scanning;
using EasyCopier.Features.Library.Tabs;
using EasyCopier.Features.OsImageLauncher;
using EasyCopier.Features.Settings;
using EasyCopier.Features.Shell;
using EasyCopier.Features.Shell.Contracts;
using EasyCopier.Features.SmartAdder;
using EasyCopier.Features.TransferQueue;
using EasyCopier.Features.TransferQueue.Models;
using EasyCopier.Shared.Configuration;
using EasyCopier.Shared.Messages;
using EasyCopier.Shared.Models;
using EasyCopier.Shared.Platform;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace EasyCopier.Tests.Features.Shell;

public sealed class ShellViewModelTests
{
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

    private static (
        ShellViewModel Shell,
        StrongReferenceMessenger Messenger,
        Mock<IWindowService> WindowServiceMock,
        LibrarySelectionCoordinator SelectionCoordinator,
        TransferQueueViewModel TransferQueueVm) CreateShellSut()
    {
        StrongReferenceMessenger messenger = new();
        IDispatcherService dispatcher = CreateSyncDispatcher();
        Mock<IWindowService> windowServiceMock = new();

        Mock<ISettingsService> settingsMock = new();
        AppSettings defaultSettings = new()
        {
            PriceTier1 = 100,
            PriceTier2 = 200,
            PriceTier3 = 300,
            PriceTier4 = 400
        };
        settingsMock.Setup(s => s.LoadSettingsSync()).Returns(defaultSettings);
        settingsMock.Setup(s => s.LoadSettingsAsync()).ReturnsAsync(defaultSettings);

        LibrarySelectionCoordinator selectionCoordinator = new(settingsMock.Object, messenger);

        LibraryCatalogService catalog = new(
            settingsMock.Object,
            Mock.Of<ILibraryScannerService>(),
            Mock.Of<ILibraryCacheService>(),
            Mock.Of<ILibraryFilterService>(),
            dispatcher,
            messenger,
            NullLogger<LibraryCatalogService>.Instance);

        GamesTabViewModel gamesTab = new(selectionCoordinator, catalog);
        AppsTabViewModel appsTab = new(selectionCoordinator, catalog);
        TvAndFilmsTabViewModel tvTab = new(selectionCoordinator, catalog);

        OsImagesTabViewModel osImagesTab = new(
            selectionCoordinator,
            settingsMock.Object,
            Mock.Of<IIsoBootstrapperService>(),
            dispatcher,
            messenger,
            NullLogger<OsImagesTabViewModel>.Instance);

        Mock<ITransferQueueService> queueServiceMock = new();
        queueServiceMock.Setup(q => q.QueueItems).Returns([]);

        TransferQueueViewModel transferQueueVm = new(
            Mock.Of<IDriveCoordinatorService>(),
            queueServiceMock.Object,
            Mock.Of<IFileTransferService>(),
            Mock.Of<IFolderPickerService>(),
            Mock.Of<IFileSystemService>(),
            Mock.Of<IConflictPromptService>(),
            Mock.Of<IProcessService>(),
            dispatcher,
            messenger,
            NullLogger<TransferQueueViewModel>.Instance);

        SmartAdderViewModel smartAdderVm = new(
            windowServiceMock.Object,
            Mock.Of<ISmartAdderHistoryService>(),
            NullLogger<SmartAdderViewModel>.Instance);

        Mock<IUpdateService> updateMock = new();
        updateMock.Setup(u => u.CheckForUpdatesAsync()).ReturnsAsync(false);

        ShellViewModel shell = new(
            NullLogger<ShellViewModel>.Instance,
            windowServiceMock.Object,
            settingsMock.Object,
            updateMock.Object,
            dispatcher,
            selectionCoordinator,
            catalog,
            gamesTab,
            appsTab,
            tvTab,
            osImagesTab,
            transferQueueVm,
            smartAdderVm,
            messenger);

        return (shell, messenger, windowServiceMock, selectionCoordinator, transferQueueVm);
    }

    [Fact]
    public void CopySelectedGamesCommand_RespectsCanExecuteAndSendsEnqueueTransferRequestMessage()
    {
        (ShellViewModel shell, StrongReferenceMessenger messenger, _, LibrarySelectionCoordinator selection, TransferQueueViewModel queueVm) = CreateShellSut();
        using ShellViewModel disposedShell = shell;

        Assert.False(shell.CopySelectedGamesCommand.CanExecute(null));

        GameEntry game = new("Hades", @"C:\Games\Hades", 3L * 1024 * 1024 * 1024, null, DateTime.UtcNow, false);
        selection.SetCategorySelection(LibraryCategory.Game, [game]);
        Assert.False(shell.CopySelectedGamesCommand.CanExecute(null));

        RemovableDrive drive = new("E:", "GAMES", "NTFS", 64_000_000_000L, 32_000_000_000L, 50.0);
        queueVm.SelectedDrive = drive;
        Assert.True(shell.CopySelectedGamesCommand.CanExecute(null));

        shell.CurrentTabIndex = (int)LibraryTabIndex.OsImages;
        Assert.True(shell.IsOsImagesTabActive);
        Assert.False(shell.CopySelectedGamesCommand.CanExecute(null));

        shell.CurrentTabIndex = (int)LibraryTabIndex.Games;
        Assert.True(shell.CopySelectedGamesCommand.CanExecute(null));

        EnqueueTransferRequestMessage? captured = null;
        messenger.Register<ShellViewModelTests, EnqueueTransferRequestMessage>(this, (_, msg) => captured = msg);

        shell.CopySelectedGamesCommand.Execute(null);

        Assert.NotNull(captured);
        Assert.Equal("E:", captured!.TargetDriveLetter);
        TransferSourceItem item = Assert.Single(captured.Items);
        Assert.Equal("Hades", item.Name);
        Assert.Equal(@"C:\Games\Hades", item.SourcePath);
        Assert.Equal(100m, item.CalculatedPrice);
    }

    [Fact]
    public void NavigationCommands_RouteToExpectedWindowKindsAndTabSpecificAddFolderAction()
    {
        (ShellViewModel shell, _, Mock<IWindowService> windowServiceMock, LibrarySelectionCoordinator selection, _) = CreateShellSut();
        using ShellViewModel disposedShell = shell;

        shell.OpenSettingsCommand.Execute(null);
        shell.OpenHistoryCommand.Execute(null);
        shell.OpenAboutCommand.Execute(null);

        windowServiceMock.Verify(w => w.ShowWindow(AppWindowKind.Settings, null, null), Times.Once);
        windowServiceMock.Verify(w => w.ShowWindow(AppWindowKind.History, null, null), Times.Once);
        windowServiceMock.Verify(w => w.ShowWindow(AppWindowKind.About, null, null), Times.Once);

        shell.CurrentTabIndex = (int)LibraryTabIndex.Games;
        shell.AddSourceFolderCommand.Execute(null);
        shell.CurrentTabIndex = (int)LibraryTabIndex.Apps;
        shell.AddSourceFolderCommand.Execute(null);
        shell.CurrentTabIndex = (int)LibraryTabIndex.TvAndFilms;
        shell.AddSourceFolderCommand.Execute(null);
        shell.CurrentTabIndex = (int)LibraryTabIndex.OsImages;
        shell.AddSourceFolderCommand.Execute(null);

        windowServiceMock.Verify(w => w.ShowWindow(AppWindowKind.Settings, SettingsOpenAction.AddGameFolder, null), Times.Once);
        windowServiceMock.Verify(w => w.ShowWindow(AppWindowKind.Settings, SettingsOpenAction.AddAppFolder, null), Times.Once);
        windowServiceMock.Verify(w => w.ShowWindow(AppWindowKind.Settings, SettingsOpenAction.AddTvAndFilmFolder, null), Times.Once);
        windowServiceMock.Verify(w => w.ShowWindow(AppWindowKind.Settings, SettingsOpenAction.AddOsImageFolder, null), Times.Once);

        shell.CurrentTabIndex = (int)LibraryTabIndex.Games;
        selection.SetCategorySelection(LibraryCategory.Game, [new GameEntry("G", @"C:\G", 10, null, DateTime.UtcNow, false)]);
        Assert.True(shell.HasSelectedGames);
        shell.ClearSelectionCommand.Execute(null);
        Assert.False(shell.HasSelectedGames);
    }

    [Fact]
    public void BatchCompletedMessage_UpdatesStatusAndBanner_AndDisposeUnregistersRecipient()
    {
        (ShellViewModel shell, StrongReferenceMessenger messenger, _, _, _) = CreateShellSut();

        Assert.True(messenger.IsRegistered<TransferBatchCompletedMessage>(shell));

        messenger.Send(new TransferBatchCompletedMessage(
            Guid.NewGuid(),
            "E:",
            SuccessCount: 2,
            FailureCount: 0,
            TotalBytesTransferred: 2_147_483_648L,
            Duration: TimeSpan.FromSeconds(10),
            TargetDriveLabel: "USB"));

        Assert.Contains("Completed 2 item(s)", shell.StatusMessage, StringComparison.Ordinal);
        Assert.True(shell.IsGlobalNotificationVisible);
        Assert.True(shell.CanDisplayGlobalNotification);
        Assert.Equal(ValidationSeverity.Success, shell.GlobalNotificationSeverity);

        shell.Dispose();
        Assert.False(messenger.IsRegistered<TransferBatchCompletedMessage>(shell));
    }
}

