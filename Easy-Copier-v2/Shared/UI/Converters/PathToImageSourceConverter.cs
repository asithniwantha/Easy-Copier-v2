using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;

namespace EasyCopier.Shared.UI.Converters;

/// <summary>
/// Converts a local file system path string to a <see cref="BitmapImage"/> for UI image bindings.
/// </summary>
public class PathToImageSourceConverter : IValueConverter
{
    /// <inheritdoc />
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string path && !string.IsNullOrEmpty(path))
        {
            try
            {
                if (File.Exists(path) && Uri.TryCreate(path, UriKind.Absolute, out Uri? uri))
                {
                    return new BitmapImage(uri)
                    {
                        DecodePixelHeight = 280,
                        DecodePixelType = DecodePixelType.Logical
                    };
                }
            }
            catch
            {
                // Malformed path/URI - fall through and return null so no image is shown.
            }
        }

        return null;
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

