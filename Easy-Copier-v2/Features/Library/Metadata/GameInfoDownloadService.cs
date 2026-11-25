using System.Net;
using System.Text.RegularExpressions;
using EasyCopier.Features.Library.Scanning;
using EasyCopier.Features.Shell.Contracts;
using Microsoft.Extensions.Logging;

namespace EasyCopier.Features.Library.Metadata;

/// <summary>
/// Service that downloads game system requirements, cover images, and category metadata from various web APIs and web pages.
/// </summary>
public sealed partial class GameInfoDownloadService : IGameInfoDownloadService, IGameMetadataDownloader, IDisposable
{
    private readonly ILogger<GameInfoDownloadService> _logger;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameInfoDownloadService"/> class.
    /// </summary>
    public GameInfoDownloadService(ILogger<GameInfoDownloadService> logger, HttpClient? httpClient = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        if (httpClient != null)
        {
            _httpClient = httpClient;
            _ownsClient = false;
        }
        else
        {
            SocketsHttpHandler? handler = null;
            try
            {
                handler = new SocketsHttpHandler
                {
                    PooledConnectionLifetime = TimeSpan.FromMinutes(15),
                    AutomaticDecompression = DecompressionMethods.All
                };
                _httpClient = new HttpClient(handler, disposeHandler: true);
                handler = null;
            }
            finally
            {
                handler?.Dispose();
            }

            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            _ownsClient = true;
        }
    }

    /// <inheritdoc cref="IGameInfoDownloadService.DownloadGameInfoAsync" />
    public async Task DownloadGameInfoAsync(
        IEnumerable<string> sourceFolders,
        IProgress<string>? progress = null,
        bool overwriteExistingCovers = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceFolders);

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

                List<string> foldersToProcess = [];
                foreach (string subdir in initialSubdirectories)
                {
                    string folderName = Path.GetFileName(subdir).TrimEnd();
                    if (folderName.EndsWith("collection", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            string[] collectionSubdirectories = await Task.Run(
                                () => Directory.GetDirectories(subdir, "*", SearchOption.TopDirectoryOnly),
                                cancellationToken).ConfigureAwait(false);
                            foldersToProcess.AddRange(collectionSubdirectories);
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

                int idx = 1;
                foreach (string gameFolder in foldersToProcess)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    string gameName = Path.GetFileName(gameFolder);
                    progress?.Report($"[{idx}/{foldersToProcess.Count}] Processing: {gameName}");

                    await ProcessGameAsync(gameName, gameFolder, overwriteExistingCovers, cancellationToken).ConfigureAwait(false);
                    idx++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing source folder: {Path}", sourceFolder);
                progress?.Report($"Error processing: {sourceFolder}");
            }
        }

        progress?.Report("Download complete");
    }

    /// <inheritdoc />
    public async Task<bool> DownloadCoverForGameAsync(
        string gameFolder,
        bool overwrite = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(gameFolder) || !Directory.Exists(gameFolder))
        {
            return false;
        }

        string gameName = Path.GetFileName(gameFolder);
        await DownloadCoverAsync(gameName, gameFolder, overwrite, cancellationToken).ConfigureAwait(false);

        string coverPath = Path.Combine(gameFolder, "cover.jpg");
        return File.Exists(coverPath) && new FileInfo(coverPath).Length >= 1500;
    }

    private async Task ProcessGameAsync(string gameName, string gameFolder, bool overwriteExistingCovers, CancellationToken cancellationToken)
    {
        await DownloadRequirementsAsync(gameName, gameFolder, cancellationToken).ConfigureAwait(false);
        await DownloadCoverAsync(gameName, gameFolder, overwriteExistingCovers, cancellationToken).ConfigureAwait(false);
        await DownloadCategoriesAsync(gameName, gameFolder, overwriteExistingCovers, cancellationToken).ConfigureAwait(false);
    }

    private async Task DownloadRequirementsAsync(string gameName, string gameFolder, CancellationToken cancellationToken)
    {
        string reqFile = Path.Combine(gameFolder, "system_requirements.txt");
        if (File.Exists(reqFile))
        {
            string content = await File.ReadAllTextAsync(reqFile, cancellationToken).ConfigureAwait(false);
            if (content.Contains("Steam Web API", StringComparison.Ordinal))
            {
                return;
            }
        }

        try
        {
            Dictionary<string, Dictionary<string, string>>? reqs = await FetchSteamRequirementsAsync(gameName, cancellationToken).ConfigureAwait(false);
            if (reqs != null)
            {
                string formatted = FormatRequirements(gameName, reqs);
                await File.WriteAllTextAsync(reqFile, formatted, cancellationToken).ConfigureAwait(false);
                await Task.Delay(300, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download requirements for {GameName}", gameName);
        }
    }

    private async Task DownloadCoverAsync(string gameName, string gameFolder, bool overwriteExisting, CancellationToken cancellationToken)
    {
        string coverPath = Path.Combine(gameFolder, "cover.jpg");
        if (File.Exists(coverPath))
        {
            FileInfo fi = new(coverPath);
            if (fi.Length >= 1500 && !overwriteExisting)
            {
                return;
            }

            try
            {
                File.Delete(coverPath);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to delete old/corrupted cover at {Path}", coverPath);
            }
        }

        try
        {
            IReadOnlyList<string> candidates = GameTitleSanitizer.GenerateSearchCandidates(gameName);

            foreach (string candidate in candidates)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                string? appId = await FetchSteamAppIdAsync(candidate, cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(appId) && await TryFetchSteamCoverAsync(appId, coverPath, cancellationToken).ConfigureAwait(false))
                {
                    await Task.Delay(150, cancellationToken).ConfigureAwait(false);
                    return;
                }

                if (await TryFetchCheapSharkCoverAsync(candidate, coverPath, cancellationToken).ConfigureAwait(false)
                    || await TryFetchGogCoverAsync(candidate, coverPath, cancellationToken).ConfigureAwait(false)
                    || await TryFetchPcgwCoverAsync(candidate, coverPath, cancellationToken).ConfigureAwait(false)
                    || await TryFetchWikipediaCoverAsync(candidate, coverPath, cancellationToken).ConfigureAwait(false)
                    || await TryFetchOpenCriticCoverAsync(candidate, coverPath, cancellationToken).ConfigureAwait(false)
                    || await TryFetchLutrisCoverAsync(candidate, coverPath, cancellationToken).ConfigureAwait(false)
                    || await TryFetchGsrCoverAsync(candidate, coverPath, cancellationToken).ConfigureAwait(false))
                {
                    await Task.Delay(150, cancellationToken).ConfigureAwait(false);
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download cover for {GameName}", gameName);
        }
    }

    private async Task<bool> DownloadImageAsync(string url, string outputPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(new Uri(url), cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                byte[] bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                if (!GameTitleSanitizer.IsValidImage(bytes))
                {
                    _logger.LogDebug("Image payload from {Url} failed validation (length: {Length})", url, bytes.Length);
                    return false;
                }

                string? parentDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                {
                    _ = Directory.CreateDirectory(parentDir);
                }

                await File.WriteAllBytesAsync(outputPath, bytes, cancellationToken).ConfigureAwait(false);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DownloadImage failed for {Url}", url);
        }

        return false;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_ownsClient)
        {
            _httpClient?.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    [GeneratedRegex("<[^<]+?>")]
    private static partial Regex HtmlTagRegex();
}

