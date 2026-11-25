using System.Collections.Specialized;
using System.ComponentModel;
using EasyCopier.Features.TransferQueue.Models;
using EasyCopier.Shared.Platform;

namespace EasyCopier.Features.TransferQueue;

/// <summary>
/// Tracks live active transfers in <see cref="ITransferQueueService"/> for <see cref="ISystemTrayService"/>
/// without coupling <c>Shared/</c> to <c>Features/TransferQueue/</c>.
/// </summary>
public sealed class TransferActivityMonitor : ITransferActivityMonitor, IDisposable
{
    private readonly ITransferQueueService _transferQueueService;
    private readonly List<TransferQueueItem> _trackedItems = [];
    private bool _disposed;

    /// <inheritdoc />
    public event EventHandler? ActivityChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransferActivityMonitor"/> class.
    /// </summary>
    public TransferActivityMonitor(ITransferQueueService transferQueueService)
    {
        ArgumentNullException.ThrowIfNull(transferQueueService);
        _transferQueueService = transferQueueService;

        foreach (TransferQueueItem item in _transferQueueService.QueueItems)
        {
            AttachItem(item);
        }

        _transferQueueService.QueueItems.CollectionChanged += OnQueueItemsChanged;
    }

    /// <inheritdoc />
    public int ActiveTransferCount => _transferQueueService.QueueItems.Count(i => i.IsActive);

    /// <inheritdoc />
    public bool HasActiveTransfers => _transferQueueService.QueueItems.Any(i => i.IsActive);

    /// <inheritdoc />
    public string GetTooltipSummary()
    {
        List<TransferQueueItem> active = _transferQueueService.QueueItems.Where(i => i.IsActive).ToList();
        if (active.Count == 0)
        {
            return "Easy Copier";
        }

        TransferQueueItem? inProgress = active.FirstOrDefault(i => i.Status == TransferQueueItemStatus.InProgress);
        string summary = inProgress is not null
            ? $"Easy Copier - {inProgress.ItemsSummary} ({Math.Round(inProgress.ProgressPercentage)}%)"
            : $"Easy Copier - {active.Count} transfer(s) queued";

        return summary.Length > 120 ? summary[..120] : summary;
    }

    /// <inheritdoc />
    public void CancelAllActiveTransfers()
    {
        List<TransferQueueItem> activeItems = _transferQueueService.QueueItems.Where(i => i.IsActive).ToList();
        foreach (TransferQueueItem item in activeItems)
        {
            _transferQueueService.CancelItem(item);
        }

        RaiseActivityChanged();
    }

    private void OnQueueItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (TransferQueueItem item in e.OldItems)
            {
                DetachItem(item);
            }
        }

        if (e.NewItems is not null)
        {
            foreach (TransferQueueItem item in e.NewItems)
            {
                AttachItem(item);
            }
        }

        RaiseActivityChanged();
    }

    private void AttachItem(TransferQueueItem item)
    {
        if (_trackedItems.Contains(item))
        {
            return;
        }

        item.PropertyChanged += OnItemPropertyChanged;
        _trackedItems.Add(item);
    }

    private void DetachItem(TransferQueueItem item)
    {
        item.PropertyChanged -= OnItemPropertyChanged;
        _ = _trackedItems.Remove(item);
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TransferQueueItem.Status)
            or nameof(TransferQueueItem.IsActive)
            or nameof(TransferQueueItem.ProgressPercentage))
        {
            RaiseActivityChanged();
        }
    }

    private void RaiseActivityChanged() => ActivityChanged?.Invoke(this, EventArgs.Empty);

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _transferQueueService.QueueItems.CollectionChanged -= OnQueueItemsChanged;

        foreach (TransferQueueItem item in _trackedItems)
        {
            item.PropertyChanged -= OnItemPropertyChanged;
        }

        _trackedItems.Clear();
    }
}
