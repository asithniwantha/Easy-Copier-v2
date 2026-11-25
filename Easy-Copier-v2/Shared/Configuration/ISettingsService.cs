namespace EasyCopier.Shared.Configuration;

/// <summary>
/// Defines operations for loading, saving, and retrieving application configuration settings.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Asynchronously loads settings from disk or returns cached settings if already loaded.
    /// </summary>
    /// <returns>A task returning the active <see cref="AppSettings"/> instance.</returns>
    Task<AppSettings> LoadSettingsAsync();

    /// <summary>
    /// Synchronously loads settings from disk or returns cached settings if already loaded.
    /// </summary>
    /// <returns>The active <see cref="AppSettings"/> instance.</returns>
    AppSettings LoadSettingsSync();

    /// <summary>
    /// Asynchronously saves application settings to disk and updates memory cache.
    /// </summary>
    /// <param name="settings">The <see cref="AppSettings"/> instance to persist.</param>
    /// <returns>A task representing the save operation.</returns>
    Task SaveSettingsAsync(AppSettings settings);

    /// <summary>
    /// Returns the full local file path to the settings JSON file.
    /// </summary>
    /// <returns>The file path string.</returns>
    string GetSettingsFilePath();
}

