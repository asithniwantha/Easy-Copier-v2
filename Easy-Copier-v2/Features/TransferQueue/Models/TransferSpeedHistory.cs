namespace EasyCopier.Features.TransferQueue.Models;

/// <summary>
/// Maintains a rolling, rate-limited history of transfer speed samples (in MB/s) used to render the speed graph.
/// </summary>
public sealed class TransferSpeedHistory(int capacity = TransferSpeedHistory.DefaultCapacity, TimeSpan? minSampleInterval = null)
{
    /// <summary>Default number of retained samples (about one minute at the default interval).</summary>
    public const int DefaultCapacity = 120;

    private const double BytesPerMegabyte = 1024.0 * 1024.0;

    private readonly TimeSpan _minSampleInterval = minSampleInterval ?? TimeSpan.FromMilliseconds(500);
    private readonly Queue<double> _samples = new();
    private TimeSpan? _lastSampleAt;

    /// <summary>Gets the maximum number of retained samples.</summary>
    public int Capacity { get; } = capacity > 0 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity));

    /// <summary>Gets the highest speed recorded, in MB/s.</summary>
    public double PeakMBps { get; private set; }

    /// <summary>Converts a rate in bytes per second to MB/s.</summary>
    public static double ToMBps(double bytesPerSecond) => Math.Max(0, bytesPerSecond) / BytesPerMegabyte;

    /// <summary>
    /// Adds a sample if at least the minimum interval has elapsed since the previous one.
    /// </summary>
    /// <returns><see langword="true"/> if the sample was recorded.</returns>
    public bool TryAdd(double mbps, TimeSpan timestamp)
    {
        if (_lastSampleAt is { } last && timestamp - last < _minSampleInterval)
        {
            return false;
        }

        _lastSampleAt = timestamp;
        double value = Math.Max(0, mbps);
        _samples.Enqueue(value);

        while (_samples.Count > Capacity)
        {
            _ = _samples.Dequeue();
        }

        PeakMBps = Math.Max(PeakMBps, value);
        return true;
    }

    /// <summary>Returns an immutable copy of the current samples, oldest first.</summary>
    public IReadOnlyList<double> Snapshot() => _samples.ToArray();

    /// <summary>Clears all samples and the peak value.</summary>
    public void Clear()
    {
        _samples.Clear();
        _lastSampleAt = null;
        PeakMBps = 0;
    }
}
