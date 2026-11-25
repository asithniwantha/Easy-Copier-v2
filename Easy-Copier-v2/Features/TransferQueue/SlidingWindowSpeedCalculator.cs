namespace EasyCopier.Features.TransferQueue;

/// <summary>
/// Calculates the current transfer rate (bytes per second) over a short sliding time window,
/// so the value reflects recent throughput rather than the average since the transfer started.
/// </summary>
public sealed class SlidingWindowSpeedCalculator(TimeSpan window)
{
    private readonly List<(TimeSpan Time, long Bytes)> _samples = [];

    /// <summary>Initializes a calculator with a default two-second window.</summary>
    public SlidingWindowSpeedCalculator()
        : this(TimeSpan.FromSeconds(2))
    {
    }

    /// <summary>
    /// Records the cumulative bytes transferred at the given elapsed time and returns the current rate in bytes per second.
    /// </summary>
    public double AddSample(TimeSpan elapsed, long totalBytesTransferred)
    {
        _samples.Add((elapsed, totalBytesTransferred));

        // Keep exactly one sample at or before the window start so the span always covers the full window.
        while (_samples.Count > 2 && elapsed - _samples[1].Time >= window)
        {
            _samples.RemoveAt(0);
        }

        (TimeSpan Time, long Bytes) oldest = _samples[0];
        double seconds = (elapsed - oldest.Time).TotalSeconds;

        if (seconds <= 0)
        {
            return 0;
        }

        return Math.Max(0, (totalBytesTransferred - oldest.Bytes) / seconds);
    }

    /// <summary>Clears all recorded samples.</summary>
    public void Reset() => _samples.Clear();
}
