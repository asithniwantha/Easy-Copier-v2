using Windows.Foundation;

namespace EasyCopier.Features.Shell.Contracts;

/// <summary>
/// Defines methods for presenting item detail flyouts and handling folder launch actions in library views.
/// </summary>
public interface IFlyoutService
{
    /// <summary>Handles button click events to open an item's folder in File Explorer.</summary>
    void HandleOpenFolderClick(object sender);

    /// <summary>Displays the category-appropriate details flyout asynchronously for <paramref name="entry"/>.</summary>
    Task ShowDetailsFlyoutAsync(object sender, Point? position, GameEntry? entry = null);
}

