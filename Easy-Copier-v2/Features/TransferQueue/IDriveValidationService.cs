using EasyCopier.Features.Shell.Contracts;
using EasyCopier.Features.TransferQueue.Models;
using EasyCopier.Shared.Models;

namespace EasyCopier.Features.TransferQueue;

/// <summary>
/// Defines operations for validating file transfer operations prior to execution.
/// </summary>
public interface IDriveValidationService
{
    /// <summary>
    /// Validates whether the selected entries can be safely copied to the specified target drive and path.
    /// </summary>
    Task<IReadOnlyList<ValidationResult>> ValidateTransferAsync(
        IEnumerable<GameEntry> games,
        RemovableDrive targetDrive,
        string destinationBasePath);
}

