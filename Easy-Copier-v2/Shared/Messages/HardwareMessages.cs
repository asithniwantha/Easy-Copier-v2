namespace EasyCopier.Shared.Messages;

/// <summary>
/// Cross-slice message broadcast when a removable storage volume connects, disconnects, or updates capacity.
/// </summary>
public sealed record DriveConnectionChangedMessage(
    string DriveLetter,
    bool IsConnected,
    long FreeSpaceBytes,
    long TotalCapacityBytes);
