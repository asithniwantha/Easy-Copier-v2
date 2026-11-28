namespace EasyCopier.Features.TransferQueue;

/// <summary>
/// Pure geometry helpers that map speed samples to graph coordinates (kept free of WinUI types for headless testing).
/// </summary>
public static class SpeedGraphGeometry
{
    private const double MinimumScaleMax = 1.0;
    private const double Headroom = 1.15;

    /// <summary>Gets the upper bound of the Y axis (MB/s) for the given samples.</summary>
    public static double GetScaleMax(IReadOnlyList<double> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);

        double max = 0;

        for (int i = 0; i < samples.Count; i++)
        {
            max = Math.Max(max, samples[i]);
        }

        return Math.Max(MinimumScaleMax, max * Headroom);
    }

    /// <summary>
    /// Maps samples to points inside a <paramref name="width"/> x <paramref name="height"/> area.
    /// The newest sample is placed at the right edge and the oldest slots scroll off to the left.
    /// </summary>
    public static IReadOnlyList<(double X, double Y)> BuildPoints(
        IReadOnlyList<double> samples,
        double width,
        double height,
        int capacity,
        double scaleMax)
    {
        ArgumentNullException.ThrowIfNull(samples);

        if (samples.Count == 0 || width <= 0 || height <= 0 || capacity < 2 || scaleMax <= 0)
        {
            return [];
        }

        double step = width / (capacity - 1);
        int count = Math.Min(samples.Count, capacity);
        int start = samples.Count - count;
        List<(double X, double Y)> points = new(count);

        for (int i = 0; i < count; i++)
        {
            double x = width - ((count - 1 - i) * step);
            double ratio = Math.Clamp(samples[start + i] / scaleMax, 0, 1);
            points.Add((x, height - (ratio * height)));
        }

        return points;
    }
}
