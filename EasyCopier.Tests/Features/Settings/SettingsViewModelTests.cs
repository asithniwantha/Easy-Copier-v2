using CommunityToolkit.Mvvm.Messaging;
using EasyCopier.Features.Settings;
using EasyCopier.Features.Shell.Contracts;
using EasyCopier.Shared.Configuration;
using EasyCopier.Shared.Messages;
using EasyCopier.Shared.Models;
using EasyCopier.Shared.Platform;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace EasyCopier.Tests.Features.Settings;

public sealed class SettingsViewModelTests
{
    private readonly Mock<ISettingsService> _settingsServiceMock = new();
    private readonly Mock<IFolderPickerService> _folderPickerMock = new();
    private readonly Mock<IFilePickerService> _filePickerMock = new();
    private readonly Mock<ISourceFolderValidator> _folderValidatorMock = new();
    private readonly Mock<IProcessService> _processServiceMock = new();
    private readonly Mock<IGameMetadataDownloader> _metadataDownloaderMock = new();
    private readonly Mock<IRufusExecutableResolver> _rufusResolverMock = new();
    private readonly Mock<IStartupService> _startupServiceMock = new();
    private readonly Mock<IDispatcherService> _dispatcherMock = new();
    private readonly Mock<IUpdateService> _updateServiceMock = new();
    private readonly Mock<IDialogService> _dialogServiceMock = new();
    private readonly Mock<IWindowService> _windowServiceMock = new();
    private readonly StrongReferenceMessenger _messenger = new();

    public SettingsViewModelTests()
    {
        _folderValidatorMock
            .Setup(v => v.ValidateSourceFoldersAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync((IEnumerable<string> paths) =>
                paths.Select(p => new SourceFolder(p, true, DateTime.UtcNow)).ToList());

        _settingsServiceMock
            .Setup(s => s.SaveSettingsAsync(It.IsAny<AppSettings>()))
            .Returns(Task.CompletedTask);
    }

    private SettingsViewModel CreateViewModel() => new(
        NullLogger<SettingsViewModel>.Instance,
        _settingsServiceMock.Object,
        _folderPickerMock.Object,
        _filePickerMock.Object,
        _folderValidatorMock.Object,
        _processServiceMock.Object,
        _metadataDownloaderMock.Object,
        _rufusResolverMock.Object,
        _startupServiceMock.Object,
        _dispatcherMock.Object,
        _updateServiceMock.Object,
        _dialogServiceMock.Object,
        _windowServiceMock.Object,
        _messenger);

    [Fact]
    public async Task SaveAndCloseCommand_WhenSourcePathsAndPriceTiersChange_BroadcastsAccurateSettingsChangedMessage()
    {
        using SettingsViewModel vm = CreateViewModel();
        vm.LoadSettings(new AppSettings
        {
            PriceTier1 = 100,
            GameSourceFolders = [@"C:\Games"]
        });

        SettingsChangedMessage? received = null;
        _messenger.Register<SettingsViewModelTests, SettingsChangedMessage>(this, (_, msg) => received = msg);

        bool closeRequested = false;
        vm.CloseRequested += (_, _) => closeRequested = true;

        vm.PriceTier1 = "150";
        vm.GameSourceFolders.Add(@"D:\MoreGames");

        await vm.SaveAndCloseCommand.ExecuteAsync(null);

        Assert.NotNull(received);
        Assert.True(received!.SourcePathsUpdated);
        Assert.True(received.PriceTiersUpdated);
        Assert.True(closeRequested);
        _settingsServiceMock.Verify(s => s.SaveSettingsAsync(It.Is<AppSettings>(a => a.PriceTier1 == 150 && a.GameSourceFolders.Count == 2)), Times.Once);
        _startupServiceMock.Verify(s => s.UpdateStartOnLogon(false), Times.Once);
    }

    [Fact]
    public async Task SaveCommand_WhenOnlyToggleChanges_BroadcastsFalseFlagsInSettingsChangedMessage()
    {
        using SettingsViewModel vm = CreateViewModel();
        vm.LoadSettings(new AppSettings
        {
            PriceTier1 = 100,
            PlayNotificationSounds = true
        });

        SettingsChangedMessage? received = null;
        _messenger.Register<SettingsViewModelTests, SettingsChangedMessage>(this, (_, msg) => received = msg);

        vm.PlayNotificationSounds = false;
        await vm.SaveAndCloseCommand.ExecuteAsync(null);

        Assert.NotNull(received);
        Assert.False(received!.SourcePathsUpdated);
        Assert.False(received.PriceTiersUpdated);
    }

    [Fact]
    public async Task SaveAndCloseCommand_WhenFolderInaccessibleAndCancelled_DoesNotSaveOrBroadcast()
    {
        using SettingsViewModel vm = CreateViewModel();
        vm.LoadSettings(new AppSettings
        {
            GameSourceFolders = [@"Z:\Missing"]
        });

        _folderValidatorMock
            .Setup(v => v.ValidateSourceFoldersAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync((IEnumerable<string> paths) =>
                paths.Select(p => new SourceFolder(p, false, DateTime.UtcNow)).ToList());

        _dialogServiceMock
            .Setup(d => d.ShowConfirmationDialogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        SettingsChangedMessage? received = null;
        _messenger.Register<SettingsViewModelTests, SettingsChangedMessage>(this, (_, msg) => received = msg);

        await vm.SaveAndCloseCommand.ExecuteAsync(null);

        Assert.Null(received);
        _settingsServiceMock.Verify(s => s.SaveSettingsAsync(It.IsAny<AppSettings>()), Times.Never);
    }

    [Fact]
    public void FindDuplicatesCommand_OpensDuplicateManagerWindowViaWindowService()
    {
        using SettingsViewModel vm = CreateViewModel();

        vm.FindDuplicatesCommand.Execute(null);

        _windowServiceMock.Verify(w => w.ShowWindow(AppWindowKind.DuplicateManager, null, null), Times.Once);
    }

    [Fact]
    public async Task SourceFolderValidator_DetectsExistingAndMissingFolders()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"EasyCopierValidator_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string missingDir = Path.Combine(Path.GetTempPath(), $"Missing_{Guid.NewGuid():N}");

        try
        {
            SourceFolderValidator validator = new(NullLogger<SourceFolderValidator>.Instance);
            IReadOnlyList<SourceFolder> results = await validator.ValidateSourceFoldersAsync([tempDir, missingDir]);

            Assert.Equal(2, results.Count);
            Assert.True(results[0].IsValid);
            Assert.False(results[1].IsValid);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}

