using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.Messaging;
using EasyCopier.Features.TransferQueue.Models;
using EasyCopier.Shared.Messages;
using Microsoft.Extensions.Logging;

namespace EasyCopier.Features.TransferQueue;

/// <summary>
/// Provides physical and logical drive detection, real-time drive insertion/removal monitoring via WMI,
/// and cross-slice notifications via <see cref="DriveConnectionChangedMessage"/>.
/// </summary>
public sealed class DriveDiscoveryService : IDriveDiscoveryService
{
    private const ushort UsbBusType = 7;

    private readonly ILogger<DriveDiscoveryService> _logger;
    private readonly IMessenger _messenger;
    private readonly object _watcherLock = new();
    private readonly ManualResetEventSlim _driveChangeCallbacksCompleted = new(initialState: true);
    private Dictionary<string, RemovableDrive> _knownDrives = new(StringComparer.OrdinalIgnoreCase);

    private ManagementEventWatcher? _driveWatcher;
    private int _activeDriveChangeCallbacks;
    private int _isWatching;
    private int _isDisposed;

    /// <inheritdoc />
    public event EventHandler? DrivesChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="DriveDiscoveryService"/> class.
    /// </summary>
    public DriveDiscoveryService(ILogger<DriveDiscoveryService> logger, IMessenger messenger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(messenger);

        _logger = logger;
        _messenger = messenger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RemovableDrive>> GetRemovableDrivesAsync()
    {
        IReadOnlyList<RemovableDrive> drives = await Task.Run(() =>
        {
            List<RemovableDrive> removableDrives = [];

            try
            {
                DriveInfo[] allDrives = DriveInfo.GetDrives();

                foreach (DriveInfo drive in allDrives)
                {
                    try
                    {
                        if (!drive.IsReady)
                        {
                            continue;
                        }

                        string driveLetterWithColon = drive.Name.TrimEnd('\\');
                        (string? model, bool isUsb) = GetPhysicalDiskInfo(driveLetterWithColon);

                        bool isEligible = drive.DriveType == DriveType.Removable
                            || (drive.DriveType == DriveType.Fixed && isUsb);

                        if (!isEligible)
                        {
                            continue;
                        }

                        long usedBytes = drive.TotalSize - drive.AvailableFreeSpace;
                        double usedPercentage = drive.TotalSize > 0
                            ? (double)usedBytes / drive.TotalSize * 100
                            : 0;

                        string brand = model ?? "Unknown";

                        RemovableDrive removableDrive = new(
                            driveLetterWithColon,
                            string.IsNullOrWhiteSpace(drive.VolumeLabel) ? "Removable Drive" : drive.VolumeLabel,
                            drive.DriveFormat,
                            drive.TotalSize,
                            drive.AvailableFreeSpace,
                            usedPercentage,
                            brand);

                        removableDrives.Add(removableDrive);

                        _logger.LogInformation(
                            "Found removable drive: {Letter} ({Label}), {Format}, {Brand}, {Free} free of {Total}",
                            drive.Name, drive.VolumeLabel, drive.DriveFormat, brand,
                            drive.AvailableFreeSpace, drive.TotalSize);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ManagementException or COMException or InvalidOperationException)
                    {
                        _logger.LogWarning(ex, "Error reading drive: {Name}", drive.Name);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException or ManagementException or COMException)
            {
                _logger.LogError(ex, "Error enumerating drives");
            }

            return (IReadOnlyList<RemovableDrive>)removableDrives;
        });

        PublishConnectionChanges(drives);
        return drives;
    }

    private void PublishConnectionChanges(IReadOnlyList<RemovableDrive> currentDrives)
    {
        Dictionary<string, RemovableDrive> currentMap = currentDrives.ToDictionary(d => d.DriveLetter, StringComparer.OrdinalIgnoreCase);

        foreach ((string letter, RemovableDrive previous) in _knownDrives)
        {
            if (!currentMap.ContainsKey(letter))
            {
                _messenger.Send(new DriveConnectionChangedMessage(letter, false, 0, previous.TotalBytes));
            }
        }

        foreach (RemovableDrive drive in currentDrives)
        {
            if (!_knownDrives.TryGetValue(drive.DriveLetter, out RemovableDrive? previous)
                || previous.FreeBytes != drive.FreeBytes
                || previous.TotalBytes != drive.TotalBytes)
            {
                _messenger.Send(new DriveConnectionChangedMessage(drive.DriveLetter, true, drive.FreeBytes, drive.TotalBytes));
            }
        }

        _knownDrives = currentMap;
    }

    private (string? Model, bool IsUsb) GetPhysicalDiskInfo(string driveLetterWithColon)
    {
        try
        {
            string escapedLetter = driveLetterWithColon.Replace("\\", "\\\\", StringComparison.Ordinal);

            using ManagementObjectSearcher partitionSearcher = new(
                $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{escapedLetter}'}} WHERE AssocClass = Win32_LogicalDiskToPartition");

            foreach (ManagementObject partition in partitionSearcher.Get())
            {
                string? partitionDeviceId = partition["DeviceID"]?.ToString();
                if (string.IsNullOrEmpty(partitionDeviceId))
                {
                    continue;
                }

                string escapedPartitionId = partitionDeviceId.Replace("\\", "\\\\", StringComparison.Ordinal);

                using ManagementObjectSearcher diskSearcher = new(
                    $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{escapedPartitionId}'}} WHERE AssocClass = Win32_DiskDriveToDiskPartition");

                foreach (ManagementObject disk in diskSearcher.Get())
                {
                    string? model = disk["Model"]?.ToString();
                    string? interfaceType = disk["InterfaceType"]?.ToString();
                    bool isUsb = string.Equals(interfaceType, "USB", StringComparison.OrdinalIgnoreCase);

                    if (!isUsb && disk["Index"] is not null)
                    {
                        isUsb = IsUsbBusType(disk["Index"].ToString());
                    }

                    return (string.IsNullOrWhiteSpace(model) ? null : model.Trim(), isUsb);
                }
            }
        }
        catch (Exception ex) when (ex is ManagementException or COMException or UnauthorizedAccessException or PlatformNotSupportedException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Error querying physical disk info for {Letter}", driveLetterWithColon);
        }

        return (null, false);
    }

    private bool IsUsbBusType(string? diskIndex)
    {
        if (string.IsNullOrEmpty(diskIndex))
        {
            return false;
        }

        try
        {
            ManagementScope scope = new(@"root\Microsoft\Windows\Storage");
            scope.Connect();

            using ManagementObjectSearcher searcher = new(
                scope,
                new ObjectQuery($"SELECT BusType FROM MSFT_PhysicalDisk WHERE DeviceId = '{diskIndex}'"));

            foreach (ManagementObject physicalDisk in searcher.Get())
            {
                ushort busType = Convert.ToUInt16(physicalDisk["BusType"], CultureInfo.InvariantCulture);
                return busType == UsbBusType;
            }
        }
        catch (Exception ex) when (ex is ManagementException or COMException or UnauthorizedAccessException or PlatformNotSupportedException or FormatException or InvalidCastException or OverflowException)
        {
            _logger.LogWarning(ex, "Error querying MSFT_PhysicalDisk BusType for disk index {Index}", diskIndex);
        }

        return false;
    }

    /// <inheritdoc />
    public void StartWatching()
    {
        lock (_watcherLock)
        {
            if (_isWatching != 0 || _isDisposed != 0)
            {
                return;
            }

            try
            {
                WqlEventQuery query = new("SELECT * FROM Win32_VolumeChangeEvent WHERE EventType = 2 OR EventType = 3");
                _driveWatcher = new ManagementEventWatcher(query);
                _driveWatcher.EventArrived += OnDriveChanged;
                _driveWatcher.Start();
                _isWatching = 1;

                _logger.LogInformation("Drive watcher started");
            }
            catch (Exception ex) when (ex is ManagementException or COMException or UnauthorizedAccessException or PlatformNotSupportedException or InvalidOperationException)
            {
                _logger.LogError(ex, "Failed to start drive watcher");
            }
        }
    }

    /// <inheritdoc />
    public void StopWatching()
    {
        ManagementEventWatcher? watcherToDispose;

        lock (_watcherLock)
        {
            if (_isWatching == 0 || _driveWatcher is null)
            {
                return;
            }

            watcherToDispose = _driveWatcher;
            _driveWatcher = null;
            _isWatching = 0;
            watcherToDispose.EventArrived -= OnDriveChanged;
        }

        try
        {
            watcherToDispose.Stop();
        }
        catch (Exception ex) when (ex is ManagementException or COMException or InvalidOperationException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error stopping drive watcher");
        }
        finally
        {
            WaitForPendingDriveNotifications();
            watcherToDispose.Dispose();
            _logger.LogInformation("Drive watcher stopped");
        }
    }

    private void OnDriveChanged(object sender, EventArrivedEventArgs e)
    {
        if (Volatile.Read(ref _isWatching) == 0 || Volatile.Read(ref _isDisposed) != 0)
        {
            return;
        }

        if (Interlocked.Increment(ref _activeDriveChangeCallbacks) == 1)
        {
            _driveChangeCallbacksCompleted.Reset();
        }

        try
        {
            if (Volatile.Read(ref _isWatching) == 0 || Volatile.Read(ref _isDisposed) != 0)
            {
                return;
            }

            _logger.LogInformation("Drive change detected");
            DrivesChanged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            if (Interlocked.Decrement(ref _activeDriveChangeCallbacks) == 0)
            {
                _driveChangeCallbacksCompleted.Set();
            }
        }
    }

    private void WaitForPendingDriveNotifications()
    {
        if (!_driveChangeCallbacksCompleted.Wait(TimeSpan.FromSeconds(2)))
        {
            _logger.LogWarning("Timed out waiting for pending drive watcher callbacks to finish.");
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        StopWatching();
        _driveChangeCallbacksCompleted.Dispose();
    }
}

