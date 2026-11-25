using CommunityToolkit.Mvvm.Messaging;
using EasyCopier.Features.OsImageLauncher;
using EasyCopier.Features.Shell.Contracts;
using EasyCopier.Shared.Configuration;
using EasyCopier.Shared.Messages;
using EasyCopier.Shared.Platform;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace EasyCopier.Tests.Features.OsImageLauncher;

public sealed class OsImageLauncherTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"EasyCopierOsTest_{Guid.NewGuid():N}");

    public OsImageLauncherTests()
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

    [Fact]
    public void RufusResolutionHelper_ParsesVersionsAndResolvesHighestVersionBinary()
    {
        Assert.Equal(new Version(4, 7), RufusResolutionHelper.TryParseVersionFromFileName("rufus-4.7.exe"));
        Assert.Equal(new Version(4, 11, 1), RufusResolutionHelper.TryParseVersionFromFileName("rufus-4.11.1.exe"));
        Assert.Null(RufusResolutionHelper.TryParseVersionFromFileName("not-rufus.exe"));

        string oldExe = Path.Combine(_tempRoot, "rufus-4.5.exe");
        string newExe = Path.Combine(_tempRoot, "rufus-4.7.exe");
        File.WriteAllText(oldExe, "old");
        File.WriteAllText(newExe, "new");

        string resolved = RufusResolutionHelper.ResolveLatestRufusPath(oldExe);
        Assert.Equal(newExe, resolved);
    }

    [Fact]
    public async Task IsoBootstrapperService_LaunchWithIsoAsync_ValidatesAndLaunchesExecutable()
    {
        string isoPath = Path.Combine(_tempRoot, "windows11.iso");
        string oldRufus = Path.Combine(_tempRoot, "rufus-4.5.exe");
        string latestRufus = Path.Combine(_tempRoot, "rufus-4.7.exe");
        File.WriteAllText(isoPath, "iso");
        File.WriteAllText(oldRufus, "old");
        File.WriteAllText(latestRufus, "new");

        AppSettings savedSettings = new() { RufusExecutablePath = oldRufus };

        Mock<ISettingsService> settingsMock = new();
        settingsMock.Setup(s => s.LoadSettingsAsync()).ReturnsAsync(savedSettings);
        settingsMock.Setup(s => s.SaveSettingsAsync(It.IsAny<AppSettings>())).Returns(Task.CompletedTask);

        Mock<IProcessService> processMock = new();

        IsoBootstrapperService service = new(
            settingsMock.Object,
            new FileSystemService(),
            processMock.Object,
            new RufusExecutableResolverAdapter(),
            NullLogger<IsoBootstrapperService>.Instance);

        (bool missingSuccess, _) = await service.LaunchWithIsoAsync(Path.Combine(_tempRoot, "missing.iso"));
        Assert.False(missingSuccess);

        (bool success, string msg) = await service.LaunchWithIsoAsync(isoPath);
        Assert.True(success);
        Assert.Contains("windows11.iso", msg, StringComparison.Ordinal);
        settingsMock.Verify(s => s.SaveSettingsAsync(It.Is<AppSettings>(a => a.RufusExecutablePath == latestRufus)), Times.Once);
        processMock.Verify(p => p.LaunchExecutable(latestRufus, $"-i \"{isoPath}\""), Times.Once);
    }

    [Fact]
    public async Task OsImagesTabViewModel_ScansFiltersSortsAndLaunchesRufus()
    {
        File.WriteAllBytes(Path.Combine(_tempRoot, "Ubuntu.iso"), new byte[100]);
        File.WriteAllBytes(Path.Combine(_tempRoot, "Arch.iso"), new byte[500]);
        File.WriteAllText(Path.Combine(_tempRoot, "readme.txt"), "ignore");

        Mock<ISettingsService> settingsMock = new();
        settingsMock.Setup(s => s.LoadSettingsAsync()).ReturnsAsync(new AppSettings
        {
            OsImageSourceFolders = [_tempRoot]
        });

        Mock<IIsoBootstrapperService> bootstrapperMock = new();
        bootstrapperMock
            .Setup(b => b.LaunchWithIsoAsync(It.IsAny<string>()))
            .ReturnsAsync((true, "Launched!"));

        Mock<IDispatcherService> dispatcherMock = new();
        dispatcherMock
            .Setup(d => d.TryEnqueue(It.IsAny<Action>()))
            .Returns((Action a) =>
            {
                a();
                return true;
            });

        StrongReferenceMessenger messenger = new();
        LibrarySelectionCoordinator coordinator = new(settingsMock.Object, messenger);
        coordinator.SetActiveTab(isOsImagesTabActive: true);

        using OsImagesTabViewModel vm = new(
            coordinator,
            settingsMock.Object,
            bootstrapperMock.Object,
            dispatcherMock.Object,
            messenger,
            NullLogger<OsImagesTabViewModel>.Instance);

        await vm.ScanOsImagesCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.OsImages.Count);
        Assert.False(vm.IsOsImagesEmpty);

        vm.SelectedOsImageSortOption = OsImageSortOption.Name;
        Assert.Equal(["Arch", "Ubuntu"], vm.OsImages.Select(x => x.Name).ToArray());

        vm.SearchText = "ub";
        Assert.Single(vm.OsImages);
        Assert.Equal("Ubuntu", vm.OsImages[0].Name);

        vm.UpdateSelection([vm.OsImages[0]]);
        await vm.OpenInRufusCommand.ExecuteAsync(null);
        Assert.Equal("Launched!", vm.StatusMessage);

        vm.SearchText = string.Empty;
        vm.SelectedOsImageSortOption = OsImageSortOption.Size;
        Assert.Equal(["Arch", "Ubuntu"], vm.OsImages.Select(x => x.Name).ToArray());
    }

    [Fact]
    public void OsImageDetailsViewModel_InitializesMetadataForValidAndMissingPaths()
    {
        string isoPath = Path.Combine(_tempRoot, "test.iso");
        File.WriteAllText(isoPath, "data");

        OsImageDetailsViewModel vm = new(new FileSystemService());
        vm.Initialize("test", isoPath);
        Assert.False(vm.IsStatusVisible);
        Assert.NotEqual("N/A", vm.DateCreatedFormatted);

        vm.Initialize("missing", Path.Combine(_tempRoot, "nope.iso"));
        Assert.True(vm.IsStatusVisible);
        Assert.Equal("N/A", vm.DateCreatedFormatted);
    }
}
