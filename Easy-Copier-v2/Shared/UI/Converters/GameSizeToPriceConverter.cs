using EasyCopier.Shared.Configuration;
using EasyCopier.Shared.UI.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace EasyCopier.Shared.UI.Converters;

/// <summary>
/// Converts a byte size into a formatted price string based on user settings or size brackets.
/// </summary>
public class GameSizeToPriceConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is long bytes)
        {
            double gb = bytes / (1024.0 * 1024.0 * 1024.0);

            try
            {
                AppSettings settings = new();

                if (Application.Current is App app)
                {
                    ISettingsService settingsService = app.Services.GetRequiredService<ISettingsService>();
                    settings = settingsService.LoadSettingsSync();
                }

                return $"Rs. {FormattingHelpers.CalculatePrice(bytes, settings)}";
            }
            catch
            {
                return gb <= 5.0 ? "Rs. 100" : gb <= 10.0 ? "Rs. 200" : gb < 16.0 ? "Rs. 300" : "Rs. 400";
            }
        }

        return "Rs. -";
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

