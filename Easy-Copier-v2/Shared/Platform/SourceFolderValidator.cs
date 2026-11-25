using EasyCopier.Shared.Models;
using Microsoft.Extensions.Logging;

namespace EasyCopier.Shared.Platform;

/// <summary>
/// Validates configured library source folder paths against the local file system.
/// </summary>
public sealed class SourceFolderValidator : ISourceFolderValidator
{
    private readonly ILogger<SourceFolderValidator> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SourceFolderValidator"/> class.
    /// </summary>
    public SourceFolderValidator(ILogger<SourceFolderValidator> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SourceFolder>> ValidateSourceFoldersAsync(IEnumerable<string> folderPaths)
    {
        ArgumentNullException.ThrowIfNull(folderPaths);

        IEnumerable<Task<SourceFolder>> tasks = folderPaths.Select(async path =>
        {
            try
            {
                bool exists = await Task.Run(() => Directory.Exists(path));
                SourceFolder folder = new(path, exists, DateTime.Now);

                if (!exists)
                {
                    _logger.LogWarning("Source folder not accessible: {Path}", path);
                }

                return folder;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating source folder: {Path}", path);
                return new SourceFolder(path, false, DateTime.Now);
            }
        });

        return await Task.WhenAll(tasks);
    }

    /// <inheritdoc />
    public Task<bool> FolderExistsAsync(string folderPath)
    {
        return Task.Run(() => Directory.Exists(folderPath));
    }
}

