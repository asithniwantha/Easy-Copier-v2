using System.Diagnostics;
using System.Globalization;
using EasyCopier.Features.TransferQueue.Models;
using EasyCopier.Shared.Interop;
using EasyCopier.Shared.UI.Helpers;

namespace EasyCopier.Features.TransferQueue;

/// <summary>
/// Receives progress updates from Windows Shell <see cref="IFileOperation"/> and reports transfer status and speed metrics.
/// </summary>
public sealed class FileOperationProgressSink : IFileOperationProgressSink
{
    private readonly IProgress<TransferProgress>? _progress;
    private readonly long _totalBytes;
    private readonly CancellationToken _cancellationToken;
    private readonly Stopwatch _stopwatch;
    private long _bytesTransferred;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileOperationProgressSink"/> class.
    /// </summary>
    public FileOperationProgressSink(
        IProgress<TransferProgress>? progress,
        long totalBytes,
        CancellationToken cancellationToken = default)
    {
        _progress = progress;
        _totalBytes = totalBytes;
        _cancellationToken = cancellationToken;
        _stopwatch = new Stopwatch();
    }

    /// <inheritdoc />
    public uint StartOperations()
    {
        _stopwatch.Start();
        return 0;
    }

    /// <inheritdoc />
    public uint FinishOperations(uint hrResult)
    {
        _stopwatch.Stop();
        _progress?.Report(new TransferProgress(100.0, string.Empty, "Completed"));
        return 0;
    }

    /// <inheritdoc />
    public uint PreRenameItem(uint dwFlags, IShellItem psiItem, string pszNewName) => 0;

    /// <inheritdoc />
    public uint PostRenameItem(uint dwFlags, IShellItem psiItem, string pszNewName, uint hrRename, IShellItem psiNewlyCreated) => 0;

    /// <inheritdoc />
    public uint PreMoveItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, string pszNewName) => 0;

    /// <inheritdoc />
    public uint PostMoveItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, string pszNewName, uint hrMove, IShellItem psiNewlyCreated) => 0;

    /// <inheritdoc />
    public uint PreCopyItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, string pszNewName)
    {
        if (_cancellationToken.IsCancellationRequested)
        {
            const uint COPYENGINE_E_USER_CANCELLED = 0x80270000;
            return COPYENGINE_E_USER_CANCELLED;
        }

        return 0;
    }

    /// <inheritdoc />
    public uint PostCopyItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, string pszNewName, uint hrCopy, IShellItem psiNewlyCreated) => 0;

    /// <inheritdoc />
    public uint PreDeleteItem(uint dwFlags, IShellItem psiItem) => 0;

    /// <inheritdoc />
    public uint PostDeleteItem(uint dwFlags, IShellItem psiItem, uint hrDelete, IShellItem psiNewlyCreated) => 0;

    /// <inheritdoc />
    public uint PreNewItem(uint dwFlags, IShellItem psiDestinationFolder, string pszNewName) => 0;

    /// <inheritdoc />
    public uint PostNewItem(uint dwFlags, IShellItem psiDestinationFolder, string pszNewName, string pszTemplateName, uint hrNew, IShellItem psiNewlyCreated) => 0;

    /// <inheritdoc />
    public uint UpdateProgress(uint iWorkTotal, uint iWorkSoFar)
    {
        if (_cancellationToken.IsCancellationRequested)
        {
            const uint COPYENGINE_E_USER_CANCELLED = 0x80270000;
            return COPYENGINE_E_USER_CANCELLED;
        }

        if (_progress is not null && iWorkTotal > 0 && _totalBytes > 0)
        {
            double fraction = (double)iWorkSoFar / iWorkTotal;
            _bytesTransferred = (long)(fraction * _totalBytes);
            double percentage = fraction * 100.0;

            double elapsedSeconds = _stopwatch.Elapsed.TotalSeconds;
            string speedText = "Calculating...";
            string remainingTimeText = "Calculating...";

            if (elapsedSeconds > 0)
            {
                double bytesPerSecond = _bytesTransferred / elapsedSeconds;
                speedText = FormattingHelpers.FormatBytes((long)bytesPerSecond) + "/s";

                if (bytesPerSecond > 0)
                {
                    long remainingBytes = _totalBytes - _bytesTransferred;
                    double remainingSeconds = remainingBytes / bytesPerSecond;
                    TimeSpan remainingTime = TimeSpan.FromSeconds(remainingSeconds);
                    remainingTimeText = remainingTime.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
                }
            }

            _progress.Report(new TransferProgress(percentage, speedText, remainingTimeText));
        }

        return 0;
    }

    /// <inheritdoc />
    public uint ResetTimer() => 0;

    /// <inheritdoc />
    public uint PauseTimer() => 0;

    /// <inheritdoc />
    public uint ResumeTimer() => 0;
}

