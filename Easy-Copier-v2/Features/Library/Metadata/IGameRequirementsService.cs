namespace EasyCopier.Features.Library.Metadata;

/// <summary>
/// Defines operations for retrieving and formatting system requirements for game directories.
/// </summary>
public interface IGameRequirementsService
{
    /// <summary>
    /// Asynchronously retrieves and formats the system requirements text for the specified game folder.
    /// </summary>
    Task<string> GetFormattedRequirementsAsync(string folderPath);
}

