using EasyCopier.Shared.Configuration;
using EasyCopier.Shared.Interop;
using EasyCopier.Shared.Platform;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace EasyCopier.Features.Shell;

/// <summary>
/// Primary top-level application window hosting <see cref="MainPage"/> and managing system tray minimize-on-close behavior.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly ISystemTrayService _systemTrayService;
    private readonly ITransferActivityMonitor _transferActivityMonitor;
    private readonly ISettingsService _settingsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    public MainWindow(
        ShellViewModel viewModel,
        ISystemTrayService systemTrayService,
        ITransferActivityMonitor transferActivityMonitor,
        ISettingsService settingsService)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _systemTrayService = systemTrayService ?? throw new ArgumentNullException(nameof(systemTrayService));
        _transferActivityMonitor = transferActivityMonitor ?? throw new ArgumentNullException(nameof(transferActivityMonitor));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        NativeWindowHelper.InitializeWindow(this, 1400, 900);
        NativeWindowHelper.SetMinimumSize(this, 960, 640);

        _systemTrayService.Initialize(this);
        ConfigureWindowLifecycle();

        _ = RootFrame.Navigate(typeof(MainPage), viewModel);
    }

    private void ConfigureWindowLifecycle()
    {
        nint hwnd = WindowNative.GetWindowHandle(this);
        WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

        if (appWindow is not null)
        {
            appWindow.Closing += (_, args) =>
            {
                AppSettings settings = _settingsService.LoadSettingsSync();
                if (_transferActivityMonitor.HasActiveTransfers && settings.MinimizeToTrayOnClose)
                {
                    args.Cancel = true;
                    _systemTrayService.MinimizeToTray();
                }
                else
                {
                    _systemTrayService.RemoveTrayIcon();
                }
            };
        }
    }
}
