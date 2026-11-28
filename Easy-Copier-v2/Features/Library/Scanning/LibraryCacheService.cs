using System.Security;
using System.Text.Json;
using EasyCopier.Features.Library.Models;
using EasyCopier.Features.Shell.Contracts;
using EasyCopier.Shared.Configuration;
using Microsoft.Extensions.Logging;

namespace EasyCopier.Features.Library.Scanning;

/// <summary>
/// Service for serializing, deserializing, and validating library scan results stored in <c>library_cache.json</c>.
/// </summary>
public class LibraryCacheService : ILibraryCacheService
{
    private const string CacheFileName = "library_cache.json";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly ILogger<LibraryCacheService> _logger;
    private readonly string? _customCacheFilePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="LibraryCacheService"/> class.
    /// </summary>
    public LibraryCacheService(ILogger<LibraryCacheService> logger)
        : this(logger, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LibraryCacheService"/> class with a custom cache path (primarily for testing).
    /// </summary>
    public LibraryCacheService(ILogger<LibraryCacheService> logger, string? customCacheFilePath)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _customCacheFilePath = customCacheFilePath;
    }

    private string GetCacheFilePath()
    {
        if (!string.IsNullOrWhiteSpace(_customCacheFilePath))
        {
            string? dir = Path.GetDirectoryName(_customCacheFilePath);
            if (!string.IsNullOrEmpty(dir))
            {
                _ = Directory.CreateDirectory(dir);
            }

            return _customCacheFilePath;
        }

        string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string appFolder = Path.Combine(appDataFolder, "EasyCopier");
        _ = Directory.CreateDirectory(appFolder);
        return Path.Combine(appFolder, CacheFileName);
    }

    /// <inheritdoc />
    public async Task<LibraryCacheSnapshot?> LoadCacheAsync()
    {
        string cachePath = GetCacheFilePath();

        try
        {
            if (!File.Exists(cachePath))
            {
                _logger.LogInformation("Cache file not found at {Path}", cachePath);
                return null;
            }

            string json = await File.ReadAllTextAsync(cachePath).ConfigureAwait(false);
            LibraryCacheSnapshot? cache = JsonSerializer.Deserialize<LibraryCacheSnapshot>(json);

            if (cache == null)
            {
                _logger.LogWarning("Failed to deserialize cache from {Path}", cachePath);
                return null;
            }

            if (cache.SchemaVersion != LibraryCacheSnapshot.CurrentSchemaVersion)
            {
                _logger.LogInformation(
                    "Cache schema version {CacheVersion} does not match current version {CurrentVersion}. Invalidating cache.",
                    cache.SchemaVersion,
                    LibraryCacheSnapshot.CurrentSchemaVersion);
                return null;
            }

            _logger.LogInformation(
                "Cache loaded successfully: {GameCount} games, {AppCount} apps, {VideoCount} films/TV from {CachedAt}",
                cache.Games.Count,
                cache.Apps.Count,
                cache.TvAndFilms.Count,
                cache.CachedAt);

            return cache;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Cache file is corrupt or malformed at {Path}", cachePath);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or SecurityException)
        {
            _logger.LogError(ex, "Error loading cache from {Path}", cachePath);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task SaveCacheAsync(LibraryCacheSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        string cachePath = GetCacheFilePath();
        string tempPath = cachePath + ".tmp";

        try
        {
            string json = JsonSerializer.Serialize(snapshot, JsonOptions);
            await File.WriteAllTextAsync(tempPath, json).ConfigureAwait(false);

            File.Move(tempPath, cachePath, overwrite: true);

            _logger.LogInformation(
                "Cache saved successfully: {GameCount} games, {AppCount} apps, {FingerprintCount} fingerprints",
                snapshot.Games.Count,
                snapshot.Apps.Count,
                snapshot.ItemFingerprints.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving cache to {Path}", cachePath);

            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch (Exception cleanupEx) when (cleanupEx is IOException or UnauthorizedAccessException)
                {
                    // Ignore cleanup errors
                }
            }

            throw;
        }
    }

    /// <inheritdoc />
    public async Task InvalidateCacheAsync()
    {
        string cachePath = GetCacheFilePath();

        try
        {
            if (File.Exists(cachePath))
            {
                await Task.Run(() => File.Delete(cachePath)).ConfigureAwait(false);
                _logger.LogInformation("Cache invalidated at {Path}", cachePath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _logger.LogWarning(ex, "Error invalidating cache at {Path}", cachePath);
        }
    }

    /// <inheritdoc />
    public async Task<CacheValidationOutcome> ValidateCacheAsync(
        LibraryCacheSnapshot cache,
        AppSettings currentSettings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(currentSettings);

        try
        {
            List<string> normalizedCacheGameFolders = [.. (cache.GameSourceFolders ?? []).Select(NormalizePath).OrderBy(p => p)];
            List<string> normalizedCacheAppFolders = [.. (cache.AppSourceFolders ?? []).Select(NormalizePath).OrderBy(p => p)];
            List<string> normalizedCacheTvAndFilmFolders = [.. (cache.TvAndFilmSourceFolders ?? []).Select(NormalizePath).OrderBy(p => p)];

            List<string> normalizedCurrentGameFolders = [.. (currentSettings.GameSourceFolders ?? []).Select(NormalizePath).OrderBy(p => p)];
            List<string> normalizedCurrentAppFolders = [.. (currentSettings.AppSourceFolders ?? []).Select(NormalizePath).OrderBy(p => p)];
            List<string> normalizedCurrentTvAndFilmFolders = [.. (currentSettings.TvAndFilmSourceFolders ?? []).Select(NormalizePath).OrderBy(p => p)];

            if (!normalizedCacheGameFolders.SequenceEqual(normalizedCurrentGameFolders)
                || !normalizedCacheAppFolders.SequenceEqual(normalizedCurrentAppFolders)
                || !normalizedCacheTvAndFilmFolders.SequenceEqual(normalizedCurrentTvAndFilmFolders))
            {
                _logger.LogInformation("Cache configuration mismatch: source folders have changed");
                return new CacheValidationOutcome(CacheValidationResult.ConfigurationMismatch, []);
            }

            List<string> allSourceFolders = normalizedCurrentGameFolders
                .Concat(normalizedCurrentAppFolders)
                .Concat(normalizedCurrentTvAndFilmFolders)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (string sourceFolder in allSourceFolders)
            {
                if (!Directory.Exists(sourceFolder))
                {
                    _logger.LogInformation("Cache invalid: source folder no longer exists: {Path}", sourceFolder);
                    return new CacheValidationOutcome(CacheValidationResult.SourcesUnavailable, []);
                }
            }

            List<string> changedItems = [];
            List<GameEntry> allCachedItems = [.. cache.Games, .. cache.Apps, .. cache.TvAndFilms ?? []];

            foreach (GameEntry item in allCachedItems)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                string normalizedPath = NormalizePath(item.FolderPath);

                if (!cache.ItemFingerprints.TryGetValue(normalizedPath, out ItemFingerprint? cachedFingerprint))
                {
                    _logger.LogDebug("Item missing fingerprint: {Path}", item.FolderPath);
                    changedItems.Add(normalizedPath);
                    continue;
                }

                if (!Directory.Exists(item.FolderPath) && !File.Exists(item.FolderPath))
                {
                    _logger.LogDebug("Item removed: {Path}", item.FolderPath);
                    changedItems.Add(normalizedPath);
                    continue;
                }

                try
                {
                    ItemFingerprint currentFingerprint = await ComputeItemFingerprintAsync(item.FolderPath, cancellationToken).ConfigureAwait(false);
                    if (!FingerprintsMatch(cachedFingerprint, currentFingerprint))
                    {
                        _logger.LogDebug("Item changed: {Path}", item.FolderPath);
                        changedItems.Add(normalizedPath);
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    _logger.LogDebug("Item inaccessible (treating as changed): {Path}", item.FolderPath);
                    changedItems.Add(normalizedPath);
                }
                catch (Exception ex) when (ex is IOException or ArgumentException or SecurityException)
                {
                    _logger.LogWarning(ex, "Error validating item (treating as changed): {Path}", item.FolderPath);
                    changedItems.Add(normalizedPath);
                }
            }

            HashSet<string> currentTopLevelItems = new(StringComparer.OrdinalIgnoreCase);
            foreach (string sourceFolder in allSourceFolders)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    string[] subdirectories = await Task.Run(
                        () => Directory.GetDirectories(sourceFolder, "*", SearchOption.TopDirectoryOnly),
                        cancellationToken).ConfigureAwait(false);

                    foreach (string subdir in subdirectories)
                    {
                        _ = currentTopLevelItems.Add(NormalizePath(subdir));
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or SecurityException)
                {
                    _logger.LogWarning(ex, "Error enumerating source folder (invalidating cache): {Path}", sourceFolder);
                    return new CacheValidationOutcome(CacheValidationResult.ItemsChanged, []);
                }
            }

            HashSet<string> cachedTopLevelItems = allCachedItems
                .Select(item => NormalizePath(item.FolderPath))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            List<string> newItems = currentTopLevelItems.Except(cachedTopLevelItems).ToList();
            List<string> removedItems = cachedTopLevelItems.Except(currentTopLevelItems).ToList();

            if (newItems.Count > 0)
            {
                _logger.LogInformation("New items detected: {Count}", newItems.Count);
                changedItems.AddRange(newItems);
            }

            if (removedItems.Count > 0)
            {
                _logger.LogInformation("Removed items detected: {Count}", removedItems.Count);
                changedItems.AddRange(removedItems);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Cache validation cancelled");
                return new CacheValidationOutcome(CacheValidationResult.Valid, []);
            }

            if (changedItems.Count > 0)
            {
                _logger.LogInformation("Cache validation: {Count} items changed", changedItems.Count);
                return new CacheValidationOutcome(CacheValidationResult.ItemsChanged, changedItems);
            }

            _logger.LogInformation("Cache validation successful: no changes detected");
            return new CacheValidationOutcome(CacheValidationResult.Valid, []);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or SecurityException)
        {
            _logger.LogError(ex, "Error during cache validation");
            return new CacheValidationOutcome(CacheValidationResult.CorruptOrInvalid, []);
        }
    }

    /// <inheritdoc />
    public async Task<string?> CreateAndSaveSnapshotAsync(IEnumerable<GameEntry> allEntries, AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(allEntries);
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            Dictionary<string, ItemFingerprint> fingerprints = new(StringComparer.OrdinalIgnoreCase);
            List<GameEntry> entriesList = allEntries.ToList();
            string? warningMessage = null;

            foreach (GameEntry entry in entriesList)
            {
                try
                {
                    ItemFingerprint fingerprint = await ComputeItemFingerprintAsync(entry.FolderPath).ConfigureAwait(false);
                    string normalizedPath = NormalizePath(entry.FolderPath);
                    fingerprints[normalizedPath] = fingerprint;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or SecurityException)
                {
                    warningMessage = $"Warning: Could not compute fingerprint for {entry.Name}: {ex.Message}";
                    _logger.LogWarning(ex, "Could not compute fingerprint for {Name}", entry.Name);
                }
            }

            List<GameEntry> games = entriesList.Where(e => e.Category == LibraryCategory.Game).ToList();
            List<GameEntry> apps = entriesList.Where(e => e.Category == LibraryCategory.App).ToList();
            List<GameEntry> tvAndFilms = entriesList.Where(e => e.Category == LibraryCategory.TvAndFilm).ToList();

            LibraryCacheSnapshot snapshot = new(
                LibraryCacheSnapshot.CurrentSchemaVersion,
                games,
                apps,
                tvAndFilms,
                [],
                [.. settings.GameSourceFolders],
                [.. settings.AppSourceFolders],
                [.. settings.TvAndFilmSourceFolders ?? []],
                [],
                DateTime.Now,
                fingerprints);

            await SaveCacheAsync(snapshot).ConfigureAwait(false);
            return warningMessage;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException or ArgumentException or SecurityException)
        {
            _logger.LogError(ex, "Failed to save cache snapshot");
            return $"Failed to save cache: {ex.Message}";
        }
    }

    /// <inheritdoc />
    public async Task<ItemFingerprint> ComputeItemFingerprintAsync(
        string folderPath,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            long totalBytes = 0;
            DateTime latestWriteTime = DateTime.MinValue;

            if (File.Exists(folderPath))
            {
                FileInfo fileInfo = new(folderPath);
                totalBytes = fileInfo.Length;
                latestWriteTime = fileInfo.LastWriteTimeUtc;
            }
            else if (Directory.Exists(folderPath))
            {
                DirectoryInfo dirInfo = new(folderPath);
                foreach (FileInfo file in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    totalBytes += file.Length;
                    if (file.LastWriteTimeUtc > latestWriteTime)
                    {
                        latestWriteTime = file.LastWriteTimeUtc;
                    }
                }
            }

            return new ItemFingerprint(NormalizePath(folderPath), totalBytes, latestWriteTime);
        }, cancellationToken).ConfigureAwait(false);
    }

    private static bool FingerprintsMatch(ItemFingerprint cached, ItemFingerprint current)
        => cached.TotalBytes == current.TotalBytes && cached.LastWriteTimeUtc == current.LastWriteTimeUtc;

    private static string NormalizePath(string path)
        => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}

