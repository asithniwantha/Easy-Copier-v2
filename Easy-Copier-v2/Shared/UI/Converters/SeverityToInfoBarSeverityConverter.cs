using EasyCopier.Shared.Models;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

namespace EasyCopier.Shared.UI.Converters;

/// <summary>
/// Converts a domain <see cref="ValidationSeverity"/> enum value to a WinUI <see cref="InfoBarSeverity"/> value.
/// </summary>
public class SeverityToInfoBarSeverityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is ValidationSeverity severity
            ? severity switch
            {
                ValidationSeverity.Success => InfoBarSeverity.Success,
                ValidationSeverity.Error => InfoBarSeverity.Error,
                ValidationSeverity.Warning => InfoBarSeverity.Warning,
                ValidationSeverity.Info => InfoBarSeverity.Informational,
                _ => InfoBarSeverity.Informational
            }
            : InfoBarSeverity.Informational;
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

