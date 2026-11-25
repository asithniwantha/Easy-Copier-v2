using EasyCopier.Shared.Configuration;
using EasyCopier.Shared.UI.Helpers;
using Xunit;

namespace EasyCopier.Tests.Shared.UI;

public sealed class FormattingHelpersTests
{
    private const long Gibibyte = 1024L * 1024L * 1024L;

    [Theory]
    [InlineData(4, 100)]
    [InlineData(5, 100)]
    [InlineData(8, 200)]
    [InlineData(10, 200)]
    [InlineData(12, 300)]
    [InlineData(16, 400)]
    public void CalculatePrice_MapsSizeBucketsToTiers(int gigabytes, int expectedPrice)
    {
        var settings = new AppSettings();
        int price = FormattingHelpers.CalculatePrice(gigabytes * Gibibyte, settings);
        Assert.Equal(expectedPrice, price);
    }

    [Fact]
    public void FormatBytes_FormatsHumanReadableUnits()
    {
        Assert.Equal("512 B", FormattingHelpers.FormatBytes(512));
        Assert.Equal("1 KB", FormattingHelpers.FormatBytes(1024));
        Assert.Equal("2.5 GB", FormattingHelpers.FormatBytes((long)(2.5 * Gibibyte)));
    }
}

