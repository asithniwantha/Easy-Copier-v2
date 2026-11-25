namespace EasyCopier.Shared.Configuration;

/// <summary>
/// Holds application configuration and user preference settings.
/// </summary>
public class AppSettings
{
    /// <summary>Gets or sets the list of source directory paths for game libraries.</summary>
    public List<string> GameSourceFolders { get; set; } = [];

    /// <summary>Gets or sets the list of source directory paths for app libraries.</summary>
    public List<string> AppSourceFolders { get; set; } = [];

    /// <summary>Gets or sets the list of source directory paths for TV and film libraries.</summary>
    public List<string> TvAndFilmSourceFolders { get; set; } = [];

    /// <summary>Gets or sets the list of source directory paths for OS image libraries.</summary>
    public List<string> OsImageSourceFolders { get; set; } = [];

    /// <summary>Gets or sets the file path to the Rufus executable for bootable USB creation.</summary>
    public string RufusExecutablePath { get; set; } = @"%USERPROFILE%\Downloads\Programs\rufus.exe";

    /// <summary>Gets or sets comma-separated video file extension filters.</summary>
    public string VideoFileExtensions { get; set; } = ".mp4,.mkv,.avi";

    /// <summary>Gets or sets comma-separated app file extension filters.</summary>
    public string AppFileExtensions { get; set; } = ".exe,.msi,.rar,.zip";

    /// <summary>Gets or sets a value indicating whether to auto-scan library folders at application startup.</summary>
    public bool AutoScanOnStartup { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the application should launch automatically at Windows logon.</summary>
    public bool StartOnLogon { get; set; }

    /// <summary>Gets or sets a value indicating whether updates should be downloaded automatically when available.</summary>
    public bool AutoDownloadUpdates { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether sound effects play on completion/failure events.</summary>
    public bool PlayNotificationSounds { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether Windows toast notifications are enabled.</summary>
    public bool ShowDesktopNotifications { get; set; } = true;

    /// <summary>Gets or sets the drive letter of the last selected destination drive.</summary>
    public string LastSelectedDrive { get; set; } = string.Empty;

    /// <summary>Gets or sets the timestamp of the last library scan.</summary>
    public DateTime LastScanTime { get; set; } = DateTime.MinValue;

    /// <summary>Gets or sets the base price for tier 1 content (e.g., &lt; 5 GB).</summary>
    public int PriceTier1 { get; set; } = 100;

    /// <summary>Gets or sets the price for tier 2 content (e.g., 5-10 GB).</summary>
    public int PriceTier2 { get; set; } = 200;

    /// <summary>Gets or sets the price for tier 3 content (e.g., 10-16 GB).</summary>
    public int PriceTier3 { get; set; } = 300;

    /// <summary>Gets or sets the price for tier 4 content (e.g., &gt; 16 GB).</summary>
    public int PriceTier4 { get; set; } = 400;

    /// <summary>Gets or sets the post-copy data verification strategy.</summary>
    public CopyVerificationMode VerificationMode { get; set; } = CopyVerificationMode.QuickSizeAndCount;

    /// <summary>Gets or sets a value indicating whether closing the main window minimizes to the system tray when transfers are active.</summary>
    public bool MinimizeToTrayOnClose { get; set; } = true;
}

