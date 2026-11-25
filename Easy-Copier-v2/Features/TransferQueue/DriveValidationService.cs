using EasyCopier.Features.Shell.Contracts;
using EasyCopier.Features.TransferQueue.Models;
using EasyCopier.Shared.Models;
using EasyCopier.Shared.UI.Helpers;
using Microsoft.Extensions.Logging;

namespace EasyCopier.Features.TransferQueue;

/// <summary>
/// Validates transfer requirements such as available drive space, FAT32 4GB file limitations,
/// source availability, and existing destination paths.
/// </summary>
public sealed class DriveValidationService : IDriveValidationService
{
    private readonly ILogger<DriveValidationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DriveValidationService"/> class.
    /// </summary>
    public DriveValidationService(ILogger<DriveValidationService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ValidationResult>> ValidateTransferAsync(
        IEnumerable<GameEntry> games,
        RemovableDrive targetDrive,
        string destinationBasePath)
    {
        ArgumentNullException.ThrowIfNull(games);
        ArgumentNullException.ThrowIfNull(targetDrive);

        return Task.Run<IReadOnlyList<ValidationResult>>(() =>
        {
            List<ValidationResult> results = [];
            List<GameEntry> gamesList = [.. games];

            if (gamesList.Count == 0)
            {
                results.Add(new ValidationResult(false, ValidationSeverity.Error, "No games selected"));
                return results;
            }

            long totalRequiredBytes = gamesList.Sum(g => g.TotalBytes);

            if (totalRequiredBytes > targetDrive.FreeBytes)
            {
                results.Add(new ValidationResult(
                    false,
                    ValidationSeverity.Error,
                    $"Insufficient space: Need {FormattingHelpers.FormatBytes(totalRequiredBytes)}, available {FormattingHelpers.FormatBytes(targetDrive.FreeBytes)}"));
            }

            if (targetDrive.IsFat32)
            {
                List<GameEntry> gamesWithLargeFiles = gamesList.Where(g => g.HasLargeFiles).ToList();
                if (gamesWithLargeFiles.Count > 0)
                {
                    results.Add(new ValidationResult(
                        false,
                        ValidationSeverity.Error,
                        $"FAT32 drive cannot store files >4GB. {gamesWithLargeFiles.Count} game(s) affected: {string.Join(", ", gamesWithLargeFiles.Select(g => g.Name))}"));
                }
            }

            if (!string.IsNullOrWhiteSpace(destinationBasePath) && Directory.Exists(destinationBasePath))
            {
                foreach (GameEntry game in gamesList)
                {
                    bool isSingleFile = File.Exists(game.FolderPath);
                    string destPath = isSingleFile
                        ? Path.Combine(destinationBasePath, Path.GetFileName(game.FolderPath))
                        : Path.Combine(destinationBasePath, game.Name);

                    if (Directory.Exists(destPath) || File.Exists(destPath))
                    {
                        results.Add(new ValidationResult(
                            true,
                            ValidationSeverity.Warning,
                            $"'{game.Name}' already exists at destination and will be merged/overwritten"));
                    }
                }
            }

            foreach (GameEntry game in gamesList)
            {
                if (!Directory.Exists(game.FolderPath) && !File.Exists(game.FolderPath))
                {
                    _logger.LogWarning("Source path not accessible during transfer validation: {Path}", game.FolderPath);
                    results.Add(new ValidationResult(
                        false,
                        ValidationSeverity.Error,
                        $"Source not accessible: {game.Name}"));
                }
            }

            if (results.All(r => r.Severity != ValidationSeverity.Error))
            {
                results.Insert(0, new ValidationResult(
                    true,
                    ValidationSeverity.Info,
                    $"Ready to copy {gamesList.Count} game(s) ({FormattingHelpers.FormatBytes(totalRequiredBytes)})"));
            }

            return results;
        });
    }
}

