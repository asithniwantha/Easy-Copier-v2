using EasyCopier.Features.Shell.Contracts;
using EasyCopier.Features.TransferQueue;
using EasyCopier.Features.TransferQueue.Models;
using EasyCopier.Shared.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EasyCopier.Tests.Features.TransferQueue;

public sealed class DriveValidationServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"EasyCopierDriveValTest_{Guid.NewGuid():N}");

    public DriveValidationServiceTests()
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
    public async Task ValidateTransferAsync_WhenSelectionEmpty_ReturnsError()
    {
        DriveValidationService validator = new(NullLogger<DriveValidationService>.Instance);
        RemovableDrive drive = new("E:", "USB", "exFAT", 64_000_000_000L, 32_000_000_000L, 50.0);

        IReadOnlyList<ValidationResult> results = await validator.ValidateTransferAsync([], drive, _tempRoot);

        ValidationResult error = Assert.Single(results);
        Assert.False(error.IsValid);
        Assert.Equal(ValidationSeverity.Error, error.Severity);
        Assert.Equal("No games selected", error.Message);
    }

    [Fact]
    public async Task ValidateTransferAsync_WhenSourceMissing_ReturnsError()
    {
        DriveValidationService validator = new(NullLogger<DriveValidationService>.Instance);
        RemovableDrive drive = new("E:", "USB", "exFAT", 64_000_000_000L, 32_000_000_000L, 50.0);
        string missingPath = Path.Combine(_tempRoot, "NonExistentGame");
        GameEntry missingEntry = new("MissingGame", missingPath, 1_000_000L, null, DateTime.UtcNow, false);

        IReadOnlyList<ValidationResult> results = await validator.ValidateTransferAsync([missingEntry], drive, _tempRoot);

        Assert.Contains(results, r => r.Severity == ValidationSeverity.Error && r.Message.Contains("Source not accessible: MissingGame", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ValidateTransferAsync_WhenDestinationItemExists_EmitsWarningButRemainsValid()
    {
        string sourceFolder = Path.Combine(_tempRoot, "Src", "Hades");
        string destRoot = Path.Combine(_tempRoot, "Dest");
        string existingDest = Path.Combine(destRoot, "Hades");
        Directory.CreateDirectory(sourceFolder);
        Directory.CreateDirectory(existingDest);

        DriveValidationService validator = new(NullLogger<DriveValidationService>.Instance);
        RemovableDrive drive = new("F:", "NTFS_USB", "NTFS", 128_000_000_000L, 64_000_000_000L, 50.0);
        GameEntry entry = new("Hades", sourceFolder, 4_000_000_000L, null, DateTime.UtcNow, false);

        IReadOnlyList<ValidationResult> results = await validator.ValidateTransferAsync([entry], drive, destRoot);

        Assert.DoesNotContain(results, r => r.Severity == ValidationSeverity.Error);
        Assert.Contains(results, r => r.Severity == ValidationSeverity.Warning && r.Message.Contains("already exists at destination", StringComparison.Ordinal));
        Assert.Contains(results, r => r.Severity == ValidationSeverity.Info && r.IsValid);
    }

    [Fact]
    public async Task ValidateTransferAsync_OnFat32WithMultipleOversizedEntries_ListsAffectedEntries()
    {
        string g1 = Path.Combine(_tempRoot, "Game1");
        string g2 = Path.Combine(_tempRoot, "Game2");
        Directory.CreateDirectory(g1);
        Directory.CreateDirectory(g2);

        DriveValidationService validator = new(NullLogger<DriveValidationService>.Instance);
        RemovableDrive fat32Drive = new("G:", "FAT_USB", "FAT32", 128_000_000_000L, 100_000_000_000L, 20.0);
        List<GameEntry> entries =
        [
            new("Game1", g1, 5_000_000_000L, null, DateTime.UtcNow, HasLargeFiles: true),
            new("Game2", g2, 6_000_000_000L, null, DateTime.UtcNow, HasLargeFiles: true)
        ];

        IReadOnlyList<ValidationResult> results = await validator.ValidateTransferAsync(entries, fat32Drive, Path.Combine(_tempRoot, "Out"));

        ValidationResult fatError = Assert.Single(results, r => r.Severity == ValidationSeverity.Error);
        Assert.Contains("2 game(s) affected", fatError.Message, StringComparison.Ordinal);
        Assert.Contains("Game1", fatError.Message, StringComparison.Ordinal);
        Assert.Contains("Game2", fatError.Message, StringComparison.Ordinal);
    }
}

