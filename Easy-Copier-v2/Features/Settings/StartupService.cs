using System.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace EasyCopier.Features.Settings;

/// <summary>
/// Service for adding or removing the application's executable path in the Windows CurrentUser Run registry key.
/// </summary>
public sealed class StartupService : IStartupService
{
    private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "EasyCopier";
    private readonly ILogger<StartupService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StartupService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance for operational output.</param>
    public StartupService(ILogger<StartupService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    public void UpdateStartOnLogon(bool enable)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key is null)
            {
                _logger.LogWarning("Failed to open Registry Run key.");
                return;
            }

            if (enable)
            {
                string executablePath = Environment.ProcessPath ?? string.Empty;
                if (string.IsNullOrEmpty(executablePath))
                {
                    _logger.LogWarning("Failed to determine application path for startup registration.");
                    return;
                }

                string expectedFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "EasyCopier");

                if (!executablePath.StartsWith(expectedFolder, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning(
                        "Cannot enable start on logon: Application is not running from {ExpectedFolder}. Current path: {ExecutablePath}",
                        expectedFolder,
                        executablePath);
                    return;
                }

                string launchCommand = $"\"{executablePath}\"";
                key.SetValue(AppName, launchCommand);
                _logger.LogInformation("Added {AppName} to startup registry with path: {LaunchCommand}", AppName, launchCommand);
            }
            else if (key.GetValue(AppName) is not null)
            {
                key.DeleteValue(AppName, throwOnMissingValue: false);
                _logger.LogInformation("Removed {AppName} from startup registry.", AppName);
            }
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException or ObjectDisposedException or ArgumentException)
        {
            _logger.LogError(ex, "Error updating startup registry key.");
        }
    }
}

