using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace EasyCopier.Features.TransferQueue;

/// <summary>
/// Lightweight line/area graph that plots transfer speed samples (MB/s) over time.
/// </summary>
public sealed partial class SpeedGraphControl : UserControl
{
    /// <summary>Identifies the <see cref="Samples"/> dependency property.</summary>
    public static readonly DependencyProperty SamplesProperty = DependencyProperty.Register(
        nameof(Samples),
        typeof(IReadOnlyList<double>),
        typeof(SpeedGraphControl),
        new PropertyMetadata(null, OnSamplesChanged));

    /// <summary>Initializes a new instance of the <see cref="SpeedGraphControl"/> class.</summary>
    public SpeedGraphControl()
    {
        InitializeComponent();
    }

    /// <summary>Gets or sets the speed samples in MB/s, oldest first.</summary>
    public IReadOnlyList<double>? Samples
    {
        get => (IReadOnlyList<double>?)GetValue(SamplesProperty);
        set => SetValue(SamplesProperty, value);
    }

    private static void OnSamplesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((SpeedGraphControl)d).Redraw();

    private void GraphHost_SizeChanged(object sender, SizeChangedEventArgs e) => Redraw();

    private void Redraw()
    {
        IReadOnlyList<double> samples = Samples ?? [];
        double scaleMax = SpeedGraphGeometry.GetScaleMax(samples);
        ScaleLabel.Text = string.Create(CultureInfo.CurrentCulture, $"{scaleMax:0.#} MB/s");

        double width = GraphHost.ActualWidth;
        double height = GraphHost.ActualHeight;

        IReadOnlyList<(double X, double Y)> points = SpeedGraphGeometry.BuildPoints(
            samples, width, height, Models.TransferSpeedHistory.DefaultCapacity, scaleMax);

        PointCollection line = [];
        PointCollection area = [];

        foreach ((double x, double y) in points)
        {
            line.Add(new Point(x, y));
            area.Add(new Point(x, y));
        }

        if (points.Count > 0)
        {
            area.Add(new Point(points[^1].X, height));
            area.Add(new Point(points[0].X, height));
        }

        LineShape.Points = line;
        AreaShape.Points = area;
    }
}
