using EasyCopier.Features.TransferQueue.Models;
using EasyCopier.Shared.Configuration;

namespace EasyCopier.Features.TransferQueue;

/// <summary>
/// Defines operations for verifying the integrity of transferred files and directories against their source.
/// </summary>
public interface IVerificationService
{
    /// <summary>
    /// Asynchronously validates the contents of a destination file or directory against its source.
    /// </summary>
    Task<VerificationResult> VerifyCopyAsync(
        string sourcePath,
        string destinationPath,
        CopyVerificationMode mode,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}
