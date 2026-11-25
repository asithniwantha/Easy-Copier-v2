using EasyCopier.Shared.Models;
using EasyCopier.Shared.Platform;

namespace EasyCopier.Features.Library.Scanning;

/// <summary>
/// Service for validating source folder paths and reading local game metadata files.
/// </summary>
public class SourceLibraryService : ISourceLibraryService
{
    private readonly ISourceFolderValidator _sourceFolderValidator;

    /// <summary>
    /// Initializes a new instance of the <see cref="SourceLibraryService"/> class.
    /// </summary>
    public SourceLibraryService(ISourceFolderValidator sourceFolderValidator)
    {
        _sourceFolderValidator = sourceFolderValidator ?? throw new ArgumentNullException(nameof(sourceFolderValidator));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SourceFolder>> ValidateSourceFoldersAsync(IEnumerable<string> folderPaths)
        => _sourceFolderValidator.ValidateSourceFoldersAsync(folderPaths);

    /// <inheritdoc />
    public Task<bool> FolderExistsAsync(string folderPath)
        => _sourceFolderValidator.FolderExistsAsync(folderPath);

    /// <inheritdoc />
    public async Task<string> GetSystemRequirementsAsync(string folderPath)
    {
        return await Task.Run(() =>
        {
            string reqFilePath = Path.Combine(folderPath, "system_requirements.txt");

            if (File.Exists(reqFilePath))
            {
                try
                {
                    return File.ReadAllText(reqFilePath);
                }
                catch
                {
                    return "Error reading system requirements.";
                }
            }

            return "System requirements aren't available.";
        }).ConfigureAwait(false);
    }
}

