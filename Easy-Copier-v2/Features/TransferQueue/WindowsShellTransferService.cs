using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using EasyCopier.Features.Shell.Contracts;
using EasyCopier.Features.TransferQueue.Models;
using EasyCopier.Shared.Configuration;
using EasyCopier.Shared.Interop;
using EasyCopier.Shared.Messages;
using EasyCopier.Shared.UI.Helpers;
using Microsoft.Extensions.Logging;

namespace EasyCopier.Features.TransferQueue;

/// <summary>
/// Implements file transfer operations utilizing the Windows Shell COM API (<see cref="IFileOperation"/>) on STA background threads,
/// returning granular per-item outcomes without coupling to History persistence.
/// </summary>
public sealed partial class WindowsShellTransferService : IFileTransferService
{
    private readonly ILogger<WindowsShellTransferService> _logger;
    private readonly ISettingsService _settingsService;
    private readonly IVerificationService _verificationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowsShellTransferService"/> class.
    /// </summary>
    public WindowsShellTransferService(
        ILogger<WindowsShellTransferService> logger,
        ISettingsService settingsService,
        IVerificationService verificationService)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(verificationService);

        _logger = logger;
        _settingsService = settingsService;
        _verificationService = verificationService;
    }

    /// <inheritdoc />
    public Task<(long Size, int Count)> GetFolderStatsAsync(string path)
    {
        return Task.Run(() =>
        {
            if (string.IsNullOrEmpty(path))
            {
                return (0L, 0);
            }

            try
            {
                if (File.Exists(path))
                {
                    return (new FileInfo(path).Length, 1);
                }

                if (Directory.Exists(path))
                {
                    DirectoryInfo dirInfo = new(path);
                    long size = 0;
                    int count = 0;

                    foreach (FileInfo file in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
                    {
                        size += file.Length;
                        count++;
                    }

                    return (size, count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to calculate stats for {Path}", path);
            }

            return (0L, 0);
        });
    }

    /// <inheritdoc />
    public Task<TransferOutcome> TransferGamesAsync(
        TransferRequest request,
        IProgress<TransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        TaskCompletionSource<TransferOutcome> tcs = new();

        Thread thread = new(() =>
        {
            Stopwatch batchStopwatch = Stopwatch.StartNew();

            try
            {
                if (!OperatingSystem.IsWindows())
                {
                    throw new PlatformNotSupportedException("Windows Shell file operations require Windows.");
                }

                int hr = NativeMethods.CoInitializeEx(IntPtr.Zero, NativeMethods.COINIT_APARTMENTTHREADED);
                if (hr < 0)
                {
                    _logger.LogWarning("CoInitializeEx failed with HRESULT 0x{HResult:X8}", hr);
                }

                _logger.LogInformation(
                    "Starting transfer of {Count} items to {Drive}",
                    request.Items.Count,
                    request.TargetDrive.DriveLetter);

                if (!Directory.Exists(request.DestinationPath))
                {
                    _ = Directory.CreateDirectory(request.DestinationPath);
                    _logger.LogInformation("Created destination directory: {Path}", request.DestinationPath);
                }

                int successCount = 0;
                int skippedCount = 0;
                long totalBytes = 0;
                List<string> errors = [];
                List<TransferCompletedItemDescriptor> itemResults = [];
                AppSettings settings = _settingsService.LoadSettingsSync();

                foreach (TransferItem item in request.Items)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("Transfer operation cancelled by user before copying remaining items.");
                        errors.Add("Transfer cancelled by user");
                        break;
                    }

                    Stopwatch itemStopwatch = Stopwatch.StartNew();

                    try
                    {
                        if (item?.Game is null)
                        {
                            _logger.LogWarning("Found null transfer item or game reference; skipping.");
                            continue;
                        }

                        GameEntry game = item.Game;
                        string destPath = Path.Combine(request.DestinationPath, game.Name);
                        if (File.Exists(game.FolderPath))
                        {
                            destPath = Path.Combine(request.DestinationPath, Path.GetFileName(game.FolderPath));
                        }

                        _logger.LogInformation("Copying {Game} to {Dest} with action {Action}", game.Name, destPath, item.Action);

                        if (item.Action == CopyAction.Skip)
                        {
                            skippedCount++;
                            itemStopwatch.Stop();
                            _logger.LogInformation("Skipping {Game}", game.Name);
                            itemResults.Add(new TransferCompletedItemDescriptor(
                                game.Name,
                                game.FolderPath,
                                destPath,
                                BytesTransferred: 0,
                                IsSuccess: true,
                                Amount: 0,
                                BatchAmount: 0,
                                ErrorLog: string.Empty,
                                SubFilesJson: string.Empty,
                                Duration: itemStopwatch.Elapsed,
                                StatusText: "Skipped",
                                Timestamp: DateTime.Now));
                            continue;
                        }

                        if (item.Action == CopyAction.Replace)
                        {
                            DeleteItemWithFileOperation(destPath);
                        }

                        bool result = item.Action == CopyAction.Merge && Directory.Exists(destPath) && Directory.Exists(game.FolderPath)
                            ? MergeDirectory(game.FolderPath, destPath)
                            : CopyItemWithFileOperation(game.FolderPath, request.DestinationPath, destPath, progress, game.TotalBytes, cancellationToken);

                        string errorMsg = string.Empty;

                        if (result && settings.VerificationMode != CopyVerificationMode.None && !cancellationToken.IsCancellationRequested)
                        {
                            _logger.LogInformation("Verifying copied files for {Game} with mode {Mode}", game.Name, settings.VerificationMode);
                            Progress<double> verifyProgress = new(p =>
                            {
                                progress?.Report(new TransferProgress(
                                    Percentage: p,
                                    SpeedText: "Verifying...",
                                    RemainingTimeText: $"{Math.Round(p)}%"));
                            });

                            VerificationResult verifyResult = _verificationService.VerifyCopyAsync(
                                game.FolderPath,
                                destPath,
                                settings.VerificationMode,
                                verifyProgress,
                                cancellationToken).GetAwaiter().GetResult();

                            if (!verifyResult.IsSuccess)
                            {
                                result = false;
                                errorMsg = verifyResult.ErrorMessage ?? "Data verification failed";
                                if (verifyResult.MismatchedFiles is { Count: > 0 })
                                {
                                    errorMsg += $" ({string.Join(", ", verifyResult.MismatchedFiles)})";
                                }

                                _logger.LogWarning("Post-copy verification failed for {Game}: {Error}", game.Name, errorMsg);
                            }
                            else
                            {
                                _logger.LogInformation("Post-copy verification succeeded for {Game}", game.Name);
                            }
                        }

                        string subFilesJson = CaptureSubFilesJson(game.FolderPath);

                        if (result)
                        {
                            successCount++;
                            totalBytes += game.TotalBytes;
                            _logger.LogInformation("Successfully copied: {Game}", game.Name);
                        }
                        else
                        {
                            if (string.IsNullOrEmpty(errorMsg))
                            {
                                errorMsg = "Copy operation was cancelled or failed";
                            }

                            errors.Add($"{game.Name}: {errorMsg}");
                            _logger.LogWarning("Copy failed or cancelled: {Game}", game.Name);
                        }

                        itemStopwatch.Stop();
                        int itemAmount = result ? FormattingHelpers.CalculatePrice(game.TotalBytes, settings) : 0;

                        itemResults.Add(new TransferCompletedItemDescriptor(
                            game.Name,
                            game.FolderPath,
                            destPath,
                            BytesTransferred: result ? game.TotalBytes : 0,
                            IsSuccess: result,
                            Amount: itemAmount,
                            BatchAmount: 0,
                            ErrorLog: errorMsg,
                            SubFilesJson: subFilesJson,
                            Duration: itemStopwatch.Elapsed,
                            StatusText: result ? "Completed" : "Failed",
                            Timestamp: DateTime.Now));
                    }
                    catch (Exception ex)
                    {
                        itemStopwatch.Stop();
                        string errorMsg = ex.Message;
                        string itemName = item?.Game?.Name ?? "Unknown item";
                        errors.Add($"{itemName}: {errorMsg}");
                        _logger.LogError(ex, "Error copying item: {Item}", itemName);

                        string srcPath = item?.Game?.FolderPath ?? string.Empty;
                        string destPath = Path.Combine(request.DestinationPath, itemName);

                        itemResults.Add(new TransferCompletedItemDescriptor(
                            itemName,
                            srcPath,
                            destPath,
                            BytesTransferred: 0,
                            IsSuccess: false,
                            Amount: 0,
                            BatchAmount: 0,
                            ErrorLog: errorMsg,
                            SubFilesJson: string.Empty,
                            Duration: itemStopwatch.Elapsed,
                            StatusText: "Failed",
                            Timestamp: DateTime.Now));
                    }
                }

                batchStopwatch.Stop();

                int totalBatchAmount = itemResults.Sum(r => r.Amount);
                List<TransferCompletedItemDescriptor> finalizedResults = itemResults
                    .Select(r => r with { BatchAmount = totalBatchAmount })
                    .ToList();

                bool allSuccess = (successCount + skippedCount) == request.Items.Count && errors.Count == 0;
                string message = allSuccess
                    ? $"Successfully copied {successCount} item(s)"
                    : $"Copied {successCount} of {request.Items.Count} items. Errors: {string.Join("; ", errors)}";

                _logger.LogInformation(
                    "Transfer completed. Success: {SuccessCount}, Skipped: {SkippedCount}, Total Bytes: {TotalBytes}, Errors: {ErrorCount}",
                    successCount,
                    skippedCount,
                    totalBytes,
                    errors.Count);

                tcs.SetResult(new TransferOutcome(
                    allSuccess,
                    message,
                    successCount,
                    totalBytes,
                    DateTime.Now,
                    batchStopwatch.Elapsed,
                    finalizedResults));
            }
            catch (Exception ex)
            {
                batchStopwatch.Stop();
                _logger.LogError(ex, "Transfer failed");
                tcs.SetResult(new TransferOutcome(
                    false,
                    $"Transfer failed: {ex.Message}",
                    0,
                    0,
                    DateTime.Now,
                    batchStopwatch.Elapsed));
            }
        })
        {
            IsBackground = true
        };

        if (OperatingSystem.IsWindows())
        {
            thread.SetApartmentState(ApartmentState.STA);
        }

        thread.Start();
        return tcs.Task;
    }

    private string CaptureSubFilesJson(string sourcePath)
    {
        try
        {
            if (Directory.Exists(sourcePath))
            {
                string[] files = Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories);
                return JsonSerializer.Serialize(files);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to scan source directory for sub-files: {Path}", sourcePath);
        }

        return string.Empty;
    }

    private bool MergeDirectory(string sourceDir, string destDir)
    {
        try
        {
            DirectoryInfo dir = new(sourceDir);
            if (!dir.Exists)
            {
                return false;
            }

            DirectoryInfo[] dirs = dir.GetDirectories();
            _ = Directory.CreateDirectory(destDir);

            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destDir, file.Name);
                if (!File.Exists(targetFilePath))
                {
                    _ = file.CopyTo(targetFilePath, false);
                }
            }

            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destDir, subDir.Name);
                if (!MergeDirectory(subDir.FullName, newDestinationDir))
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Merge operation failed for {Source}", sourceDir);
            return false;
        }
    }

    private void DeleteItemWithFileOperation(string targetPath)
    {
        if (!OperatingSystem.IsWindows() || (!Directory.Exists(targetPath) && !File.Exists(targetPath)))
        {
            return;
        }

        object? fileOpObj = null;
        IShellItem? targetItem = null;

        try
        {
            Type? type = Type.GetTypeFromCLSID(new Guid(FileOperationInterop.CLSID_FileOperation));
            if (type is null)
            {
                throw new InvalidOperationException("Could not get type from CLSID");
            }

            fileOpObj = Activator.CreateInstance(type) ?? throw new InvalidOperationException("Could not create IFileOperation instance");
            IFileOperation fileOp = (IFileOperation)fileOpObj;

            FILEOP_FLAGS flags = FILEOP_FLAGS.FOF_NOCONFIRMATION | FILEOP_FLAGS.FOFX_SHOWELEVATIONPROMPT;
            _ = fileOp.SetOperationFlags(flags);

            FileOperationInterop.SHCreateItemFromParsingName(targetPath, IntPtr.Zero, typeof(IShellItem).GUID, out targetItem);
            _ = fileOp.DeleteItem(targetItem, null);
            _ = fileOp.PerformOperations();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete existing destination for replacement: {Dest}", targetPath);
        }
        finally
        {
            if (targetItem is not null)
            {
                _ = Marshal.ReleaseComObject(targetItem);
            }

            if (fileOpObj is not null && Marshal.IsComObject(fileOpObj))
            {
                _ = Marshal.ReleaseComObject(fileOpObj);
            }
        }
    }

    private bool CopyItemWithFileOperation(
        string sourcePath,
        string destFolder,
        string destPath,
        IProgress<TransferProgress>? progress,
        long totalBytes,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        object? fileOpObj = null;
        IFileOperation? fileOp = null;
        IShellItem? sourceItem = null;
        IShellItem? destFolderItem = null;
        uint cookie = 0;

        try
        {
            Type? type = Type.GetTypeFromCLSID(new Guid(FileOperationInterop.CLSID_FileOperation));
            if (type is null)
            {
                throw new InvalidOperationException("Could not get type from CLSID");
            }

            fileOpObj = Activator.CreateInstance(type) ?? throw new InvalidOperationException("Could not create IFileOperation instance");
            fileOp = (IFileOperation)fileOpObj;

            FILEOP_FLAGS flags = FILEOP_FLAGS.FOF_NOCONFIRMMKDIR | FILEOP_FLAGS.FOFX_SHOWELEVATIONPROMPT;
            _ = fileOp.SetOperationFlags(flags);

            IFileOperationProgressSink sink = new FileOperationProgressSink(progress, totalBytes, cancellationToken);
            _ = fileOp.Advise(sink, out cookie);

            FileOperationInterop.SHCreateItemFromParsingName(sourcePath, IntPtr.Zero, typeof(IShellItem).GUID, out sourceItem);
            FileOperationInterop.SHCreateItemFromParsingName(destFolder, IntPtr.Zero, typeof(IShellItem).GUID, out destFolderItem);

            string destName = Path.GetFileName(destPath);
            _ = fileOp.CopyItem(sourceItem, destFolderItem, destName, null);

            uint hr = fileOp.PerformOperations();
            _ = fileOp.GetAnyOperationsAborted(out bool aborted);

            const uint COPYENGINE_E_USER_CANCELLED = 0x80270000;
            bool isFailed = hr is >= 0x80000000 and not COPYENGINE_E_USER_CANCELLED;

            return !(aborted || hr == COPYENGINE_E_USER_CANCELLED || isFailed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "COM copy operation failed for {Source}", sourcePath);
            return false;
        }
        finally
        {
            if (fileOp is not null && cookie != 0)
            {
                _ = fileOp.Unadvise(cookie);
            }

            if (sourceItem is not null)
            {
                _ = Marshal.ReleaseComObject(sourceItem);
            }

            if (destFolderItem is not null)
            {
                _ = Marshal.ReleaseComObject(destFolderItem);
            }

            if (fileOpObj is not null && Marshal.IsComObject(fileOpObj))
            {
                _ = Marshal.ReleaseComObject(fileOpObj);
            }
        }
    }

    private static partial class NativeMethods
    {
        public const uint COINIT_APARTMENTTHREADED = 0x2;

        [LibraryImport("ole32.dll")]
        public static partial int CoInitializeEx(IntPtr pvReserved, uint dwCoInit);
    }
}

