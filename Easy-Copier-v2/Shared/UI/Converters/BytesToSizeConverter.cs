using EasyCopier.Shared.UI.Helpers;
using Microsoft.UI.Xaml.Data;

namespace EasyCopier.Shared.UI.Converters;

/// <summary>
/// Converts a byte count (<see cref="long"/>) into a human-readable file size string (e.g., "1.5 GB").
/// </summary>
public class BytesToSizeConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is long bytes ? FormattingHelpers.FormatBytes(bytes) : "0 B";

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

