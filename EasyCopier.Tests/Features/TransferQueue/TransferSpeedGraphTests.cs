using EasyCopier.Features.TransferQueue;
using EasyCopier.Features.TransferQueue.Models;
using Xunit;

namespace EasyCopier.Tests.Features.TransferQueue;

public sealed class TransferSpeedGraphTests
{
    private const long OneMegabyte = 1024 * 1024;

    [Fact]
    public void SlidingWindow_ReportsRecentRateRatherThanAverage()
    {
        SlidingWindowSpeedCalculator calculator = new(TimeSpan.FromSeconds(2));

        _ = calculator.AddSample(TimeSpan.Zero, 0);
        _ = calculator.AddSample(TimeSpan.FromSeconds(1), 100 * OneMegabyte);
        _ = calculator.AddSample(TimeSpan.FromSeconds(2), 200 * OneMegabyte);
        _ = calculator.AddSample(TimeSpan.FromSeconds(3), 200 * OneMegabyte + OneMegabyte);
        double rate = calculator.AddSample(TimeSpan.FromSeconds(4), 200 * OneMegabyte + 2 * OneMegabyte);

        // Last two seconds only moved 2 MB; the overall average would be ~50 MB/s.
        Assert.Equal(OneMegabyte, rate, 1);
    }

    [Fact]
    public void SlidingWindow_ReturnsZeroWhenNoTimeElapsed()
    {
        SlidingWindowSpeedCalculator calculator = new();

        Assert.Equal(0, calculator.AddSample(TimeSpan.Zero, 0));
    }

    [Fact]
    public void History_RateLimitsSamplesAndCapsCapacity()
    {
        TransferSpeedHistory history = new(capacity: 3, minSampleInterval: TimeSpan.FromMilliseconds(500));

        Assert.True(history.TryAdd(10, TimeSpan.Zero));
        Assert.False(history.TryAdd(20, TimeSpan.FromMilliseconds(100)));
        Assert.True(history.TryAdd(30, TimeSpan.FromMilliseconds(600)));
        Assert.True(history.TryAdd(5, TimeSpan.FromMilliseconds(1200)));
        Assert.True(history.TryAdd(7, TimeSpan.FromMilliseconds(1800)));

        Assert.Equal([30d, 5d, 7d], history.Snapshot());
        Assert.Equal(30, history.PeakMBps);
    }

    [Fact]
    public void History_ConvertsBytesPerSecondToMBps()
    {
        Assert.Equal(2.5, TransferSpeedHistory.ToMBps(2.5 * OneMegabyte), 6);
        Assert.Equal(0, TransferSpeedHistory.ToMBps(-1));
    }

    [Fact]
    public void QueueItem_RecordSpeedUpdatesCurrentSamplesAndPeak()
    {
        TransferQueueItem item = new([], new RemovableDrive(@"E:\", "USB", "NTFS", 0, 0, 0), @"E:\", 0);

        item.RecordSpeed(50 * OneMegabyte);

        Assert.Equal(50, item.CurrentSpeedMBps, 6);
        Assert.Equal(50, item.PeakSpeedMBps, 6);
        _ = Assert.Single(item.SpeedSamples);

        item.ResetSpeedHistory();

        Assert.Empty(item.SpeedSamples);
        Assert.Equal(0, item.CurrentSpeedMBps);
    }

    [Fact]
    public void Geometry_PlacesNewestSampleAtRightEdgeAndScalesToHeight()
    {
        IReadOnlyList<(double X, double Y)> points = SpeedGraphGeometry.BuildPoints([0, 10], 100, 50, 11, 10);

        Assert.Equal(2, points.Count);
        Assert.Equal(90, points[0].X, 6);
        Assert.Equal(50, points[0].Y, 6);
        Assert.Equal(100, points[1].X, 6);
        Assert.Equal(0, points[1].Y, 6);
    }

    [Fact]
    public void Geometry_ScaleHasMinimumAndHeadroom()
    {
        Assert.Equal(1.0, SpeedGraphGeometry.GetScaleMax([]));
        Assert.Equal(115, SpeedGraphGeometry.GetScaleMax([100]), 6);
    }
}
