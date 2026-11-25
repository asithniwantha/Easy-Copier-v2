namespace EasyCopier.Features.Settings;

/// <summary>
/// Defines operations for checking, downloading, and applying application updates via Velopack.
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// Asynchronously checks if a newer version of the application is available from the release feed.
    /// </summary>
    Task<bool> CheckForUpdatesAsync();

    /// <summary>
    /// Asynchronously downloads the latest available application update package.
    /// </summary>
    Task DownloadUpdateAsync();

    /// <summary>
    /// Restarts the application and applies the downloaded update package.
    /// </summary>
    void RestartAndApplyUpdate();
}

