using EasyCopier.Shared.Interop;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.Windows.Storage.Pickers;
using WinRT.Interop;

namespace EasyCopier.Shared.Platform;

/// <summary>
/// Implements file picker operations using WinRT <see cref="FileOpenPicker"/> and <see cref="FileSavePicker"/>.
/// </summary>
public class FilePickerService : IFilePickerService
{
    private readonly IDispatcherService _dispatcherService;
    private readonly IAppWindowContext _appWindowContext;
    private readonly ILogger<FilePickerService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FilePickerService"/> class.
    /// </summary>
    public FilePickerService(
        IDispatcherService dispatcherService,
        IAppWindowContext appWindowContext,
        ILogger<FilePickerService>? logger = null)
    {
        _dispatcherService = dispatcherService ?? throw new ArgumentNullException(nameof(dispatcherService));
        _appWindowContext = appWindowContext ?? throw new ArgumentNullException(nameof(appWindowContext));
        _logger = logger ?? NullLogger<FilePickerService>.Instance;
    }

    /// <inheritdoc />
    public async Task<string?> PickOpenFileAsync(IList<string> fileTypeFilters)
    {
        ArgumentNullException.ThrowIfNull(fileTypeFilters);

        TaskCompletionSource<string?> tcs = new();

        bool enqueued = _dispatcherService.TryEnqueue(async () =>
        {
            try
            {
                nint windowHandle = NativeWindowHelper.GetActiveWindowHandle();
                if (windowHandle == IntPtr.Zero && _appWindowContext.MainWindow is Window mainWindow)
                {
                    windowHandle = WindowNative.GetWindowHandle(mainWindow);
                }

                if (windowHandle == IntPtr.Zero)
                {
                    tcs.SetResult(null);
                    return;
                }

                WindowId windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);

                FileOpenPicker openPicker = new(windowId)
                {
                    SuggestedStartLocation = PickerLocationId.ComputerFolder,
                    ViewMode = PickerViewMode.List
                };

                foreach (string filter in fileTypeFilters)
                {
                    openPicker.FileTypeFilter.Add(filter);
                }

                PickFileResult result = await openPicker.PickSingleFileAsync();
                tcs.SetResult(result?.Path);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        if (!enqueued)
        {
            tcs.SetResult(null);
        }

        return await tcs.Task;
    }

    /// <inheritdoc />
    public async Task<string?> PickSaveFileAsync(string suggestedFileName, IDictionary<string, IList<string>> fileTypeChoices)
    {
        ArgumentNullException.ThrowIfNull(fileTypeChoices);

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
                    _logger.LogError("Unable to retrieve a valid window handle for FileSavePicker.");
                    tcs.SetResult(null);
                    return;
                }

                WindowId windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);

                FileSavePicker savePicker = new(windowId)
                {
                    SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                    SuggestedFileName = suggestedFileName
                };

                foreach (KeyValuePair<string, IList<string>> kvp in fileTypeChoices)
                {
                    savePicker.FileTypeChoices.Add(kvp.Key, kvp.Value);
                }

                PickFileResult result = await savePicker.PickSaveFileAsync();
                tcs.SetResult(result?.Path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while showing FileSavePicker.");
                tcs.SetException(ex);
            }
        });

        if (!enqueued)
        {
            _logger.LogError("Failed to enqueue FileSavePicker display on UI thread dispatcher.");
            tcs.SetResult(null);
        }

        return await tcs.Task;
    }
}

