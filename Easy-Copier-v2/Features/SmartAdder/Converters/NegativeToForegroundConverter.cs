using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace EasyCopier.Features.SmartAdder.Converters;

/// <summary>
/// Converts a boolean indicating negative value status to a red text foreground brush.
/// </summary>
public sealed class NegativeToForegroundConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isNegative && isNegative)
        {
            return new SolidColorBrush(Colors.Red);
        }

        return DependencyProperty.UnsetValue;
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

