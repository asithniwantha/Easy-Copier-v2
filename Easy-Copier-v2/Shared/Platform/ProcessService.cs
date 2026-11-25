using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Extensions.Logging;

namespace EasyCopier.Shared.Platform;

/// <summary>
/// Implements process launching and Windows identity checking functionality.
/// </summary>
public partial class ProcessService : IProcessService
{
    private const uint SHFMT_ID_DEFAULT = 0xFFFF;
    private const uint SHFMT_ERROR = 0xFFFFFFFF;
    private const uint SHFMT_CANCEL = 0xFFFFFFFE;
    private const uint SHFMT_NOFORMAT = 0xFFFFFFFD;

    private readonly ILogger<ProcessService>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessService"/> class.
    /// </summary>
    /// <param name="logger">Optional logger instance for recording process execution diagnostics.</param>
    public ProcessService(ILogger<ProcessService>? logger = null)
    {
        _logger = logger;
    }

    [LibraryImport("shell32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial uint SHFormatDrive(IntPtr hwnd, uint drive, uint fmtID, uint options);

    /// <inheritdoc />
    public void OpenInExplorer(string path)
    {
        try
        {
            _ = Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to launch Windows Explorer for path: {Path}", path);
            Trace.TraceWarning("Failed to launch Windows Explorer for path '{0}': {1}", path, ex);
        }
    }

    /// <inheritdoc />
    public bool IsRunningAsAdministrator()
    {
        try
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to determine administrator execution role.");
            return false;
        }
    }

    /// <inheritdoc />
    public void OpenFormatDialog(string driveLetter)
    {
        if (string.IsNullOrWhiteSpace(driveLetter))
        {
            return;
        }

        char letter = driveLetter[0];
        if (!char.IsAsciiLetter(letter))
        {
            return;
        }

        uint driveIndex = (uint)(char.ToUpperInvariant(letter) - 'A');
        _logger?.LogInformation("Launching Windows native format dialog for drive {DriveLetter} (index {Index})", driveLetter, driveIndex);

        uint result = SHFormatDrive(IntPtr.Zero, driveIndex, SHFMT_ID_DEFAULT, 0);

        switch (result)
        {
            case SHFMT_CANCEL:
                _logger?.LogInformation("Format dialog cancelled by user for drive {DriveLetter}", driveLetter);
                break;
            case SHFMT_ERROR:
                _logger?.LogWarning("Format dialog encountered an error for drive {DriveLetter}", driveLetter);
                break;
            case SHFMT_NOFORMAT:
                _logger?.LogWarning("Drive {DriveLetter} is not formattable", driveLetter);
                break;
            default:
                _logger?.LogInformation("Format dialog completed successfully for drive {DriveLetter} (result: 0x{Result:X8})", driveLetter, result);
                break;
        }
    }

    /// <inheritdoc />
    public void LaunchExecutable(string fileName, string arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        _ = Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = true
        });
    }
}

