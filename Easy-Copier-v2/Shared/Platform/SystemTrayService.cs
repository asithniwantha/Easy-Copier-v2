using System.Runtime.InteropServices;
using EasyCopier.Shared.Interop;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace EasyCopier.Shared.Platform;

/// <summary>
/// Implements system tray integration using Win32 <c>Shell_NotifyIcon</c> and window subclassing,
/// enabling background transfer resilience, interactive tray menu, and live progress tooltips.
/// </summary>
public sealed class SystemTrayService : ISystemTrayService
{
    private const nuint SUBCLASS_ID = 4040;
    private const uint MENU_OPEN_ID = 5001;
    private const uint MENU_CANCEL_ID = 5002;
    private const uint MENU_EXIT_ID = 5003;

    private readonly ITransferActivityMonitor _transferActivityMonitor;
    private readonly IDispatcherService _dispatcherService;
    private readonly ILogger<SystemTrayService> _logger;

    private IntPtr _hWnd = IntPtr.Zero;
    private IntPtr _hIcon = IntPtr.Zero;
    private bool _isSubclassed;
    private bool _disposed;
    private SystemTrayInterop.SUBCLASSPROC? _subclassProc;

    /// <inheritdoc />
    public bool IsTrayIconVisible { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemTrayService"/> class.
    /// </summary>
    public SystemTrayService(
        ITransferActivityMonitor transferActivityMonitor,
        IDispatcherService dispatcherService,
        ILogger<SystemTrayService> logger)
    {
        _transferActivityMonitor = transferActivityMonitor ?? throw new ArgumentNullException(nameof(transferActivityMonitor));
        _dispatcherService = dispatcherService ?? throw new ArgumentNullException(nameof(dispatcherService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public void Initialize(Window mainWindow)
    {
        ArgumentNullException.ThrowIfNull(mainWindow);

        try
        {
            _hWnd = WindowNative.GetWindowHandle(mainWindow);
            if (_hWnd == IntPtr.Zero)
            {
                _logger.LogWarning("Failed to acquire HWND from main window for SystemTrayService.");
                return;
            }

            LoadAppIcon();

            _subclassProc = WindowSubclassProc;
            _isSubclassed = SystemTrayInterop.SetWindowSubclass(_hWnd, _subclassProc, SUBCLASS_ID, 0);
            _transferActivityMonitor.ActivityChanged += OnTransferActivityChanged;

            _logger.LogInformation("SystemTrayService initialized successfully for HWND 0x{Hwnd:X8}.", _hWnd.ToInt64());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize SystemTrayService.");
        }
    }

    /// <inheritdoc />
    public void ShowTrayIcon()
    {
        if (_hWnd == IntPtr.Zero || _hIcon == IntPtr.Zero)
        {
            return;
        }

        try
        {
            SystemTrayInterop.NOTIFYICONDATA nid = CreateNotifyIconData();
            nid.uFlags = SystemTrayInterop.NIF_MESSAGE | SystemTrayInterop.NIF_ICON | SystemTrayInterop.NIF_TIP;
            nid.szTip = GetCurrentTooltipText();

            if (!IsTrayIconVisible)
            {
                bool result = SystemTrayInterop.Shell_NotifyIcon(SystemTrayInterop.NIM_ADD, ref nid);
                if (result)
                {
                    IsTrayIconVisible = true;
                    nid.uTimeoutOrVersion = 4;
                    _ = SystemTrayInterop.Shell_NotifyIcon(SystemTrayInterop.NIM_SETVERSION, ref nid);
                }
                else
                {
                    _logger.LogWarning("Shell_NotifyIcon(NIM_ADD) returned false.");
                }
            }
            else
            {
                _ = SystemTrayInterop.Shell_NotifyIcon(SystemTrayInterop.NIM_MODIFY, ref nid);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while displaying system tray icon.");
        }
    }

    /// <inheritdoc />
    public void RemoveTrayIcon()
    {
        if (!IsTrayIconVisible || _hWnd == IntPtr.Zero)
        {
            return;
        }

        try
        {
            SystemTrayInterop.NOTIFYICONDATA nid = CreateNotifyIconData();
            _ = SystemTrayInterop.Shell_NotifyIcon(SystemTrayInterop.NIM_DELETE, ref nid);
            IsTrayIconVisible = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing system tray icon.");
        }
    }

    /// <inheritdoc />
    public void ShowNotification(string title, string message)
    {
        if (_hWnd == IntPtr.Zero)
        {
            return;
        }

        if (!IsTrayIconVisible)
        {
            ShowTrayIcon();
        }

        try
        {
            SystemTrayInterop.NOTIFYICONDATA nid = CreateNotifyIconData();
            nid.uFlags = SystemTrayInterop.NIF_INFO;
            nid.szInfoTitle = title ?? "Easy Copier";
            nid.szInfo = message ?? string.Empty;
            nid.dwInfoFlags = SystemTrayInterop.NIIF_INFO;

            _ = SystemTrayInterop.Shell_NotifyIcon(SystemTrayInterop.NIM_MODIFY, ref nid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error displaying tray balloon notification.");
        }
    }

    /// <inheritdoc />
    public void MinimizeToTray()
    {
        if (_hWnd == IntPtr.Zero)
        {
            return;
        }

        _ = _dispatcherService.TryEnqueue(() =>
        {
            ShowTrayIcon();
            _ = SystemTrayInterop.ShowWindow(_hWnd, SystemTrayInterop.SW_HIDE);

            int activeCount = _transferActivityMonitor.ActiveTransferCount;
            if (activeCount > 0)
            {
                ShowNotification(
                    "Easy Copier",
                    $"{activeCount} transfer(s) are running in the background. Click the icon to restore.");
            }
        });
    }

    /// <inheritdoc />
    public void RestoreMainWindow()
    {
        if (_hWnd == IntPtr.Zero)
        {
            return;
        }

        _ = _dispatcherService.TryEnqueue(() =>
        {
            _ = SystemTrayInterop.ShowWindow(_hWnd, SystemTrayInterop.SW_RESTORE);
            _ = SystemTrayInterop.SetForegroundWindow(_hWnd);
        });
    }

    private nint WindowSubclassProc(IntPtr hWnd, uint uMsg, nuint wParam, nint lParam, nuint uIdSubclass, nuint dwRefData)
    {
        if (uMsg == SystemTrayInterop.WM_TRAYICON)
        {
            uint mouseMsg = (uint)(lParam.ToInt64() & 0xFFFF);

            if (mouseMsg == SystemTrayInterop.WM_LBUTTONUP)
            {
                RestoreMainWindow();
                return 0;
            }

            if (mouseMsg is SystemTrayInterop.WM_RBUTTONUP or SystemTrayInterop.WM_CONTEXTMENU)
            {
                ShowContextMenu();
                return 0;
            }
        }

        return SystemTrayInterop.DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        if (_hWnd == IntPtr.Zero)
        {
            return;
        }

        IntPtr hMenu = SystemTrayInterop.CreatePopupMenu();
        if (hMenu == IntPtr.Zero)
        {
            return;
        }

        try
        {
            bool hasActiveTransfers = _transferActivityMonitor.HasActiveTransfers;

            _ = SystemTrayInterop.AppendMenu(hMenu, SystemTrayInterop.MF_STRING | SystemTrayInterop.MF_DEFAULT, MENU_OPEN_ID, "Open Easy Copier");
            _ = SystemTrayInterop.AppendMenu(hMenu, SystemTrayInterop.MF_SEPARATOR, 0, string.Empty);

            uint cancelFlags = hasActiveTransfers ? SystemTrayInterop.MF_STRING : (SystemTrayInterop.MF_STRING | SystemTrayInterop.MF_GRAYED);
            _ = SystemTrayInterop.AppendMenu(hMenu, cancelFlags, MENU_CANCEL_ID, "Cancel Active Transfers");
            _ = SystemTrayInterop.AppendMenu(hMenu, SystemTrayInterop.MF_SEPARATOR, 0, string.Empty);

            _ = SystemTrayInterop.AppendMenu(hMenu, SystemTrayInterop.MF_STRING, MENU_EXIT_ID, "Exit Easy Copier");

            if (!SystemTrayInterop.GetCursorPos(out SystemTrayInterop.POINT pt))
            {
                return;
            }

            _ = SystemTrayInterop.SetForegroundWindow(_hWnd);

            uint chosenCommand = SystemTrayInterop.TrackPopupMenuEx(
                hMenu,
                SystemTrayInterop.TPM_RETURNCMD | SystemTrayInterop.TPM_RIGHTBUTTON,
                pt.X,
                pt.Y,
                _hWnd,
                IntPtr.Zero);

            HandleMenuCommand(chosenCommand);
        }
        finally
        {
            _ = SystemTrayInterop.DestroyMenu(hMenu);
        }
    }

    private void HandleMenuCommand(uint commandId)
    {
        switch (commandId)
        {
            case MENU_OPEN_ID:
                RestoreMainWindow();
                break;

            case MENU_CANCEL_ID:
                _ = _dispatcherService.TryEnqueue(() => _transferActivityMonitor.CancelAllActiveTransfers());
                break;

            case MENU_EXIT_ID:
                _ = _dispatcherService.TryEnqueue(() =>
                {
                    RemoveTrayIcon();
                    App.Current.DisposeServices();
                    Application.Current.Exit();
                });
                break;
        }
    }

    private SystemTrayInterop.NOTIFYICONDATA CreateNotifyIconData()
    {
        return new SystemTrayInterop.NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf<SystemTrayInterop.NOTIFYICONDATA>(),
            hWnd = _hWnd,
            uID = 1,
            uCallbackMessage = SystemTrayInterop.WM_TRAYICON,
            hIcon = _hIcon,
            szTip = string.Empty,
            szInfo = string.Empty,
            szInfoTitle = string.Empty
        };
    }

    private void LoadAppIcon()
    {
        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (File.Exists(iconPath))
        {
            _hIcon = SystemTrayInterop.LoadImage(
                IntPtr.Zero,
                iconPath,
                SystemTrayInterop.IMAGE_ICON,
                0,
                0,
                SystemTrayInterop.LR_LOADFROMFILE | SystemTrayInterop.LR_DEFAULTSIZE);
        }
    }

    private string GetCurrentTooltipText()
    {
        string text = _transferActivityMonitor.GetTooltipSummary();
        return text.Length > 120 ? text[..120] + "..." : text;
    }

    private void OnTransferActivityChanged(object? sender, EventArgs e)
    {
        UpdateTrayProgress();
    }

    private void UpdateTrayProgress()
    {
        if (IsTrayIconVisible)
        {
            SystemTrayInterop.NOTIFYICONDATA nid = CreateNotifyIconData();
            nid.uFlags = SystemTrayInterop.NIF_TIP;
            nid.szTip = GetCurrentTooltipText();
            _ = SystemTrayInterop.Shell_NotifyIcon(SystemTrayInterop.NIM_MODIFY, ref nid);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        RemoveTrayIcon();

        if (_isSubclassed && _hWnd != IntPtr.Zero && _subclassProc != null)
        {
            _ = SystemTrayInterop.RemoveWindowSubclass(_hWnd, _subclassProc, SUBCLASS_ID);
            _isSubclassed = false;
        }

        _transferActivityMonitor.ActivityChanged -= OnTransferActivityChanged;

        if (_hIcon != IntPtr.Zero)
        {
            _ = SystemTrayInterop.DestroyIcon(_hIcon);
            _hIcon = IntPtr.Zero;
        }
    }
}

