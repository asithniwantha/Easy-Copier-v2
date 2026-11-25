using EasyCopier.Shared.Models;

namespace EasyCopier.Shared.Platform;

/// <summary>
/// Defines directory existence and batch accessibility validation operations for configured source folders.
/// </summary>
public interface ISourceFolderValidator
{
    /// <summary>
    /// Asynchronously checks a sequence of folder paths and returns accessibility metadata for each folder.
    /// </summary>
    Task<IReadOnlyList<SourceFolder>> ValidateSourceFoldersAsync(IEnumerable<string> folderPaths);

    /// <summary>
    /// Asynchronously verifies whether the specified directory path exists on disk.
    /// </summary>
    Task<bool> FolderExistsAsync(string folderPath);
}

