using EasyCopier.Features.Shell.Contracts;
using EasyCopier.Features.TransferQueue;
using EasyCopier.Features.TransferQueue.Models;
using EasyCopier.Shared.Configuration;
using EasyCopier.Shared.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EasyCopier.Tests.Features.TransferQueue;

public sealed class DriveValidationAndVerificationTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"EasyCopierTqTest_{Guid.NewGuid():N}");

    public DriveValidationAndVerificationTests()
    {
        _ = Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task DriveValidationService_RejectsInsufficientSpaceAndFat32OversizedFiles()
    {
        string srcFolder = Path.Combine(_tempRoot, "BigGame");
        string destRoot = Path.Combine(_tempRoot, "Dest");
        Directory.CreateDirectory(srcFolder);
        Directory.CreateDirectory(destRoot);

        DriveValidationService validator = new(NullLogger<DriveValidationService>.Instance);

        GameEntry oversizedGame = new("BigGame", srcFolder, 5_000_000_000L, null, DateTime.UtcNow, HasLargeFiles: true);
        RemovableDrive smallFat32 = new("E:", "USB", "FAT32", 32_000_000_000L, 4_000_000_000L, 50.0);

        IReadOnlyList<ValidationResult> results = await validator.ValidateTransferAsync([oversizedGame], smallFat32, destRoot);

        Assert.Contains(results, r => r.Severity == ValidationSeverity.Error && r.Message.Contains("Insufficient space", StringComparison.Ordinal));
        Assert.Contains(results, r => r.Severity == ValidationSeverity.Error && r.Message.Contains("FAT32", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DriveValidationService_AcceptsAccessibleIsoFileOnExFatDrive()
    {
        string isoFile = Path.Combine(_tempRoot, "ubuntu.iso");
        File.WriteAllBytes(isoFile, new byte[2048]);
        string destRoot = Path.Combine(_tempRoot, "Dest");
        Directory.CreateDirectory(destRoot);

        DriveValidationService validator = new(NullLogger<DriveValidationService>.Instance);
        GameEntry isoEntry = new("ubuntu", isoFile, 2048, null, DateTime.UtcNow, HasLargeFiles: true, Category: LibraryCategory.OsImage);
        RemovableDrive exFatDrive = new("F:", "EXFAT", "exFAT", 64_000_000_000L, 32_000_000_000L, 50.0);

        IReadOnlyList<ValidationResult> results = await validator.ValidateTransferAsync([isoEntry], exFatDrive, destRoot);

        Assert.DoesNotContain(results, r => r.Severity == ValidationSeverity.Error);
        Assert.Contains(results, r => r.Severity == ValidationSeverity.Info && r.IsValid);
    }

    [Fact]
    public async Task VerificationService_ValidatesQuickXxHash64AndSha256AndDetectsCorruption()
    {
        string srcDir = Path.Combine(_tempRoot, "SrcDir");
        string dstDir = Path.Combine(_tempRoot, "DstDir");
        Directory.CreateDirectory(Path.Combine(srcDir, "Sub"));
        Directory.CreateDirectory(Path.Combine(dstDir, "Sub"));

        byte[] payload = Enumerable.Range(0, 4096).Select(i => (byte)(i % 251)).ToArray();
        string srcFile = Path.Combine(srcDir, "Sub", "data.bin");
        string dstFile = Path.Combine(dstDir, "Sub", "data.bin");
        await File.WriteAllBytesAsync(srcFile, payload);
        await File.WriteAllBytesAsync(dstFile, payload);

        VerificationService service = new(NullLogger<VerificationService>.Instance);

        VerificationResult quickOk = await service.VerifyCopyAsync(srcDir, dstDir, CopyVerificationMode.QuickSizeAndCount);
        Assert.True(quickOk.IsSuccess);

        VerificationResult xxOk = await service.VerifyCopyAsync(srcDir, dstDir, CopyVerificationMode.FastXxHash64);
        Assert.True(xxOk.IsSuccess);

        VerificationResult shaOk = await service.VerifyCopyAsync(srcDir, dstDir, CopyVerificationMode.FullSha256);
        Assert.True(shaOk.IsSuccess);

        // Flip 1 byte while keeping exact file size to verify hash checks catch silent corruption
        byte[] corrupted = [.. payload];
        corrupted[100] ^= 0xFF;
        await File.WriteAllBytesAsync(dstFile, corrupted);

        VerificationResult xxFail = await service.VerifyCopyAsync(srcDir, dstDir, CopyVerificationMode.FastXxHash64);
        Assert.False(xxFail.IsSuccess);
        Assert.Contains("data.bin", xxFail.MismatchedFiles![0], StringComparison.Ordinal);

        VerificationResult shaFail = await service.VerifyCopyAsync(srcDir, dstDir, CopyVerificationMode.FullSha256);
        Assert.False(shaFail.IsSuccess);
        Assert.Contains("data.bin", shaFail.MismatchedFiles![0], StringComparison.Ordinal);
    }
}

