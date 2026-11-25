namespace EasyCopier.Shared.Platform;

/// <summary>
/// Provides services for external process execution and environment security checks.
/// </summary>
public interface IProcessService
{
    /// <summary>
    /// Opens Windows File Explorer to the specified directory or file path.
    /// </summary>
    /// <param name="path">The folder or file path to display.</param>
    void OpenInExplorer(string path);

    /// <summary>
    /// Checks whether the current application process is executing with Administrator privileges.
    /// </summary>
    /// <returns><c>true</c> if running as Administrator; otherwise, <c>false</c>.</returns>
    bool IsRunningAsAdministrator();

    /// <summary>
    /// Opens the native Windows Format dialog for the specified drive.
    /// </summary>
    /// <param name="driveLetter">The drive letter (e.g., "D:" or "D").</param>
    void OpenFormatDialog(string driveLetter);
}

