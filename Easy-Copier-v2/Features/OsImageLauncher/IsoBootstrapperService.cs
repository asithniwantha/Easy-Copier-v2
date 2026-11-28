using System.ComponentModel;
using System.Text.Json;
using EasyCopier.Features.Shell.Contracts;
using EasyCopier.Shared.Configuration;
using EasyCopier.Shared.Platform;
using Microsoft.Extensions.Logging;

namespace EasyCopier.Features.OsImageLauncher;

/// <summary>
/// Discovers the latest Rufus executable binary and launches ISO image files in Rufus.
/// </summary>
public sealed class IsoBootstrapperService : IIsoBootstrapperService
{
    private readonly ISettingsService _settingsService;
    private readonly IFileSystemService _fileSystemService;
    private readonly IProcessService _processService;
    private readonly IRufusExecutableResolver _rufusResolver;
    private readonly ILogger<IsoBootstrapperService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="IsoBootstrapperService"/> class.
    /// </summary>
    public IsoBootstrapperService(
        ISettingsService settingsService,
        IFileSystemService fileSystemService,
        IProcessService processService,
        IRufusExecutableResolver rufusResolver,
        ILogger<IsoBootstrapperService> logger)
    {
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(fileSystemService);
        ArgumentNullException.ThrowIfNull(processService);
        ArgumentNullException.ThrowIfNull(rufusResolver);
        ArgumentNullException.ThrowIfNull(logger);

        _settingsService = settingsService;
        _fileSystemService = fileSystemService;
        _processService = processService;
        _rufusResolver = rufusResolver;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<(bool Success, string Message)> LaunchWithIsoAsync(string isoPath)
    {
        if (string.IsNullOrWhiteSpace(isoPath))
        {
            return (false, "No ISO file specified.");
        }

        if (!_fileSystemService.FileExists(isoPath))
        {
            return (false, $"ISO file not found: {isoPath}");
        }

        try
        {
            AppSettings settings = await _settingsService.LoadSettingsAsync();
            string latestRufusPath = _rufusResolver.ResolveLatestRufusPath(settings.RufusExecutablePath);

            if (!string.Equals(latestRufusPath, settings.RufusExecutablePath, StringComparison.OrdinalIgnoreCase))
            {
                settings.RufusExecutablePath = latestRufusPath;
                await _settingsService.SaveSettingsAsync(settings);
            }

            if (!_fileSystemService.FileExists(latestRufusPath))
            {
                _logger.LogWarning("Rufus executable not found at resolved path: {RufusPath}", latestRufusPath);
                return (false, $"Rufus executable not found at: {latestRufusPath}");
            }

            _processService.LaunchExecutable(latestRufusPath, $"-i \"{isoPath}\"");
            _logger.LogInformation("Successfully launched Rufus ({RufusPath}) with ISO: {IsoPath}", latestRufusPath, isoPath);
            return (true, $"Opened {Path.GetFileName(isoPath)} in Rufus.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or Win32Exception or InvalidOperationException or ArgumentException)
        {
            _logger.LogError(ex, "Failed to launch Rufus for ISO path: {IsoPath}", isoPath);
            return (false, $"Error opening Rufus: {ex.Message}");
        }
    }
}

