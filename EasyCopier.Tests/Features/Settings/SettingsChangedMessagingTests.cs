using CommunityToolkit.Mvvm.Messaging;
using EasyCopier.Features.Library;
using EasyCopier.Features.Library.Scanning;
using EasyCopier.Features.OsImageLauncher;
using EasyCopier.Features.Settings;
using EasyCopier.Features.Shell.Contracts;
using EasyCopier.Shared.Configuration;
using EasyCopier.Shared.Models;
using EasyCopier.Shared.Platform;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace EasyCopier.Tests.Features.Settings;

public sealed class SettingsChangedMessagingTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"EasyCopierSettingsMsgTest_{Guid.NewGuid():N}");

    public SettingsChangedMessagingTests()
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
    public async Task SavingSettings_NotifiesSelectionCoordinatorLibraryCatalogAndOsImagesAcrossSlices()
    {
        string gameFolder = Path.Combine(_tempRoot, "Games");
        string isoFolder = Path.Combine(_tempRoot, "ISOs");
        Directory.CreateDirectory(gameFolder);
        Directory.CreateDirectory(isoFolder);
        await File.WriteAllBytesAsync(Path.Combine(isoFolder, "debian-13.iso"), new byte[1024]);

        string settingsPath = Path.Combine(_tempRoot, "appsettings.json");
        SettingsService settingsService = new(NullLogger<SettingsService>.Instance, settingsPath);
        await settingsService.SaveSettingsAsync(new AppSettings
        {
            PriceTier1 = 100
        });

        StrongReferenceMessenger messenger = new();
        IDispatcherService dispatcher = CreateSyncDispatcher();

        LibrarySelectionCoordinator selectionCoordinator = new(settingsService, messenger);
        selectionCoordinator.SetCategorySelection(
            LibraryCategory.Game,
            [new GameEntry("Hollow Knight", gameFolder, 2L * 1024 * 1024 * 1024, null, DateTime.UtcNow, false)]);
        Assert.Equal(100, selectionCoordinator.SelectedTotalPrice);

        TaskCompletionSource<AppSettings> catalogScannedWithSettings = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock<ILibraryScannerService> scannerMock = new();
        scannerMock
            .Setup(s => s.ScanCoreLibrariesAsync(It.IsAny<AppSettings>(), It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppSettings s, IProgress<string>? _, CancellationToken _) =>
            {
                catalogScannedWithSettings.TrySetResult(s);
                return (
                    Games: (IReadOnlyList<GameEntry>)[new GameEntry("Hollow Knight", gameFolder, 2_000_000_000L, null, DateTime.UtcNow, false)],
                    Apps: (IReadOnlyList<GameEntry>)[],
                    TvAndFilms: (IReadOnlyList<GameEntry>)[]
                );
            });

        using LibraryCatalogService catalog = new(
            settingsService,
            scannerMock.Object,
            new LibraryCacheService(NullLogger<LibraryCacheService>.Instance, Path.Combine(_tempRoot, "cache.json")),
            new LibraryFilterService(),
            dispatcher,
            messenger,
            NullLogger<LibraryCatalogService>.Instance);

        using OsImagesTabViewModel osImagesVm = new(
            selectionCoordinator,
            settingsService,
            Mock.Of<IIsoBootstrapperService>(),
            dispatcher,
            messenger,
            NullLogger<OsImagesTabViewModel>.Instance);

        Mock<ISourceFolderValidator> validatorMock = new();
        validatorMock
            .Setup(v => v.ValidateSourceFoldersAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync((IEnumerable<string> paths) =>
                paths.Select(p => new SourceFolder(p, true, DateTime.UtcNow)).ToList());

        using SettingsViewModel settingsVm = new(
            NullLogger<SettingsViewModel>.Instance,
            settingsService,
            Mock.Of<IFolderPickerService>(),
            Mock.Of<IFilePickerService>(),
            validatorMock.Object,
            Mock.Of<IProcessService>(),
            Mock.Of<IGameMetadataDownloader>(),
            Mock.Of<IRufusExecutableResolver>(),
            Mock.Of<IStartupService>(),
            dispatcher,
            Mock.Of<IUpdateService>(),
            Mock.Of<IDialogService>(),
            Mock.Of<IWindowService>(),
            messenger);

        settingsVm.LoadSettings(await settingsService.LoadSettingsAsync());
        settingsVm.PriceTier1 = "225";
        settingsVm.GameSourceFolders.Add(gameFolder);
        settingsVm.OsImageSourceFolders.Add(isoFolder);

        await settingsVm.SaveAndCloseCommand.ExecuteAsync(null);

        // 1. Price update immediately propagates to selection aggregates
        Assert.Equal(225, selectionCoordinator.SelectedTotalPrice);

        // 2. LibraryCatalog receives SettingsChangedMessage(SourcePathsUpdated: true) and scans new folders
        AppSettings scannedSettings = await catalogScannedWithSettings.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Contains(gameFolder, scannedSettings.GameSourceFolders);
        Assert.Single(catalog.Games);

        // 3. OsImagesTabViewModel receives SettingsChangedMessage(SourcePathsUpdated: true) and discovers the ISO
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (osImagesVm.OsImages.Count == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        GameEntry discoveredIso = Assert.Single(osImagesVm.OsImages);
        Assert.Equal("debian-13", discoveredIso.Name);
    }
}

