using EasyCopier.Features.Shell.Contracts;
using EasyCopier.Features.TransferQueue.Models;
using EasyCopier.Shared.Models;

namespace EasyCopier.Features.TransferQueue;

/// <summary>
/// Coordinates drive discovery, hardware monitoring, and transfer validation operations across connected storage volumes.
/// </summary>
public interface IDriveCoordinatorService : IDisposable
{
    IDriveDiscoveryService Discovery { get; }
    IDriveValidationService Validation { get; }

    Task<IReadOnlyList<RemovableDrive>> GetRemovableDrivesAsync();

    Task<IReadOnlyList<ValidationResult>> ValidateTransferAsync(
        IEnumerable<GameEntry> items,
        RemovableDrive targetDrive,
        string destinationBasePath);

    void StartWatching();
    void StopWatching();
    event EventHandler? DrivesChanged;
}

