using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace EasyCopier.Shared.UI.Converters;

/// <summary>
/// Converts an object reference (null vs non-null) to a UI <see cref="Visibility"/> status, supporting optional inversion via the converter parameter ("Invert").
/// </summary>
public class NullToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool isNull = value == null;
        if (parameter is string param && param.Equals("Invert", StringComparison.OrdinalIgnoreCase))
        {
            isNull = !isNull;
        }

        return isNull ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

