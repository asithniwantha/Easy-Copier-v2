using EasyCopier.Features.Library.Models;
using EasyCopier.Features.Shell.Contracts;
using EasyCopier.Shared.Configuration;

namespace EasyCopier.Features.Library.Scanning;

/// <summary>
/// Defines operations for managing persistent JSON library caching and checking cache validity against directory fingerprints.
/// </summary>
public interface ILibraryCacheService
{
    /// <summary>
    /// Asynchronously loads the cached library snapshot from local disk storage if available and schema compatible.
    /// </summary>
    Task<LibraryCacheSnapshot?> LoadCacheAsync();

    /// <summary>
    /// Asynchronously saves the library cache snapshot to local disk storage using atomic write patterns.
    /// </summary>
    Task SaveCacheAsync(LibraryCacheSnapshot snapshot);

    /// <summary>
    /// Asynchronously deletes the local library cache file from disk.
    /// </summary>
    Task InvalidateCacheAsync();

    /// <summary>
    /// Validates whether the cached snapshot matches current application settings source folders and directory fingerprints.
    /// </summary>
    Task<CacheValidationOutcome> ValidateCacheAsync(
        LibraryCacheSnapshot cache,
        AppSettings currentSettings,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes a lightweight size and modification fingerprint for a given file or folder path.
    /// </summary>
    Task<ItemFingerprint> ComputeItemFingerprintAsync(
        string folderPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes item fingerprints and creates/saves a library cache snapshot from scanned items and active settings.
    /// </summary>
    Task<string?> CreateAndSaveSnapshotAsync(IEnumerable<GameEntry> allEntries, AppSettings settings);
}

