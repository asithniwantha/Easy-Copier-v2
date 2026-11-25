using Microsoft.UI.Xaml;

namespace EasyCopier.Shared.Platform;

/// <summary>
/// Defines services for managing the application notification icon in the Windows taskbar system tray.
/// </summary>
public interface ISystemTrayService : IDisposable
{
    void Initialize(Window mainWindow);
    void ShowTrayIcon();
    void RemoveTrayIcon();
    void ShowNotification(string title, string message);
    void MinimizeToTray();
    void RestoreMainWindow();
    bool IsTrayIconVisible { get; }
}

