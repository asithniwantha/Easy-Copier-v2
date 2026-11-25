using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.UI;
using Microsoft.Windows.Storage.Pickers;

namespace EasyCopier.Shared.Platform;

/// <summary>
/// Implements elevated-safe file picker operations using <see cref="FileOpenPicker"/> and <see cref="FileSavePicker"/> initialized with <see cref="WindowId"/>.
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
                WindowId windowId = _appWindowContext.ActiveWindowId;
                if (windowId.Value == 0)
                {
                    _logger.LogError("Unable to retrieve a valid WindowId for FileOpenPicker.");
                    tcs.SetResult(null);
                    return;
                }

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
                _logger.LogError(ex, "Error occurred while showing FileOpenPicker.");
                tcs.SetException(ex);
            }
        });

        if (!enqueued)
        {
            _logger.LogError("Failed to enqueue FileOpenPicker display on UI thread dispatcher.");
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
                WindowId windowId = _appWindowContext.ActiveWindowId;
                if (windowId.Value == 0)
                {
                    _logger.LogError("Unable to retrieve a valid WindowId for FileSavePicker.");
                    tcs.SetResult(null);
                    return;
                }

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
