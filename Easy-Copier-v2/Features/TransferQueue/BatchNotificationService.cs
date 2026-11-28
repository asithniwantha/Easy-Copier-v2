using System.Runtime.InteropServices;
using EasyCopier.Features.TransferQueue.Models;
using EasyCopier.Shared.Configuration;
using EasyCopier.Shared.Platform;
using EasyCopier.Shared.UI.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace EasyCopier.Features.TransferQueue;

/// <summary>
/// Evaluates completed drive transfer batches, executes audio alerts, and formats Windows desktop toast notifications.
/// </summary>
public sealed class BatchNotificationService : IBatchNotificationService
{
    private readonly ISettingsService _settingsService;
    private readonly IAudioPlaybackService _audioPlaybackService;
    private readonly IProcessService _processService;
    private readonly ILogger<BatchNotificationService> _logger;

    /// <inheritdoc />
    public event EventHandler<(string Title, string Message, bool IsSuccess)>? BatchCompleted;

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchNotificationService"/> class.
    /// </summary>
    public BatchNotificationService(
        ISettingsService settingsService,
        IAudioPlaybackService audioPlaybackService,
        IProcessService processService,
        ILogger<BatchNotificationService> logger)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _audioPlaybackService = audioPlaybackService ?? throw new ArgumentNullException(nameof(audioPlaybackService));
        _processService = processService ?? throw new ArgumentNullException(nameof(processService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public void EvaluateAndNotifyBatchCompletion(string driveLetter, IEnumerable<TransferQueueItem> queueItems)
    {
        ArgumentNullException.ThrowIfNull(queueItems);

        if (string.IsNullOrWhiteSpace(driveLetter))
        {
            return;
        }

        AppSettings settings = _settingsService.LoadSettingsSync();
        List<TransferQueueItem> itemsList = [.. queueItems];

        int activeItemsForDrive = itemsList.Count(i =>
            i.IsActive && string.Equals(i.TargetDrive.DriveLetter, driveLetter, StringComparison.OrdinalIgnoreCase));

        if (activeItemsForDrive > 0)
        {
            return;
        }

        List<TransferQueueItem> batchItems = itemsList
            .Where(i => !i.IsActive && string.Equals(i.TargetDrive.DriveLetter, driveLetter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (batchItems.Count == 0)
        {
            return;
        }

        bool anyFailedOrCancelled = batchItems.Any(i => i.Status is TransferQueueItemStatus.Failed or TransferQueueItemStatus.Cancelled);

        if (settings.PlayNotificationSounds)
        {
            if (anyFailedOrCancelled)
            {
                _audioPlaybackService.PlayFailureSound();
            }
            else
            {
                _audioPlaybackService.PlaySuccessSound();
            }
        }

        if (settings.ShowDesktopNotifications)
        {
            ShowDesktopNotification(batchItems, !anyFailedOrCancelled);
        }
    }

    private void ShowDesktopNotification(List<TransferQueueItem> batchItems, bool isSuccess)
    {
        try
        {
            TransferQueueItem firstItem = batchItems[0];
            long totalDriveCapacity = firstItem.TargetDrive.TotalBytes;
            string statusText = isSuccess ? "Complete" : "Failed";
            string title = $"{firstItem.TargetDrive.DriveLetter} - {FormattingHelpers.FormatBytes(totalDriveCapacity)} Capacity - {statusText}";

            long totalBytes = batchItems.Sum(x => x.TotalBytes);
            int totalPrice = batchItems.Sum(x => x.TotalPrice);
            List<string> allGames = batchItems.SelectMany(x => x.Items).Select(x => x.Game.Name).ToList();
            int totalItems = allGames.Count;

            string namesText = totalItems > 3
                ? $"{totalItems} items: {string.Join(", ", allGames.Take(3))} and {totalItems - 3} more."
                : $"{totalItems} items: {string.Join(", ", allGames)}.";
            string body = $"{namesText} Size: {FormattingHelpers.FormatBytes(totalBytes)}. Price: Rs. {totalPrice}";

            BatchCompleted?.Invoke(this, (title, body, isSuccess));

            AppNotificationBuilder builder = new AppNotificationBuilder()
                .AddText(title)
                .AddText(body);

            _logger.LogInformation("Attempting to show desktop notification: {Title}", title);
            AppNotification notification = builder.BuildNotification();
            if (!AppNotificationManager.IsSupported())
            {
                _logger.LogWarning(
                    "AppNotificationManager is not supported (RunningAsAdministrator={IsAdmin}). Skipping toast display.",
                    _processService.IsRunningAsAdministrator());
                return;
            }

            if (AppNotificationManager.Default.Setting != AppNotificationSetting.Enabled)
            {
                _logger.LogWarning("Desktop notifications disabled with setting: {Setting}", AppNotificationManager.Default.Setting);
            }

            AppNotificationManager.Default.Show(notification);

            if (notification.Id != 0)
            {
                _logger.LogInformation("Desktop notification shown successfully with ID: {Id}", notification.Id);
            }
            else
            {
                _logger.LogWarning("AppNotificationManager.Default.Show returned notification ID 0.");
            }
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or UnauthorizedAccessException or PlatformNotSupportedException or ArgumentException)
        {
            _logger.LogError(ex, "Failed to show desktop notification.");
        }
    }
}

