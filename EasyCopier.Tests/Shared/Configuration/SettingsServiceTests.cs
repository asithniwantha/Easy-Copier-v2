using EasyCopier.Shared.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EasyCopier.Tests.Shared.Configuration;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"EasyCopierTests_{Guid.NewGuid():N}");

    [Fact]
    public async Task SaveAndLoadSettings_RoundTripsValues()
    {
        string settingsPath = Path.Combine(_tempDir, "appsettings.json");
        var service = new SettingsService(NullLogger<SettingsService>.Instance, settingsPath);

        var settings = new AppSettings
        {
            PriceTier1 = 150,
            PriceTier4 = 550,
            GameSourceFolders = [@"D:\Games"]
        };

        await service.SaveSettingsAsync(settings);

        var freshReader = new SettingsService(NullLogger<SettingsService>.Instance, settingsPath);
        AppSettings loaded = await freshReader.LoadSettingsAsync();

        Assert.Equal(150, loaded.PriceTier1);
        Assert.Equal(550, loaded.PriceTier4);
        Assert.Equal([@"D:\Games"], loaded.GameSourceFolders);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}

