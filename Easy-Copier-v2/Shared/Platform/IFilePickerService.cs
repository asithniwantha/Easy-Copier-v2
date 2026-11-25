namespace EasyCopier.Shared.Platform;

/// <summary>
/// Provides file picker dialog functionality for selecting files to open or save.
/// </summary>
public interface IFilePickerService
{
    /// <summary>
    /// Displays a file save picker modal dialog allowing the user to select a destination path.
    /// </summary>
    Task<string?> PickSaveFileAsync(string suggestedFileName, IDictionary<string, IList<string>> fileTypeChoices);

    /// <summary>
    /// Displays a file open picker modal dialog allowing the user to select an existing file.
    /// </summary>
    Task<string?> PickOpenFileAsync(IList<string> fileTypeFilters);
}

