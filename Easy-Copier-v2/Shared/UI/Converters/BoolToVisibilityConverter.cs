using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace EasyCopier.Shared.UI.Converters;

/// <summary>
/// Converts a boolean value to a UI <see cref="Visibility"/> status, supporting optional inversion via the converter parameter ("Invert").
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool invert = parameter?.ToString() == "Invert";
        bool boolValue = value is bool b && b;

        if (invert)
        {
            boolValue = !boolValue;
        }

        return boolValue ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

