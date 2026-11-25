using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace EasyCopier.Features.Shell.Contracts;

/// <summary>
/// Converts a <see cref="LibraryCategory"/> enum value to a UI <see cref="Visibility"/> status,
/// returning <see cref="Visibility.Visible"/> if the category is <see cref="LibraryCategory.Game"/>.
/// </summary>
public class GameCategoryToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is LibraryCategory category && category == LibraryCategory.Game
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

