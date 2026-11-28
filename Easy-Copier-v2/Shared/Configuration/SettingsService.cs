using System.Security;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EasyCopier.Shared.Configuration;

/// <summary>
/// Service for reading and persisting application configuration settings to <c>appsettings.json</c>.
/// </summary>
public class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private const string SettingsFileName = "appsettings.json";

    private readonly ILogger<SettingsService> _logger;
    private readonly string? _customSettingsFilePath;
    private AppSettings? _cachedSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance for logging configuration operations.</param>
    [ActivatorUtilitiesConstructor]
    public SettingsService(ILogger<SettingsService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsService"/> class with a custom settings file path (for tests).
    /// </summary>
    /// <param name="logger">The logger instance for logging configuration operations.</param>
    /// <param name="settingsFilePath">Custom full path to the settings JSON file.</param>
    public SettingsService(ILogger<SettingsService> logger, string settingsFilePath)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsFilePath);
        _customSettingsFilePath = settingsFilePath;
    }

    /// <summary>
    /// Gets the absolute file path to the local <c>appsettings.json</c> file in AppData.
    /// </summary>
    /// <returns>The full file path string.</returns>
    public string GetSettingsFilePath()
    {
        if (!string.IsNullOrEmpty(_customSettingsFilePath))
        {
            string? customDirectory = Path.GetDirectoryName(_customSettingsFilePath);
            if (!string.IsNullOrEmpty(customDirectory))
            {
                _ = Directory.CreateDirectory(customDirectory);
            }

            return _customSettingsFilePath;
        }

        string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string appFolder = Path.Combine(appDataFolder, "EasyCopier");
        _ = Directory.CreateDirectory(appFolder);
        return Path.Combine(appFolder, SettingsFileName);
    }

    /// <summary>
    /// Asynchronously loads settings from disk, creating default settings if the file does not exist.
    /// </summary>
    /// <returns>A task returning the loaded <see cref="AppSettings"/>.</returns>
    public async Task<AppSettings> LoadSettingsAsync()
    {
        if (_cachedSettings != null)
        {
            return _cachedSettings;
        }

        string settingsPath = GetSettingsFilePath();

        try
        {
            if (!File.Exists(settingsPath))
            {
                _logger.LogInformation("Settings file not found, creating default settings");
                AppSettings defaultSettings = new();
                await SaveSettingsAsync(defaultSettings);
                _cachedSettings = defaultSettings;
                return defaultSettings;
            }

            string json = await File.ReadAllTextAsync(settingsPath);
            AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(json);

            if (settings == null)
            {
                _logger.LogWarning("Failed to deserialize settings, using defaults");
                _cachedSettings = new AppSettings();
                return _cachedSettings;
            }

            _logger.LogInformation("Settings loaded successfully from {Path}", settingsPath);
            _cachedSettings = settings;
            return settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException or ArgumentException or SecurityException)
        {
            _logger.LogError(ex, "Error loading settings from {Path}", settingsPath);
            _cachedSettings = new AppSettings();
            return _cachedSettings;
        }
    }

    /// <summary>
    /// Synchronously loads settings from disk, returning cached values if available or defaults if missing.
    /// </summary>
    /// <returns>The loaded <see cref="AppSettings"/>.</returns>
    public AppSettings LoadSettingsSync()
    {
        if (_cachedSettings != null)
        {
            return _cachedSettings;
        }

        string settingsPath = GetSettingsFilePath();
        try
        {
            if (!File.Exists(settingsPath))
            {
                _cachedSettings = new AppSettings();
                return _cachedSettings;
            }

            string json = File.ReadAllText(settingsPath);
            AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(json);
            _cachedSettings = settings ?? new AppSettings();
            return _cachedSettings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException or ArgumentException or SecurityException)
        {
            _cachedSettings = new AppSettings();
            return _cachedSettings;
        }
    }

    /// <summary>
    /// Asynchronously writes settings to disk and updates the memory cache.
    /// </summary>
    /// <param name="settings">The application settings to serialize and save.</param>
    /// <returns>A task representing the save operation.</returns>
    public async Task SaveSettingsAsync(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        string settingsPath = GetSettingsFilePath();

        try
        {
            string json = JsonSerializer.Serialize(settings, JsonOptions);
            await File.WriteAllTextAsync(settingsPath, json);

            _cachedSettings = settings;
            _logger.LogInformation("Settings saved successfully to {Path}", settingsPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving settings to {Path}", settingsPath);
            throw;
        }
    }
}

