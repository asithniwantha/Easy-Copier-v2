using Microsoft.UI;

namespace EasyCopier.Shared.Platform;

/// <summary>
/// Defines a contract for accessing window handles, <see cref="WindowId"/>, and XAML root context without leaking raw WinUI controls.
/// </summary>
public interface IAppWindowContext
{
    /// <summary>
    /// Gets the primary application window reference.
    /// </summary>
    object? MainWindow { get; }

    /// <summary>
    /// Gets the primary XAML root object for displaying content dialogs and overlays.
    /// </summary>
    object? MainXamlRoot { get; }

    /// <summary>
    /// Gets the WinUI <see cref="WindowId"/> of the primary application window, or default if unavailable.
    /// </summary>
    WindowId MainWindowId { get; }

    /// <summary>
    /// Gets the active process <see cref="WindowId"/>, falling back to <see cref="MainWindowId"/> when elevated or inactive.
    /// </summary>
    WindowId ActiveWindowId { get; }
}
