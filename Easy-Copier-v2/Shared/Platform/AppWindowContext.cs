using Microsoft.UI.Xaml;

namespace EasyCopier.Shared.Platform;

/// <summary>
/// Provides access to the main application window context and XAML root element.
/// </summary>
public class AppWindowContext : IAppWindowContext
{
    /// <summary>
    /// Gets the current main application window object.
    /// </summary>
    public object? MainWindow => App.Current.MainWindow;

    /// <summary>
    /// Gets the main XAML root element of the main window content, if available.
    /// </summary>
    public object? MainXamlRoot => App.Current.MainWindow?.Content is FrameworkElement rootElement
        ? rootElement.XamlRoot
        : null;
}

