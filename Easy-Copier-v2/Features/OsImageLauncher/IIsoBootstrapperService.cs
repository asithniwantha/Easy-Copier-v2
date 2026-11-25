namespace EasyCopier.Features.OsImageLauncher;

/// <summary>
/// Contract for Rufus integration and OS ISO image launching operations.
/// </summary>
public interface IIsoBootstrapperService
{
    /// <summary>
    /// Resolves the Rufus executable path and launches Rufus with the specified ISO file path.
    /// </summary>
    /// <param name="isoPath">The full path to the target ISO file.</param>
    /// <returns>A tuple containing a success indicator and a status message.</returns>
    Task<(bool Success, string Message)> LaunchWithIsoAsync(string isoPath);
}

