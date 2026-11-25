using Microsoft.UI.Dispatching;

namespace EasyCopier.Shared.Platform;

/// <summary>
/// Manages execution of delegates on the WinUI 3 UI thread dispatcher queue.
/// </summary>
public class DispatcherService : IDispatcherService
{
    private readonly DispatcherQueue _dispatcherQueue;

    /// <summary>
    /// Initializes a new instance of the <see cref="DispatcherService"/> class bound to the current thread's dispatcher.
    /// </summary>
    public DispatcherService()
    {
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    }

    /// <summary>
    /// Gets a value indicating whether the caller is running on the UI thread.
    /// </summary>
    public bool HasThreadAccess => _dispatcherQueue?.HasThreadAccess ?? false;

    /// <inheritdoc />
    public bool TryEnqueue(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (_dispatcherQueue != null)
        {
            return _dispatcherQueue.TryEnqueue(() => action());
        }

        action();
        return true;
    }

    /// <inheritdoc />
    public bool TryEnqueue(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (_dispatcherQueue != null)
        {
            return _dispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    await action();
                }
                catch (Exception ex) when (ex is TaskCanceledException or OperationCanceledException)
                {
                    // Ignore expected cancellation exceptions on the dispatcher to prevent application crash during shutdown
                }
            });
        }

        _ = action();
        return true;
    }
}

