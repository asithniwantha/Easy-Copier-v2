using CommunityToolkit.Mvvm.Messaging;
using EasyCopier.Features.Shell.Contracts;
using EasyCopier.Shared.Configuration;
using EasyCopier.Shared.Messages;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EasyCopier.Tests.Features.Shell;

public sealed class LibrarySelectionCoordinatorTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"EasyCopierTests_{Guid.NewGuid():N}");

    [Fact]
    public void SelectionCoordinator_EnforcesOsImageTabExclusivity()
    {
        string settingsPath = Path.Combine(_tempDir, "appsettings.json");
        var settingsService = new SettingsService(NullLogger<SettingsService>.Instance, settingsPath);
        var coordinator = new LibrarySelectionCoordinator(settingsService);

        var game = new GameEntry("Halo", @"D:\Games\Halo", 5L * 1024 * 1024 * 1024, null, DateTime.UtcNow, false, LibraryCategory.Game);
        var iso = new GameEntry("Win11", @"D:\ISO\Win11.iso", 6L * 1024 * 1024 * 1024, null, DateTime.UtcNow, true, LibraryCategory.OsImage);

        coordinator.SetCategorySelection(LibraryCategory.Game, [game]);
        coordinator.SetCategorySelection(LibraryCategory.OsImage, [iso]);

        coordinator.SetActiveTab(isOsImagesTabActive: false);
        Assert.Equal([game], coordinator.SelectedEntries);
        Assert.Equal(100, coordinator.SelectedTotalPrice);

        coordinator.SetActiveTab(isOsImagesTabActive: true);
        Assert.Equal([iso], coordinator.SelectedEntries);
    }

    [Fact]
    public async Task SelectionCoordinator_RefreshesPricesWhenSettingsChangedMessageReceived()
    {
        string settingsPath = Path.Combine(_tempDir, "appsettings.json");
        var settingsService = new SettingsService(NullLogger<SettingsService>.Instance, settingsPath);
        var messenger = new WeakReferenceMessenger();
        var coordinator = new LibrarySelectionCoordinator(settingsService, messenger);

        var game = new GameEntry("Indie", @"D:\Games\Indie", 2L * 1024 * 1024 * 1024, null, DateTime.UtcNow, false, LibraryCategory.Game);
        coordinator.SetCategorySelection(LibraryCategory.Game, [game]);
        Assert.Equal(100, coordinator.SelectedTotalPrice);

        await settingsService.SaveSettingsAsync(new AppSettings { PriceTier1 = 250 });
        messenger.Send(new SettingsChangedMessage(SourcePathsUpdated: false, PriceTiersUpdated: true));

        Assert.Equal(250, coordinator.SelectedTotalPrice);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}

