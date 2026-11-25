using EasyCopier.Features.Shell.Contracts;
using EasyCopier.Features.TransferQueue.Models;
using EasyCopier.Shared.Models;

namespace EasyCopier.Features.TransferQueue;

/// <summary>
/// Implementation of <see cref="IDriveCoordinatorService"/> that coordinates drive discovery, real-time volume monitoring, and copy validation.
/// </summary>
public sealed class DriveCoordinatorService : IDriveCoordinatorService
{
    public IDriveDiscoveryService Discovery { get; }
    public IDriveValidationService Validation { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DriveCoordinatorService"/> class.
    /// </summary>
    public DriveCoordinatorService(IDriveDiscoveryService discovery, IDriveValidationService validation)
    {
        Discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
        Validation = validation ?? throw new ArgumentNullException(nameof(validation));
        Discovery.DrivesChanged += OnDrivesChanged;
    }

    /// <inheritdoc />
    public event EventHandler? DrivesChanged;

    /// <inheritdoc />
    public Task<IReadOnlyList<RemovableDrive>> GetRemovableDrivesAsync() => Discovery.GetRemovableDrivesAsync();

    /// <inheritdoc />
    public Task<IReadOnlyList<ValidationResult>> ValidateTransferAsync(
        IEnumerable<GameEntry> items,
        RemovableDrive targetDrive,
        string destinationBasePath)
    {
        return Validation.ValidateTransferAsync(items, targetDrive, destinationBasePath);
    }

    /// <inheritdoc />
    public void StartWatching() => Discovery.StartWatching();

    /// <inheritdoc />
    public void StopWatching() => Discovery.StopWatching();

    private void OnDrivesChanged(object? sender, EventArgs e) => DrivesChanged?.Invoke(sender, e);

    /// <inheritdoc />
    public void Dispose()
    {
        Discovery.DrivesChanged -= OnDrivesChanged;
        Discovery.Dispose();
    }
}

