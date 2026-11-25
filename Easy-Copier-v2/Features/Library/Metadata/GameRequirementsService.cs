using EasyCopier.Features.Library.Scanning;

namespace EasyCopier.Features.Library.Metadata;

/// <summary>
/// Implements <see cref="IGameRequirementsService"/> to retrieve and format game system requirements.
/// </summary>
public class GameRequirementsService : IGameRequirementsService
{
    private readonly ISourceLibraryService _sourceLibraryService;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameRequirementsService"/> class.
    /// </summary>
    public GameRequirementsService(ISourceLibraryService sourceLibraryService)
    {
        _sourceLibraryService = sourceLibraryService ?? throw new ArgumentNullException(nameof(sourceLibraryService));
    }

    /// <inheritdoc />
    public async Task<string> GetFormattedRequirementsAsync(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath))
        {
            return string.Empty;
        }

        string rawRequirementsText = await _sourceLibraryService.GetSystemRequirementsAsync(folderPath).ConfigureAwait(false);
        return SysReqFormatter.FormatText(rawRequirementsText);
    }
}

