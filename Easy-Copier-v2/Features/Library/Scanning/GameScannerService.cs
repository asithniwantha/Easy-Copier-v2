using System.Collections.Concurrent;
using EasyCopier.Features.Shell.Contracts;
using Microsoft.Extensions.Logging;

namespace EasyCopier.Features.Library.Scanning;

/// <summary>
/// Service that scans local drives and network shares for games, media files, and application directories.
/// </summary>
public class GameScannerService : IGameScannerService
{
    private const long Fat32MaxFileSizeBytes = 4_294_967_295L;

    private static readonly string[] CoverImageFileNames = ["cover.jpg", "cover.png", "cover.jpeg", "folder.jpg", "folder.png"];
    private static readonly char[] ExtensionSeparators = [',', ';', ' '];

    private readonly ILogger<GameScannerService> _logger;
    private readonly IThumbnailService _thumbnailService;
    private readonly ConcurrentDictionary<string, (DateTime LastModified, long TotalSize, bool HasLargeFiles)> _folderStatsCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="GameScannerService"/> class.
    /// </summary>
    public GameScannerService(ILogger<GameScannerService> logger, IThumbnailService thumbnailService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _thumbnailService = thumbnailService ?? throw new ArgumentNullException(nameof(thumbnailService));
    }

    private static bool IsExcludedFolder(string folderPath)
    {
        try
        {
            string folderName = Path.GetFileName(folderPath).TrimEnd();

            if ((folderName.Length > 0 && folderName[0] == '$')
                || string.Equals(folderName, "recyclebin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(folderName, "System Volume Information", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            DirectoryInfo dirInfo = new(folderPath);
            return dirInfo.Exists && (dirInfo.Attributes & FileAttributes.System) == FileAttributes.System;
        }
        catch
        {
            return true;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GameEntry>> ScanLibraryAsync(
        IEnumerable<string> sourceFolders,
        LibraryCategory category,
        IProgress<string>? progress = null,
        string? videoExtensions = null,
        string? appFileExtensions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceFolders);

        List<GameEntry> entries = [];
        HashSet<string> processedPaths = new(StringComparer.OrdinalIgnoreCase);

        string categoryLabel = category switch
        {
            LibraryCategory.App => "app",
            LibraryCategory.TvAndFilm => "film/tv",
            LibraryCategory.OsImage => "OS image",
            _ => "game"
        };

        foreach (string sourceFolder in sourceFolders)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (!Directory.Exists(sourceFolder))
            {
                _logger.LogWarning("Source folder does not exist: {Path}", sourceFolder);
                progress?.Report($"Skipping inaccessible: {sourceFolder}");
                continue;
            }

            try
            {
                progress?.Report($"Scanning: {sourceFolder}");

                string[] initialSubdirectories = await Task.Run(
                    () => Directory.GetDirectories(sourceFolder, "*", SearchOption.TopDirectoryOnly),
                    cancellationToken).ConfigureAwait(false);

                if ((category == LibraryCategory.TvAndFilm && !string.IsNullOrWhiteSpace(videoExtensions)) ||
                    (category == LibraryCategory.App && !string.IsNullOrWhiteSpace(appFileExtensions)) ||
                    category == LibraryCategory.OsImage)
                {
                    List<string> extList = category switch
                    {
                        LibraryCategory.TvAndFilm => ParseExtensionList(videoExtensions),
                        LibraryCategory.App => ParseExtensionList(appFileExtensions),
                        _ => [".iso"]
                    };

                    try
                    {
                        string[] files = await Task.Run(
                            () => Directory.GetFiles(sourceFolder, "*.*", SearchOption.TopDirectoryOnly),
                            cancellationToken).ConfigureAwait(false);

                        foreach (string file in files)
                        {
                            if (!extList.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)
                                || processedPaths.Contains(file))
                            {
                                continue;
                            }

                            string fileName = Path.GetFileName(file);
                            progress?.Report($"Processing file: {fileName}");
                            FileInfo fi = new(file);
                            long totalSize = fi.Length;
                            bool hasLargeFiles = totalSize > Fat32MaxFileSizeBytes;

                            string? coverImage = null;
                            if (category == LibraryCategory.TvAndFilm)
                            {
                                string cacheDir = Path.Combine(
                                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                    "EasyCopier",
                                    "Thumbnails");
                                coverImage = await _thumbnailService.ExtractThumbnailAsync(file, cacheDir, cancellationToken).ConfigureAwait(false);
                            }

                            DateTime dateCreated = fi.LastWriteTime < fi.CreationTime && fi.LastWriteTime != DateTime.MinValue
                                ? fi.LastWriteTime
                                : fi.CreationTime;

                            entries.Add(new GameEntry(
                                Path.GetFileNameWithoutExtension(fileName),
                                file,
                                totalSize,
                                coverImage,
                                dateCreated,
                                hasLargeFiles,
                                category));

                            _ = processedPaths.Add(file);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error scanning root files in {Path}", sourceFolder);
                    }
                }

                if (category != LibraryCategory.OsImage)
                {
                    List<string> foldersToProcess = await ExpandCollectionFoldersAsync(initialSubdirectories, cancellationToken).ConfigureAwait(false);

                    foreach (string itemFolder in foldersToProcess)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }

                        if (!processedPaths.Add(itemFolder))
                        {
                            _logger.LogDebug("Duplicate {Category} folder ignored: {Path}", categoryLabel, itemFolder);
                            continue;
                        }

                        try
                        {
                            string itemName = Path.GetFileName(itemFolder);
                            progress?.Report($"Processing: {itemName}");

                            (long totalSize, bool hasLargeFiles) = await GetFolderStatsAsync(itemFolder, Fat32MaxFileSizeBytes, cancellationToken).ConfigureAwait(false);

                            string? coverImage = FindCoverImage(itemFolder);
                            if (category == LibraryCategory.TvAndFilm && coverImage == null && !string.IsNullOrWhiteSpace(videoExtensions))
                            {
                                coverImage = await TryExtractVideoFolderThumbnailAsync(itemFolder, videoExtensions, cancellationToken).ConfigureAwait(false);
                            }

                            IReadOnlyList<GameCategory> categoriesList = GetCategories(itemFolder);

                            DirectoryInfo dirInfo = new(itemFolder);
                            DateTime dateCreated = dirInfo.LastWriteTime < dirInfo.CreationTime && dirInfo.LastWriteTime != DateTime.MinValue
                                ? dirInfo.LastWriteTime
                                : dirInfo.CreationTime;

                            entries.Add(new GameEntry(
                                itemName,
                                itemFolder,
                                totalSize,
                                coverImage,
                                dateCreated,
                                hasLargeFiles,
                                category,
                                categoriesList));

                            _logger.LogInformation("Scanned {Category}: {Name}, Size: {Size} bytes", categoryLabel, itemName, totalSize);
                        }
                        catch (UnauthorizedAccessException ex)
                        {
                            _logger.LogWarning(ex, "Access denied to {Category} folder: {Path}", categoryLabel, itemFolder);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error scanning {Category} folder: {Path}", categoryLabel, itemFolder);
                        }
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Access denied to source folder: {Path}", sourceFolder);
                progress?.Report($"Access denied: {sourceFolder}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scanning source folder: {Path}", sourceFolder);
                progress?.Report($"Error scanning: {sourceFolder}");
            }
        }

        progress?.Report($"Scan complete: {entries.Count} {categoryLabel}(s) found");
        return entries;
    }

    private async Task<List<string>> ExpandCollectionFoldersAsync(string[] initialSubdirectories, CancellationToken cancellationToken)
    {
        List<string> foldersToProcess = [];

        foreach (string subdir in initialSubdirectories)
        {
            if (IsExcludedFolder(subdir))
            {
                _logger.LogDebug("Excluded folder: {Path}", subdir);
                continue;
            }

            string folderName = Path.GetFileName(subdir).TrimEnd();
            if (folderName.EndsWith("collection", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    string[] collectionSubdirectories = await Task.Run(
                        () => Directory.GetDirectories(subdir, "*", SearchOption.TopDirectoryOnly),
                        cancellationToken).ConfigureAwait(false);

                    foreach (string collSubdir in collectionSubdirectories)
                    {
                        if (!IsExcludedFolder(collSubdir))
                        {
                            foldersToProcess.Add(collSubdir);
                        }
                        else
                        {
                            _logger.LogDebug("Excluded collection subfolder: {Path}", collSubdir);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error reading collection folder: {Path}", subdir);
                }
            }
            else
            {
                foldersToProcess.Add(subdir);
            }
        }

        return foldersToProcess;
    }

    private async Task<string?> TryExtractVideoFolderThumbnailAsync(string itemFolder, string videoExtensions, CancellationToken cancellationToken)
    {
        try
        {
            List<string> extList = ParseExtensionList(videoExtensions);
            string[] allFiles = Directory.GetFiles(itemFolder, "*.*", SearchOption.TopDirectoryOnly);
            string? videoFile = allFiles.FirstOrDefault(f => extList.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase));
            if (videoFile != null)
            {
                string cacheDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "EasyCopier",
                    "Thumbnails");
                return await _thumbnailService.ExtractThumbnailAsync(videoFile, cacheDir, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract thumbnail from video in folder: {Folder}", itemFolder);
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<(long TotalSize, bool HasLargeFiles)> GetFolderStatsAsync(
        string folderPath,
        long sizeLimit,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                if (File.Exists(folderPath))
                {
                    FileInfo fileInfo = new(folderPath);
                    DateTime fileLastWrite = fileInfo.LastWriteTimeUtc;
                    if (_folderStatsCache.TryGetValue(folderPath, out (DateTime LastModified, long TotalSize, bool HasLargeFiles) cachedFile)
                        && cachedFile.LastModified == fileLastWrite)
                    {
                        return (cachedFile.TotalSize, cachedFile.HasLargeFiles);
                    }

                    long size = fileInfo.Length;
                    bool hasLarge = size > sizeLimit;
                    _folderStatsCache[folderPath] = (fileLastWrite, size, hasLarge);
                    return (size, hasLarge);
                }

                if (!Directory.Exists(folderPath))
                {
                    return (0L, false);
                }

                DirectoryInfo dirInfo = new(folderPath);
                DateTime dirLastWrite = dirInfo.LastWriteTimeUtc;
                if (_folderStatsCache.TryGetValue(folderPath, out (DateTime LastModified, long TotalSize, bool HasLargeFiles) cachedDir)
                    && cachedDir.LastModified == dirLastWrite)
                {
                    return (cachedDir.TotalSize, cachedDir.HasLargeFiles);
                }

                long totalSize = 0;
                bool hasLargeFiles = false;

                foreach (FileInfo file in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    long fileLength = file.Length;
                    totalSize += fileLength;
                    if (fileLength > sizeLimit)
                    {
                        hasLargeFiles = true;
                    }
                }

                if (!cancellationToken.IsCancellationRequested)
                {
                    _folderStatsCache[folderPath] = (dirLastWrite, totalSize, hasLargeFiles);
                }

                return (totalSize, hasLargeFiles);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting folder stats: {Path}", folderPath);
                return (0L, false);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public IReadOnlyList<GameCategory> GetCategories(string gameFolderPath)
    {
        if (File.Exists(gameFolderPath))
        {
            return [];
        }

        try
        {
            string catPath = Path.Combine(gameFolderPath, "categories.txt");
            if (File.Exists(catPath))
            {
                string[] lines = File.ReadAllLines(catPath);
                List<GameCategory> categories = [];
                foreach (string line in lines)
                {
                    if (Enum.TryParse(line.Trim(), true, out GameCategory cat))
                    {
                        categories.Add(cat);
                    }
                }

                if (categories.Count > 0)
                {
                    return categories;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error reading categories for: {Path}", gameFolderPath);
        }

        return (GameCategory[])[GameCategory.Uncategorized];
    }

    private static List<string> ParseExtensionList(string? extensions)
    {
        return string.IsNullOrWhiteSpace(extensions)
            ? []
            : extensions
                .Split(ExtensionSeparators, StringSplitOptions.RemoveEmptyEntries)
                .Select(e => e.Trim())
                .Select(e => e.Length > 0 && e[0] == '.' ? e : "." + e)
                .ToList();
    }

    /// <inheritdoc />
    public string? FindCoverImage(string gameFolderPath)
    {
        if (File.Exists(gameFolderPath))
        {
            return null;
        }

        try
        {
            foreach (string fileName in CoverImageFileNames)
            {
                string fullPath = Path.Combine(gameFolderPath, fileName);
                if (File.Exists(fullPath))
                {
                    _logger.LogDebug("Found cover image: {Path}", fullPath);
                    return fullPath;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error searching for cover image in: {Path}", gameFolderPath);
        }

        return null;
    }
}

