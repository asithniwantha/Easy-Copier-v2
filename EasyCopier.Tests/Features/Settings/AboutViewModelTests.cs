using EasyCopier.Features.Settings.About;
using EasyCopier.Shared.Platform;
using Moq;
using Xunit;

namespace EasyCopier.Tests.Features.Settings;

public sealed class AboutViewModelTests
{
    [Fact]
    public void Commands_OpenExpectedLinksAndCopyVersionInfo()
    {
        Mock<IProcessService> processMock = new();
        Mock<IClipboardService> clipboardMock = new();

        AboutViewModel vm = new(processMock.Object, clipboardMock.Object);

        vm.OpenGitHubRepoCommand.Execute(null);
        vm.OpenGitHubIssuesCommand.Execute(null);
        vm.CopyVersionInfoCommand.Execute(null);

        processMock.Verify(p => p.OpenInExplorer("https://github.com/asithniwantha/Easy-Copier-v2"), Times.Once);
        processMock.Verify(p => p.OpenInExplorer("https://github.com/asithniwantha/Easy-Copier-v2/issues"), Times.Once);
        clipboardMock.Verify(c => c.SetText(It.Is<string>(s => s.Contains("Easy Copier v", StringComparison.Ordinal))), Times.Once);
        Assert.True(vm.IsCopied);
        Assert.Equal("Asith Niwantha", vm.DeveloperInfo);
        Assert.False(string.IsNullOrWhiteSpace(vm.AppVersion));
    }
}

