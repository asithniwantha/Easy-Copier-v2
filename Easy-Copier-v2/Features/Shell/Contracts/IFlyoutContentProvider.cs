using Microsoft.UI.Xaml;

namespace EasyCopier.Features.Shell.Contracts;

/// <summary>
/// Slice-contributed provider that constructs a category-specific details flyout content UI element for a <see cref="GameEntry"/>.
/// </summary>
public interface IFlyoutContentProvider
{
    /// <summary>Gets whether this provider builds flyouts for the specified <paramref name="category"/>.</summary>
    bool CanHandle(LibraryCategory category);

    /// <summary>Creates the flyout content element and optional presenter style for <paramref name="entry"/>.</summary>
    Task<(UIElement Content, Style? PresenterStyle)?> CreateContentAsync(GameEntry entry);
}

