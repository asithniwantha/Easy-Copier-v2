using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace EasyCopier.Features.History;

/// <summary>
/// Converts <c>IsCompactDensity</c> boolean state into <see cref="Thickness"/> row padding for the History workspace table.
/// </summary>
public sealed class DensityToPaddingConverter : IValueConverter
{
    /// <summary>Gets or sets the compact density row padding.</summary>
    public Thickness CompactPadding { get; set; } = new(12, 6, 12, 6);

    /// <summary>Gets or sets the comfortable density row padding.</summary>
    public Thickness ComfortablePadding { get; set; } = new(16, 12, 16, 12);

    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is bool compact && compact ? CompactPadding : ComfortablePadding;

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}
