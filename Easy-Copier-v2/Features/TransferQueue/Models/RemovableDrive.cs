namespace EasyCopier.Features.TransferQueue.Models;

/// <summary>
/// Represents information about a detected removable drive or storage volume.
/// </summary>
public record RemovableDrive(
    string DriveLetter,
    string DriveLabel,
    string FileSystem,
    long TotalBytes,
    long FreeBytes,
    double UsedPercentage,
    string Brand = "Unknown")
{
    /// <summary>
    /// Gets a value indicating whether the drive uses the FAT32 file system.
    /// </summary>
    public bool IsFat32 => FileSystem.Equals("FAT32", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Represents the maximum single file size allowed on FAT32 file systems (4 GB).
    /// </summary>
    public const long Fat32MaxFileSize = 4L * 1024 * 1024 * 1024;
}

