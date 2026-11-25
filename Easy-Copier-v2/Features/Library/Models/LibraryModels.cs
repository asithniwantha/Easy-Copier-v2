using EasyCopier.Features.Shell.Contracts;

namespace EasyCopier.Features.Library.Models;

/// <summary>
/// Defines sort options available for TV and film items.
/// </summary>
public enum TvAndFilmsSortOption
{
    /// <summary>Sort by name/title alphabetically.</summary>
    Name,

    /// <summary>Sort by recently downloaded or creation date.</summary>
    DateCreated
}

/// <summary>
/// Represents a unique file system item state fingerprint used for cache validity checks.
/// </summary>
public sealed record ItemFingerprint(
    string RelativePath,
    long TotalBytes,
    DateTime LastWriteTimeUtc);

/// <summary>
/// Represents a serialized snapshot of cached library entries and metadata.
/// </summary>
public sealed record LibraryCacheSnapshot(
    int SchemaVersion,
    List<GameEntry> Games,
    List<GameEntry> Apps,
    List<GameEntry> TvAndFilms,
    List<GameEntry> OsImages,
    List<string> GameSourceFolders,
    List<string> AppSourceFolders,
    List<string> TvAndFilmSourceFolders,
    List<string> OsImageSourceFolders,
    DateTime CachedAt,
    Dictionary<string, ItemFingerprint> ItemFingerprints)
{
    /// <summary>The current schema version constant for library cache validation.</summary>
    public const int CurrentSchemaVersion = 4;
}

/// <summary>
/// Specifies the validation result status when verifying a library cache snapshot.
/// </summary>
public enum CacheValidationResult
{
    /// <summary>Cache is fully valid and up-to-date.</summary>
    Valid,

    /// <summary>Cache configuration does not match current source folder settings.</summary>
    ConfigurationMismatch,

    /// <summary>One or more source folders are unavailable or unreachable.</summary>
    SourcesUnavailable,

    /// <summary>Library items have been modified, added, or deleted since cache creation.</summary>
    ItemsChanged,

    /// <summary>No existing cache snapshot was found.</summary>
    CacheNotFound,

    /// <summary>The cache file is corrupt or unparseable.</summary>
    CorruptOrInvalid
}

/// <summary>
/// Holds the detailed outcome of a cache validation check.
/// </summary>
public sealed record CacheValidationOutcome(
    CacheValidationResult Result,
    List<string> ChangedItems);

/// <summary>
/// Represents a single copy of an item discovered across monitored source libraries.
/// </summary>
public sealed record DuplicateItemEntry(
    string Name,
    string FolderPath,
    LibraryCategory Category,
    long TotalBytes,
    DateTime DateCreated);

/// <summary>
/// Represents a cluster of duplicate item entries sharing the same logical title across libraries.
/// </summary>
public sealed record DuplicateGroup(
    string ItemName,
    IReadOnlyList<DuplicateItemEntry> Entries);

/// <summary>
/// Represents the summary result of a safe file or folder recycle operation.
/// </summary>
public sealed record SafeDeletionResult(
    int SucceededCount,
    int FailedCount,
    long RecoveredBytes,
    IReadOnlyList<string> Errors);

