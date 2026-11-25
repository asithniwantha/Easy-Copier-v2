using EasyCopier.Features.TransferQueue.Models;

namespace EasyCopier.Features.TransferQueue;

/// <summary>
/// Defines operations for detecting, enumerating, and monitoring removable and USB drives.
/// </summary>
public interface IDriveDiscoveryService : IDisposable
{
    /// <summary>Asynchronously queries and returns a list of connected removable and USB drives.</summary>
    Task<IReadOnlyList<RemovableDrive>> GetRemovableDrivesAsync();

    /// <summary>Occurs when a volume change event is detected (e.g., drive insertion or removal).</summary>
    event EventHandler? DrivesChanged;

    /// <summary>Starts watching for hardware volume change events via WMI.</summary>
    void StartWatching();

    /// <summary>Stops watching for hardware volume change events.</summary>
    void StopWatching();
}

