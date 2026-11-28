using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.UI;
using Microsoft.Windows.Storage.Pickers;

namespace EasyCopier.Shared.Platform;

/// <summary>
/// Implements elevated-safe folder selection operations using <see cref="FolderPicker"/> initialized with <see cref="WindowId"/>.
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

    /// <inheritdoc />
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Forwards dispatcher callback exceptions to the caller via TaskCompletionSource.SetException instead of crashing the UI dispatcher.")]
    public async Task<string?> PickFolderAsync(string? startingDirectory = null)
    {
        TaskCompletionSource<string?> tcs = new();

        bool enqueued = _dispatcherService.TryEnqueue(async () =>
        {
            try
            {
                WindowId windowId = _appWindowContext.ActiveWindowId;
                if (windowId.Value == 0)
                {
                    _logger.LogError("Unable to retrieve a valid WindowId for FolderPicker.");
                    tcs.SetResult(null);
                    return;
                }

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
