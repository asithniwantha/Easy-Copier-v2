using EasyCopier.Shared.Interop;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.Windows.Storage.Pickers;
using WinRT.Interop;

namespace EasyCopier.Shared.Platform;

/// <summary>
/// Implements folder selection operations using WinRT <see cref="FolderPicker"/>.
/// </summary>
public class FolderPickerService : IFolderPickerService
{
    private readonly IDispatcherService _dispatcherService;
    private readonly IAppWindowContext _appWindowContext;
    private readonly ILogger<FolderPickerService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FolderPickerService"/> class.
    /// </summary>
    public FolderPickerService(
        IDispatcherService dispatcherService,
        IAppWindowContext appWindowContext,
        ILogger<FolderPickerService>? logger = null)
    {
        _dispatcherService = dispatcherService ?? throw new ArgumentNullException(nameof(dispatcherService));
        _appWindowContext = appWindowContext ?? throw new ArgumentNullException(nameof(appWindowContext));
        _logger = logger ?? NullLogger<FolderPickerService>.Instance;
    }

    /// <summary>
    /// Displays a folder picker modal dialog allowing the user to select a target directory.
    /// </summary>
    public async Task<string?> PickFolderAsync(string? startingDirectory = null)
    {
        TaskCompletionSource<string?> tcs = new();

        bool enqueued = _dispatcherService.TryEnqueue(async () =>
        {
            try
            {
                nint windowHandle = NativeWindowHelper.GetActiveWindowHandle();
                if (windowHandle == IntPtr.Zero)
                {
                    _logger.LogWarning("NativeWindowHelper.GetActiveWindowHandle() returned IntPtr.Zero. Falling back to MainWindow handle.");
                    if (_appWindowContext.MainWindow is Window mainWindow)
                    {
                        windowHandle = WindowNative.GetWindowHandle(mainWindow);
                    }
                }

                if (windowHandle == IntPtr.Zero)
                {
                    _logger.LogError("Unable to retrieve a valid window handle for FolderPicker.");
                    tcs.SetResult(null);
                    return;
                }

                WindowId windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);

                FolderPicker folderPicker = new(windowId)
                {
                    SuggestedStartLocation = PickerLocationId.ComputerFolder,
                    ViewMode = PickerViewMode.List
                };

                PickFolderResult result = await folderPicker.PickSingleFolderAsync();
                tcs.SetResult(result?.Path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while showing FolderPicker.");
                tcs.SetException(ex);
            }
        });

        if (!enqueued)
        {
            _logger.LogError("Failed to enqueue FolderPicker display on UI thread dispatcher.");
            tcs.SetResult(null);
        }

        return await tcs.Task;
    }
}

