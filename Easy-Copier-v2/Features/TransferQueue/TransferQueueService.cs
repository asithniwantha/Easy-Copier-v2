using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Threading.Channels;
using CommunityToolkit.Mvvm.Messaging;
using EasyCopier.Features.TransferQueue.Models;
using EasyCopier.Shared.Configuration;
using EasyCopier.Shared.Messages;
using EasyCopier.Shared.Platform;
using EasyCopier.Shared.UI.Helpers;
using Microsoft.Extensions.Logging;

namespace EasyCopier.Features.TransferQueue;

/// <summary>
/// Service managing background asynchronous file transfer queues with per-drive channels,
/// sound alerts, toast notifications, and cross-slice <see cref="TransferBatchCompletedMessage"/> publishing.
/// </summary>
public sealed class TransferQueueService : ITransferQueueService, IDisposable
{
    private readonly IFileTransferService _fileTransferService;
    private readonly ILogger<TransferQueueService> _logger;
    private readonly IDispatcherService _dispatcherService;
    private readonly ISettingsService _settingsService;
    private readonly IBatchNotificationService _batchNotificationService;
    private readonly IMessenger _messenger;
    private readonly Channel<TransferQueueItem> _channel = Channel.CreateUnbounded<TransferQueueItem>();
    private readonly ConcurrentDictionary<string, Channel<TransferQueueItem>> _driveChannels = new(StringComparer.OrdinalIgnoreCase);

    private int _isDisposed;

    /// <inheritdoc />
    public ObservableCollection<TransferQueueItem> QueueItems { get; } = [];

    /// <inheritdoc />
    public event EventHandler<TransferQueueItem>? ItemCompleted;

    /// <inheritdoc />
    public event EventHandler<(string Title, string Message, bool IsSuccess)>? BatchCompleted;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransferQueueService"/> class and starts background consumer tasks.
    /// </summary>
    public TransferQueueService(
        IFileTransferService fileTransferService,
        ILogger<TransferQueueService> logger,
        IDispatcherService dispatcherService,
        ISettingsService settingsService,
        IBatchNotificationService batchNotificationService,
        IMessenger messenger)
    {
        ArgumentNullException.ThrowIfNull(fileTransferService);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(dispatcherService);
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(batchNotificationService);
        ArgumentNullException.ThrowIfNull(messenger);

        _fileTransferService = fileTransferService;
        _logger = logger;
        _dispatcherService = dispatcherService;
        _settingsService = settingsService;
        _batchNotificationService = batchNotificationService;
        _messenger = messenger;

        _batchNotificationService.BatchCompleted += OnNotificationBatchCompleted;

        _ = Task.Run(ProcessQueueAsync);
    }

    private void OnNotificationBatchCompleted(object? sender, (string Title, string Message, bool IsSuccess) args)
    {
        BatchCompleted?.Invoke(sender, args);
    }

    /// <inheritdoc />
    public TransferQueueItem Enqueue(IReadOnlyList<TransferItem> items, RemovableDrive targetDrive, string destinationPath)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(targetDrive);

        AppSettings settings = _settingsService.LoadSettingsSync();
        int totalPrice = items.Sum(i => FormattingHelpers.CalculatePrice(i.Game.TotalBytes, settings));

        TransferQueueItem item = new(items, targetDrive, destinationPath, totalPrice);

        QueueItems.Add(item);
        _ = _channel.Writer.TryWrite(item);

        _logger.LogInformation(
            "Enqueued transfer of {Count} item(s) to {Drive} (queue depth: {Depth})",
            items.Count, targetDrive.DriveLetter, QueueItems.Count);

        return item;
    }

    /// <inheritdoc />
    public long GetReservedBytes(string driveLetter)
    {
        return QueueItems
            .Where(i => i.IsActive && string.Equals(i.TargetDrive.DriveLetter, driveLetter, StringComparison.OrdinalIgnoreCase))
            .Sum(i => i.TotalBytes);
    }

    /// <inheritdoc />
    public void ClearFinished()
    {
        List<TransferQueueItem> toRemove = [.. QueueItems.Where(i => !i.IsActive)];
        foreach (TransferQueueItem item in toRemove)
        {
            _ = QueueItems.Remove(item);
            item.Dispose();
        }
    }

    /// <inheritdoc />
    public void CancelItem(TransferQueueItem item)
    {
        if (item is not null && item.IsActive)
        {
            _logger.LogInformation("User requested cancellation of queue item {Id} for drive {Drive}", item.Id, item.TargetDrive.DriveLetter);
            item.Cancel();
        }
    }

    /// <inheritdoc />
    public void RetryItem(TransferQueueItem item)
    {
        if (item is null || !item.CanRetry)
        {
            return;
        }

        string? root = Path.GetPathRoot(item.DestinationPath);
        if (!string.IsNullOrEmpty(root) && !Directory.Exists(root))
        {
            _logger.LogWarning("Cannot retry item {Id}: destination drive {Root} is not accessible", item.Id, root);
            RunOnUiThread(() =>
            {
                item.Status = TransferQueueItemStatus.Failed;
                item.StatusMessage = $"Target drive {item.TargetDrive.DriveLetter} is not connected";
            });
            return;
        }

        _logger.LogInformation("Retrying queue item {Id} for drive {Drive}", item.Id, item.TargetDrive.DriveLetter);

        RunOnUiThread(item.ResetForRetry);
        _ = _channel.Writer.TryWrite(item);
    }

    private async Task ProcessQueueAsync()
    {
        await foreach (TransferQueueItem item in _channel.Reader.ReadAllAsync())
        {
            string driveKey = NormalizeDriveKey(item.TargetDrive.DriveLetter);
            Channel<TransferQueueItem> driveChannel = _driveChannels.GetOrAdd(driveKey, key =>
            {
                Channel<TransferQueueItem> channel = Channel.CreateUnbounded<TransferQueueItem>();
                _ = Task.Run(() => ProcessDriveQueueAsync(key, channel));
                return channel;
            });

            _ = driveChannel.Writer.TryWrite(item);
        }
    }

    private async Task ProcessDriveQueueAsync(string driveKey, Channel<TransferQueueItem> driveChannel)
    {
        await foreach (TransferQueueItem item in driveChannel.Reader.ReadAllAsync())
        {
            await ProcessItemAsync(item);
        }

        _ = _driveChannels.TryRemove(driveKey, out _);
    }

    private async Task ProcessItemAsync(TransferQueueItem item)
    {
        if (item.Status == TransferQueueItemStatus.Cancelled)
        {
            _logger.LogInformation("Queue item {Id} was cancelled before starting; skipping.", item.Id);
            return;
        }

        RunOnUiThread(() =>
        {
            item.Status = TransferQueueItemStatus.InProgress;
            item.StatusMessage = $"Copying to {item.TargetDrive.DriveLetter}...";
        });

        TransferOutcome? outcome = null;

        try
        {
            Progress<TransferProgress> progress = new(p =>
            {
                RunOnUiThread(() =>
                {
                    item.ProgressPercentage = p.Percentage;
                    item.SpeedText = p.SpeedText;
                    item.RemainingTimeText = p.RemainingTimeText;
                });
            });

            TransferRequest request = new(item.Items, item.TargetDrive, item.DestinationPath);
            outcome = await _fileTransferService.TransferGamesAsync(request, progress, item.CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Queue item transfer failed for {Drive}", item.TargetDrive.DriveLetter);
        }

        PublishBatchCompleted(item, outcome);

        RunOnUiThread(() =>
        {
            if (outcome is not null)
            {
                item.Status = outcome.Success ? TransferQueueItemStatus.Completed : TransferQueueItemStatus.Failed;
                item.StatusMessage = outcome.Message;

                if (outcome.Success)
                {
                    item.ProgressPercentage = 100.0;
                    item.SpeedText = string.Empty;
                    item.RemainingTimeText = string.Empty;
                }
            }
            else
            {
                item.Status = TransferQueueItemStatus.Failed;
                item.StatusMessage = "Transfer failed unexpectedly";
            }

            item.CompletedAt = DateTime.Now;
            ItemCompleted?.Invoke(this, item);

            _batchNotificationService.EvaluateAndNotifyBatchCompletion(item.TargetDrive.DriveLetter, QueueItems);
        });
    }

    private void PublishBatchCompleted(TransferQueueItem item, TransferOutcome? outcome)
    {
        Guid batchId = Guid.TryParse(item.Id, out Guid parsedId) ? parsedId : Guid.NewGuid();
        IReadOnlyList<TransferCompletedItemDescriptor> descriptors = outcome?.ItemResults is { Count: > 0 } results
            ? results
            : item.Items.Select(t => new TransferCompletedItemDescriptor(
                t.Game.Name,
                t.Game.FolderPath,
                Path.Combine(item.DestinationPath, t.Game.Name),
                BytesTransferred: outcome?.Success == true ? t.Game.TotalBytes : 0,
                IsSuccess: outcome?.Success ?? false,
                Amount: 0,
                BatchAmount: 0,
                ErrorLog: outcome?.Message ?? "Transfer failed unexpectedly",
                SubFilesJson: string.Empty,
                Duration: outcome?.Duration ?? TimeSpan.Zero,
                StatusText: outcome?.Success == true ? "Completed" : "Failed",
                Timestamp: outcome?.CompletedAt ?? DateTime.Now)).ToList();

        int successCount = descriptors.Count(d => d.IsSuccess && !string.Equals(d.StatusText, "Skipped", StringComparison.OrdinalIgnoreCase));
        int failureCount = descriptors.Count(d => !d.IsSuccess);
        long bytesTransferred = outcome?.BytesTransferred ?? descriptors.Sum(d => d.BytesTransferred);

        _messenger.Send(new TransferBatchCompletedMessage(
            batchId,
            item.TargetDrive.DriveLetter,
            successCount,
            failureCount,
            bytesTransferred,
            outcome?.Duration ?? TimeSpan.Zero,
            item.TargetDrive.DriveLabel,
            descriptors));
    }

    private static string NormalizeDriveKey(string driveLetter) =>
        (driveLetter ?? string.Empty).Trim().TrimEnd('\\').ToUpperInvariant();

    private void RunOnUiThread(Action action)
    {
        if (!_dispatcherService.HasThreadAccess)
        {
            _ = _dispatcherService.TryEnqueue(action);
        }
        else
        {
            action();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        _batchNotificationService.BatchCompleted -= OnNotificationBatchCompleted;
        _ = _channel.Writer.TryComplete();
        foreach (Channel<TransferQueueItem> driveChannel in _driveChannels.Values)
        {
            _ = driveChannel.Writer.TryComplete();
        }
    }
}
