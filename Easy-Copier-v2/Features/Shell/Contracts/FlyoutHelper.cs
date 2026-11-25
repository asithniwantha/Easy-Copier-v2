using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;

namespace EasyCopier.Features.Shell.Contracts;

/// <summary>
/// Encapsulates shared flyout and card interaction behaviors for library item views, delegating to <see cref="IFlyoutService"/>.
/// </summary>
public static class FlyoutHelper
{
    private static IFlyoutService? s_flyoutService;

    /// <summary>Configures the static helper with an <see cref="IFlyoutService"/> instance.</summary>
    public static void Initialize(IFlyoutService flyoutService) => s_flyoutService = flyoutService;

    /// <summary>Handles the click event for opening an item's folder in File Explorer.</summary>
    public static void HandleOpenFolderClick(object sender) => s_flyoutService?.HandleOpenFolderClick(sender);

    /// <summary>Displays the appropriate item details flyout based on the entry's category.</summary>
    public static async Task ShowDetailsFlyoutAsync(object sender, RightTappedRoutedEventArgs e, GameEntry? entry = null)
    {
        entry ??= (sender as FrameworkElement)?.DataContext as GameEntry;
        if (entry == null)
        {
            return;
        }

        if (sender is FrameworkElement fe && fe.DataContext == null)
        {
            fe.DataContext = entry;
        }

        Point? position = sender is FrameworkElement target && e != null ? e.GetPosition(target) : null;
        if (s_flyoutService != null)
        {
            await s_flyoutService.ShowDetailsFlyoutAsync(sender, position, entry);
        }
    }
}

