namespace EasyCopier.Shared.Platform;

/// <summary>
/// Exposes active transfer summary state and cancellation control for platform services (such as the system tray)
/// without coupling <c>Shared/</c> to <c>Features/TransferQueue/</c>.
/// </summary>
public interface ITransferActivityMonitor
{
    /// <summary>
    /// Gets the count of queued or currently executing transfers.
    /// </summary>
    int ActiveTransferCount { get; }

    /// <summary>
    /// Gets a value indicating whether any transfers are currently queued or executing.
    /// </summary>
    bool HasActiveTransfers { get; }

    /// <summary>
    /// Occurs when active transfer count, progress, or speed changes.
    /// </summary>
    event EventHandler? ActivityChanged;

    /// <summary>
    /// Builds a concise tooltip summary suitable for the Windows system tray notification icon.
    /// </summary>
    string GetTooltipSummary();

    /// <summary>
    /// Requests cancellation of all queued or in-progress transfers.
    /// </summary>
    void CancelAllActiveTransfers();
}

/// <summary>
/// Default no-op <see cref="ITransferActivityMonitor"/> implementation registered by <c>Shared/</c>
/// and superseded when the TransferQueue slice registers its live monitor.
/// </summary>
public sealed class NullTransferActivityMonitor : ITransferActivityMonitor
{
    /// <inheritdoc />
    public int ActiveTransferCount => 0;

    /// <inheritdoc />
    public bool HasActiveTransfers => false;

    /// <inheritdoc />
    public event EventHandler? ActivityChanged
    {
        add { }
        remove { }
    }

    /// <inheritdoc />
    public string GetTooltipSummary() => "Easy Copier \u2014 Idle";

    /// <inheritdoc />
    public void CancelAllActiveTransfers()
    {
    }
}

