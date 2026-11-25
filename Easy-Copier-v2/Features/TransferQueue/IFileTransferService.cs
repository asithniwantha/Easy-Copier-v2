using EasyCopier.Features.TransferQueue.Models;

namespace EasyCopier.Features.TransferQueue;

/// <summary>
/// Defines operations for executing file transfer requests and querying directory file size statistics.
/// </summary>
public interface IFileTransferService
{
    /// <summary>
    /// Asynchronously transfers items according to the provided transfer request configuration.
    /// </summary>
    Task<TransferOutcome> TransferGamesAsync(
        TransferRequest request,
        IProgress<TransferProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates the total byte size and total count of files contained within a specified file or directory path.
    /// </summary>
    Task<(long Size, int Count)> GetFolderStatsAsync(string path);
}
