using Microsoft.UI.Xaml.Data;

namespace EasyCopier.Shared.UI.Converters;

/// <summary>
/// Converts a boolean indicating ascending sort direction to a descriptive tooltip string ("Ascending" or "Descending").
/// </summary>
public class SortDirectionToolTipConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is bool isAscending && isAscending ? "Ascending" : "Descending";

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

