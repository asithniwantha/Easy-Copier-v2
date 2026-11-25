using EasyCopier.Features.Library.Models;

namespace EasyCopier.Features.Library.Duplicates;

/// <summary>
/// Defines safe deletion operations that route files and directories to the Windows Recycle Bin rather than permanently destroying them.
/// </summary>
public interface ISafeDeletionService
{
    /// <summary>
    /// Moves a single file or directory to the Windows Recycle Bin using native Windows Shell <c>IFileOperation</c>.
    /// </summary>
    Task<bool> DeleteToRecycleBinAsync(string targetPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves multiple files or directories to the Windows Recycle Bin in a batch with progress reporting.
    /// </summary>
    Task<SafeDeletionResult> BatchDeleteToRecycleBinAsync(
        IReadOnlyList<(string Path, long Bytes)> targets,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}

