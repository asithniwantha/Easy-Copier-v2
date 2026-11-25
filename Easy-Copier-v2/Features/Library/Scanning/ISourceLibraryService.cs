using EasyCopier.Shared.Models;

namespace EasyCopier.Features.Library.Scanning;

/// <summary>
/// Defines operations for validating source folder paths and reading game system requirements documents.
/// </summary>
public interface ISourceLibraryService
{
    /// <summary>
    /// Asynchronously checks a list of source folder paths and returns accessibility status for each folder.
    /// </summary>
    Task<IReadOnlyList<SourceFolder>> ValidateSourceFoldersAsync(IEnumerable<string> folderPaths);

    /// <summary>
    /// Asynchronously verifies whether a directory folder path exists on disk.
    /// </summary>
    Task<bool> FolderExistsAsync(string folderPath);

    /// <summary>
    /// Reads system requirements content from <c>system_requirements.txt</c> in the specified folder if present.
    /// </summary>
    Task<string> GetSystemRequirementsAsync(string folderPath);
}

