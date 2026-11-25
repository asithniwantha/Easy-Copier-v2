using Microsoft.UI.Xaml.Data;

namespace EasyCopier.Features.Shell.Contracts;

/// <summary>
/// Converts a list of <see cref="GameCategory"/> values into a comma-separated display string.
/// </summary>
public class CategoryFormatConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is IReadOnlyList<GameCategory> categories && categories.Count > 0
            ? string.Join(", ", categories.Select(c => c.ToString()))
            : "Uncategorized";
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

