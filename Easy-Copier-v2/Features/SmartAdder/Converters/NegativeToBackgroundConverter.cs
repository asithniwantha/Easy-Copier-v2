using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace EasyCopier.Features.SmartAdder.Converters;

/// <summary>
/// Converts a boolean indicating negative value status to a subtle red background brush adapted to light/dark theme.
/// </summary>
public sealed class NegativeToBackgroundConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isNegative && isNegative)
        {
            bool isDarkMode = Application.Current.RequestedTheme == ApplicationTheme.Dark;
            return isDarkMode
                ? new SolidColorBrush(ColorHelper.FromArgb(50, 255, 0, 0))
                : new SolidColorBrush(ColorHelper.FromArgb(30, 255, 0, 0));
        }

        return DependencyProperty.UnsetValue;
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

