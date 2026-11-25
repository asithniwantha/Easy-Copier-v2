using System.IO.Hashing;
using System.Security.Cryptography;
using EasyCopier.Features.TransferQueue.Models;
using EasyCopier.Shared.Configuration;
using Microsoft.Extensions.Logging;

namespace EasyCopier.Features.TransferQueue;

/// <summary>
/// Service providing file and folder integrity verification through file presence, size checks, and streaming checksums.
/// </summary>
public sealed class VerificationService : IVerificationService
{
    private const int ChunkBufferSize = 1024 * 1024;
    private readonly ILogger<VerificationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="VerificationService"/> class.
    /// </summary>
    public VerificationService(ILogger<VerificationService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<VerificationResult> VerifyCopyAsync(
        string sourcePath,
        string destinationPath,
        CopyVerificationMode mode,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (mode == CopyVerificationMode.None)
        {
            return new VerificationResult(true);
        }

        if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(destinationPath))
        {
            return new VerificationResult(false, "Source or destination path is empty.");
        }

        try
        {
            bool isSourceFile = File.Exists(sourcePath);
            bool isSourceDir = Directory.Exists(sourcePath);

            if (!isSourceFile && !isSourceDir)
            {
                return new VerificationResult(false, $"Source path does not exist: {sourcePath}");
            }

            return isSourceFile
                ? await VerifySingleFileAsync(sourcePath, destinationPath, mode, progress, cancellationToken)
                : await VerifyDirectoryAsync(sourcePath, destinationPath, mode, progress, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Verification cancelled by user for {Dest}", destinationPath);
            return new VerificationResult(false, "Verification was cancelled by user.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during verification between {Source} and {Dest}", sourcePath, destinationPath);
            return new VerificationResult(false, $"Verification error: {ex.Message}");
        }
    }

    private static async Task<VerificationResult> VerifySingleFileAsync(
        string sourceFile,
        string destinationFile,
        CopyVerificationMode mode,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(destinationFile))
        {
            return new VerificationResult(false, $"Destination file does not exist: {destinationFile}", (string[])[Path.GetFileName(destinationFile)]);
        }

        FileInfo srcInfo = new(sourceFile);
        FileInfo dstInfo = new(destinationFile);

        if (srcInfo.Length != dstInfo.Length)
        {
            string msg = $"File size mismatch: source ({srcInfo.Length:N0} bytes) != destination ({dstInfo.Length:N0} bytes)";
            return new VerificationResult(false, msg, (string[])[Path.GetFileName(destinationFile)]);
        }

        if (mode == CopyVerificationMode.QuickSizeAndCount)
        {
            progress?.Report(100.0);
            return new VerificationResult(true);
        }

        bool matches = mode == CopyVerificationMode.FastXxHash64
            ? await CompareXxHash64Async(sourceFile, destinationFile, srcInfo.Length, progress, cancellationToken)
            : await CompareSha256Async(sourceFile, destinationFile, srcInfo.Length, progress, cancellationToken);

        progress?.Report(100.0);

        if (!matches)
        {
            string modeName = mode == CopyVerificationMode.FastXxHash64 ? "XxHash64" : "SHA-256";
            return new VerificationResult(false, $"{modeName} checksum mismatch", (string[])[Path.GetFileName(destinationFile)]);
        }

        return new VerificationResult(true);
    }

    private async Task<VerificationResult> VerifyDirectoryAsync(
        string sourceDir,
        string destinationDir,
        CopyVerificationMode mode,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(destinationDir))
        {
            return new VerificationResult(false, $"Destination directory does not exist: {destinationDir}");
        }

        string[] sourceFiles = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
        List<string> mismatchedFiles = [];

        long totalBytes = 0;
        List<(string SrcPath, string DstPath, string RelPath, long Size)> workList = new(sourceFiles.Length);

        foreach (string srcFile in sourceFiles)
        {
            string relPath = Path.GetRelativePath(sourceDir, srcFile);
            string dstFile = Path.Combine(destinationDir, relPath);
            long size = new FileInfo(srcFile).Length;
            totalBytes += size;
            workList.Add((srcFile, dstFile, relPath, size));
        }

        long processedBytes = 0;

        foreach ((string srcPath, string dstPath, string relPath, long size) in workList)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(dstPath))
            {
                mismatchedFiles.Add($"{relPath} (missing at destination)");
                processedBytes += size;
                continue;
            }

            FileInfo dstInfo = new(dstPath);
            if (size != dstInfo.Length)
            {
                mismatchedFiles.Add($"{relPath} (size mismatch: {size:N0} != {dstInfo.Length:N0} bytes)");
                processedBytes += size;
                continue;
            }

            if (mode == CopyVerificationMode.QuickSizeAndCount)
            {
                processedBytes += size;
                if (totalBytes > 0)
                {
                    progress?.Report((double)processedBytes / totalBytes * 100.0);
                }

                continue;
            }

            bool matches = mode == CopyVerificationMode.FastXxHash64
                ? await CompareXxHash64Async(srcPath, dstPath, size, null, cancellationToken)
                : await CompareSha256Async(srcPath, dstPath, size, null, cancellationToken);

            if (!matches)
            {
                string hashName = mode == CopyVerificationMode.FastXxHash64 ? "XxHash64" : "SHA-256";
                mismatchedFiles.Add($"{relPath} ({hashName} hash mismatch)");
            }

            processedBytes += size;
            if (totalBytes > 0)
            {
                progress?.Report((double)processedBytes / totalBytes * 100.0);
            }
        }

        progress?.Report(100.0);

        if (mismatchedFiles.Count > 0)
        {
            string summary = $"Integrity verification failed for {mismatchedFiles.Count} file(s).";
            _logger.LogWarning(
                "Verification failed for {Dest}: {Summary} Details: {Details}",
                destinationDir,
                summary,
                string.Join("; ", mismatchedFiles));
            return new VerificationResult(false, summary, mismatchedFiles);
        }

        _logger.LogInformation(
            "Successfully verified {Count} file(s) ({Bytes:N0} bytes) in {Dest}",
            sourceFiles.Length,
            totalBytes,
            destinationDir);
        return new VerificationResult(true);
    }

    private static Task<bool> CompareXxHash64Async(
        string srcPath,
        string dstPath,
        long fileSize,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        return CompareStreamsAsync(
            srcPath,
            dstPath,
            fileSize,
            progress,
            () => IncrementalHashHandle.CreateXxHash64(),
            cancellationToken);
    }

    private static Task<bool> CompareSha256Async(
        string srcPath,
        string dstPath,
        long fileSize,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        return CompareStreamsAsync(
            srcPath,
            dstPath,
            fileSize,
            progress,
            () => IncrementalHashHandle.CreateSha256(),
            cancellationToken);
    }

    private static async Task<bool> CompareStreamsAsync(
        string srcPath,
        string dstPath,
        long fileSize,
        IProgress<double>? progress,
        Func<IncrementalHashHandle> createHasher,
        CancellationToken cancellationToken)
    {
        using IncrementalHashHandle srcHasher = createHasher();
        using IncrementalHashHandle dstHasher = createHasher();

        byte[] srcBuf = new byte[ChunkBufferSize];
        byte[] dstBuf = new byte[ChunkBufferSize];

        using FileStream srcStream = new(srcPath, FileMode.Open, FileAccess.Read, FileShare.Read, ChunkBufferSize, useAsync: true);
        using FileStream dstStream = new(dstPath, FileMode.Open, FileAccess.Read, FileShare.Read, ChunkBufferSize, useAsync: true);

        long bytesReadTotal = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int srcRead = await srcStream.ReadAsync(srcBuf.AsMemory(0, ChunkBufferSize), cancellationToken);
            int dstRead = await dstStream.ReadAsync(dstBuf.AsMemory(0, ChunkBufferSize), cancellationToken);

            if (srcRead != dstRead)
            {
                return false;
            }

            if (srcRead == 0)
            {
                break;
            }

            srcHasher.Append(srcBuf, srcRead);
            dstHasher.Append(dstBuf, dstRead);

            bytesReadTotal += srcRead;
            if (fileSize > 0 && progress is not null)
            {
                progress.Report((double)bytesReadTotal / fileSize * 100.0);
            }
        }

        return CryptographicOperations.FixedTimeEquals(srcHasher.GetHashBytes(), dstHasher.GetHashBytes());
    }

    private sealed class IncrementalHashHandle : IDisposable
    {
        private readonly XxHash64? _xxHash;
        private readonly IncrementalHash? _sha256;

        private IncrementalHashHandle(XxHash64 xxHash) => _xxHash = xxHash;
        private IncrementalHashHandle(IncrementalHash sha256) => _sha256 = sha256;

        public static IncrementalHashHandle CreateXxHash64() => new(new XxHash64());
        public static IncrementalHashHandle CreateSha256() => new(IncrementalHash.CreateHash(HashAlgorithmName.SHA256));

        public void Append(byte[] buffer, int count)
        {
            if (_xxHash is not null)
            {
                _xxHash.Append(buffer.AsSpan(0, count));
            }
            else
            {
                _sha256?.AppendData(buffer, 0, count);
            }
        }

        public byte[] GetHashBytes()
        {
            return _xxHash is not null
                ? _xxHash.GetCurrentHash()
                : _sha256!.GetHashAndReset();
        }

        public void Dispose() => _sha256?.Dispose();
    }
}
