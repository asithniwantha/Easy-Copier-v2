using CommunityToolkit.Mvvm.Messaging;
using EasyCopier.Features.Library;
using EasyCopier.Features.Library.Details;
using EasyCopier.Features.Library.Duplicates;
using EasyCopier.Features.Library.Models;
using EasyCopier.Features.Library.Scanning;
using EasyCopier.Features.Library.Tabs;
using EasyCopier.Features.Shell.Contracts;
using EasyCopier.Shared.Configuration;
using EasyCopier.Shared.Messages;
using EasyCopier.Shared.Platform;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace EasyCopier.Tests.Features.Library;

public sealed class LibraryCatalogAndViewModelsTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"EasyCopierCatalogTest_{Guid.NewGuid():N}");

    public LibraryCatalogAndViewModelsTests()
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
        mock.Setup(d => d.TryEnqueue(It.IsAny<Func<Task>>())).Returns((Func<Task> f) =>
        {
            _ = f();
            return true;
        });
        return mock.Object;
    }

    [Fact]
    public async Task LibraryCatalogAndTabViewModels_ScanFilterAndReactToSettingsChangedMessage()
    {
        string gameRoot = Path.Combine(_tempRoot, "Games");
        _ = Directory.CreateDirectory(Path.Combine(gameRoot, "Cyberpunk"));
        _ = Directory.CreateDirectory(Path.Combine(gameRoot, "Forza"));

        AppSettings settings = new()
        {
            AutoScanOnStartup = true,
            GameSourceFolders = [gameRoot]
        };

        Mock<ISettingsService> settingsMock = new();
        settingsMock.Setup(s => s.LoadSettingsAsync()).ReturnsAsync(settings);
        settingsMock.Setup(s => s.LoadSettingsSync()).Returns(settings);
        settingsMock.Setup(s => s.SaveSettingsAsync(It.IsAny<AppSettings>())).Returns(Task.CompletedTask);

        WeakReferenceMessenger messenger = new();
        GameScannerService gameScanner = new(NullLogger<GameScannerService>.Instance, Mock.Of<IThumbnailService>());
        LibraryScannerService scannerService = new(gameScanner);
        LibraryCacheService cacheService = new(NullLogger<LibraryCacheService>.Instance, Path.Combine(_tempRoot, "cache.json"));

        using LibraryCatalogService catalog = new(
            settingsMock.Object,
            scannerService,
            cacheService,
            new LibraryFilterService(),
            CreateSyncDispatcher(),
            messenger,
            NullLogger<LibraryCatalogService>.Instance);

        ILibrarySelectionCoordinator coordinator = new LibrarySelectionCoordinator(settingsMock.Object, messenger);
        using GamesTabViewModel gamesTab = new(coordinator, catalog);
        using AppsTabViewModel appsTab = new(coordinator, catalog);
        using TvAndFilmsTabViewModel tvTab = new(coordinator, catalog);

        await catalog.InitializeAsync();
        Assert.Equal(2, gamesTab.Games.Count);

        gamesTab.SearchText = "Cyber";
        Assert.Single(gamesTab.Games);
        Assert.Equal("Cyberpunk", gamesTab.Games[0].Name);
        Assert.Single(gamesTab.GetSuggestions("Cyber"));

        gamesTab.SearchText = string.Empty;
        _ = Directory.CreateDirectory(Path.Combine(gameRoot, "Hades"));
        messenger.Send(new SettingsChangedMessage(SourcePathsUpdated: true, PriceTiersUpdated: false));

        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (gamesTab.Games.Count < 3 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(25);
        }

        Assert.Equal(3, gamesTab.Games.Count);
        Assert.False(appsTab.IsScanning);
        Assert.True(tvTab.IsTvAndFilmsEmpty);
    }

    [Fact]
    public async Task DuplicateManagerViewModel_GroupsAndRecyclesSelectedDuplicates()
    {
        DuplicateGroup group = new("SharedGame",
        [
            new DuplicateItemEntry("SharedGame", "D:\\Games\\SharedGame", LibraryCategory.Game, 1024, new DateTime(2024, 1, 1)),
            new DuplicateItemEntry("SharedGame", "E:\\Games\\SharedGame", LibraryCategory.Game, 1024, new DateTime(2025, 1, 1))
        ]);

        Mock<ILibraryScannerService> scannerMock = new();
        scannerMock.Setup(s => s.GetDuplicateGroupsAsync(It.IsAny<AppSettings>(), It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([group]);

        Mock<ISafeDeletionService> deletionMock = new();
        deletionMock.Setup(d => d.BatchDeleteToRecycleBinAsync(
                It.IsAny<IReadOnlyList<(string Path, long Bytes)>>(),
                It.IsAny<IProgress<double>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SafeDeletionResult(1, 0, 1024, []));

        Mock<ISettingsService> settingsMock = new();
        settingsMock.Setup(s => s.LoadSettingsSync()).Returns(new AppSettings());

        Mock<IDialogService> dialogMock = new();
        dialogMock.Setup(d => d.ShowConfirmationDialogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        using DuplicateManagerViewModel vm = new(
            scannerMock.Object,
            deletionMock.Object,
            settingsMock.Object,
            dialogMock.Object,
            CreateSyncDispatcher(),
            NullLogger<DuplicateManagerViewModel>.Instance);

        await vm.ScanDuplicatesAsync();
        Assert.Single(vm.DuplicateGroups);

        vm.SelectOlderCopies();
        Assert.Equal(1, vm.MarkedForDeletionCount);
        Assert.Equal(1024, vm.TotalRecoverableBytes);

        await vm.DeleteSelectedAsync();
        Assert.Empty(vm.DuplicateGroups);
        Assert.Equal(0, vm.MarkedForDeletionCount);
    }

    [Fact]
    public async Task VideoDetailsViewModel_EnumeratesFilesRelativeAndSetsSummary()
    {
        string showDir = Path.Combine(_tempRoot, "ShowFolder");
        _ = Directory.CreateDirectory(showDir);
        await File.WriteAllBytesAsync(Path.Combine(showDir, "Episode1.mkv"), new byte[128]);

        VideoDetailsViewModel vm = new(new FileSystemService());
        await vm.InitializeAsync("ShowFolder", showDir);

        Assert.False(vm.IsStatusVisible);
        Assert.Single(vm.Files);
        Assert.Equal("Episode1.mkv", vm.Files[0].Name);
        Assert.Contains("1 file(s)", vm.Summary, StringComparison.Ordinal);
    }
}
