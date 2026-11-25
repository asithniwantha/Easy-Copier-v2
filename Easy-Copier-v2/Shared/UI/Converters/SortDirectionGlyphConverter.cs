using Microsoft.UI.Xaml.Data;

namespace EasyCopier.Shared.UI.Converters;

/// <summary>
/// Converts a boolean indicating ascending sort direction to a Segoe MDL2 Assets glyph character string for sort direction indicators.
/// </summary>
public class SortDirectionGlyphConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is bool isAscending && isAscending ? "\uE70E" : "\uE70D";

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

