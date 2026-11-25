using EasyCopier.Shared.Platform;
using Moq;
using Xunit;

namespace EasyCopier.Tests.Shared.Platform;

public sealed class PickerServicesTests
{
    [Fact]
    public async Task FolderPickerContract_ReturnsSelectedPathAndHandlesCancellation()
    {
        var mockPicker = new Mock<IFolderPickerService>();
        mockPicker.Setup(p => p.PickFolderAsync(@"E:\")).ReturnsAsync(@"E:\Games");
        mockPicker.Setup(p => p.PickFolderAsync(null)).ReturnsAsync((string?)null);

        Assert.Equal(@"E:\Games", await mockPicker.Object.PickFolderAsync(@"E:\"));
        Assert.Null(await mockPicker.Object.PickFolderAsync());
    }

    [Fact]
    public async Task FilePickerContract_SupportsOpenAndSaveContracts()
    {
        var mockPicker = new Mock<IFilePickerService>();
        mockPicker.Setup(p => p.PickOpenFileAsync(It.Is<IList<string>>(f => f.Contains(".exe"))))
            .ReturnsAsync(@"C:\Tools\rufus.exe");
        mockPicker.Setup(p => p.PickSaveFileAsync("report.csv", It.IsAny<IDictionary<string, IList<string>>>()))
            .ReturnsAsync(@"C:\Exports\report.csv");

        Assert.Equal(@"C:\Tools\rufus.exe", await mockPicker.Object.PickOpenFileAsync([".exe"]));
        Assert.Equal(
            @"C:\Exports\report.csv",
            await mockPicker.Object.PickSaveFileAsync("report.csv", new Dictionary<string, IList<string>> { ["CSV"] = [".csv"] }));
    }

    [Theory]
    [InlineData(SettingsOpenAction.AddGameFolder, "Game")]
    [InlineData(SettingsOpenAction.AddAppFolder, "App")]
    [InlineData(SettingsOpenAction.AddTvAndFilmFolder, "TvAndFilm")]
    [InlineData(SettingsOpenAction.AddOsImageFolder, "OsImage")]
    [InlineData(SettingsOpenAction.None, null)]
    public void SettingsOpenAction_MapsToExpectedCategoryString(SettingsOpenAction action, string? expected)
    {
        Assert.Equal(expected, action.ToFolderCategoryString());
    }

    [Fact]
    public void NullTransferActivityMonitor_DefaultsToIdleState()
    {
        var monitor = new NullTransferActivityMonitor();

        Assert.False(monitor.HasActiveTransfers);
        Assert.Equal(0, monitor.ActiveTransferCount);
        Assert.Contains("Idle", monitor.GetTooltipSummary(), StringComparison.Ordinal);
        monitor.CancelAllActiveTransfers();
    }
}

