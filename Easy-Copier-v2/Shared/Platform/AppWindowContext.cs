using EasyCopier.Shared.Interop;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace EasyCopier.Shared.Platform;

/// <summary>
/// Provides access to the main application window context, active <see cref="WindowId"/>, and XAML root element.
/// </summary>
public class AppWindowContext : IAppWindowContext
{
    /// <inheritdoc />
    public object? MainWindow => Application.Current is App app ? app.MainWindow : null;

    /// <inheritdoc />
    public object? MainXamlRoot => MainWindow is Window { Content: FrameworkElement rootElement }
        ? rootElement.XamlRoot
        : null;

    /// <inheritdoc />
    public WindowId MainWindowId
    {
        get
        {
            if (MainWindow is not Window mainWindow)
            {
                return default;
            }

            nint hwnd = WindowNative.GetWindowHandle(mainWindow);
            return hwnd == IntPtr.Zero ? default : Win32Interop.GetWindowIdFromWindow(hwnd);
        }
    }

    /// <inheritdoc />
    public WindowId ActiveWindowId
    {
        get
        {
            nint hwnd = NativeWindowHelper.GetActiveWindowHandle();
            return hwnd != IntPtr.Zero
                ? Win32Interop.GetWindowIdFromWindow(hwnd)
                : MainWindowId;
        }
    }
}
