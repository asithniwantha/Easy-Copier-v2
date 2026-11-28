using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using EasyCopier.Features.Library.Scanning;
using Microsoft.Extensions.Logging;

namespace EasyCopier.Features.Library.Metadata;

/// <summary>
/// Partial class implementation of <see cref="GameInfoDownloadService"/> handling cover art retrieval routines.
/// </summary>
public sealed partial class GameInfoDownloadService
{
    private static readonly string[] SteamCoverUrlTemplates =
    [
        "https://steamcdn-a.akamaihd.net/steam/apps/{0}/library_600x900_2x.jpg",
        "https://steamcdn-a.akamaihd.net/steam/apps/{0}/library_600x900.jpg",
        "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/{0}/library_600x900.jpg",
        "https://steamcdn-a.akamaihd.net/steam/apps/{0}/library_hero.jpg",
        "https://steamcdn-a.akamaihd.net/steam/apps/{0}/capsule_616x353.jpg",
        "https://steamcdn-a.akamaihd.net/steam/apps/{0}/header.jpg"
    ];

    private static readonly (string Formatter, string Extension)[] GogCoverFormatters =
    [
        ("product_card_v2_mobile_slider_639", "jpg"),
        ("glados_432", "jpg"),
        ("_glados_432", "jpg"),
        ("format_2x", "jpg"),
        ("product_card_v2_catalog_list_300", "jpg"),
        ("avif", "webp")
    ];

    private async Task<bool> TryFetchSteamCoverAsync(string appId, string coverPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return false;
        }

        foreach (string template in SteamCoverUrlTemplates)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            string url = string.Format(CultureInfo.InvariantCulture, template, appId);
            if (await DownloadImageAsync(url, coverPath, cancellationToken).ConfigureAwait(false))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<bool> TryFetchCheapSharkCoverAsync(string gameName, string coverPath, CancellationToken cancellationToken)
    {
        Uri url = new($"https://www.cheapshark.com/api/1.0/games?title={Uri.EscapeDataString(gameName)}&limit=8");
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            string content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using JsonDocument doc = JsonDocument.Parse(content);
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
            {
                return false;
            }

            JsonElement? bestElement = null;
            double bestScore = 0.0;

            foreach (JsonElement item in doc.RootElement.EnumerateArray())
            {
                if (item.TryGetProperty("external", out JsonElement extProp))
                {
                    string? candidateTitle = extProp.GetString();
                    if (!string.IsNullOrEmpty(candidateTitle))
                    {
                        double score = GameTitleSanitizer.ComputeMatchScore(gameName, candidateTitle);
                        if (score > bestScore && score >= 0.55)
                        {
                            bestScore = score;
                            bestElement = item;
                        }
                    }
                }
            }

            if (bestElement.HasValue)
            {
                JsonElement match = bestElement.Value;

                if (match.TryGetProperty("steamAppID", out JsonElement steamProp))
                {
                    string? steamAppId = steamProp.GetString();
                    if (!string.IsNullOrEmpty(steamAppId) && steamAppId != "0" && int.TryParse(steamAppId, out _)
                        && await TryFetchSteamCoverAsync(steamAppId, coverPath, cancellationToken).ConfigureAwait(false))
                    {
                        return true;
                    }
                }

                if (match.TryGetProperty("thumb", out JsonElement thumbProp))
                {
                    string? thumbUrl = thumbProp.GetString();
                    if (!string.IsNullOrEmpty(thumbUrl)
                        && await DownloadImageAsync(thumbUrl, coverPath, cancellationToken).ConfigureAwait(false))
                    {
                        return true;
                    }
                }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException or UriFormatException or RegexMatchTimeoutException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            _logger.LogWarning(ex, "CheapShark cover failed for {GameName}", gameName);
        }

        return false;
    }

    private async Task<bool> TryFetchGogCoverAsync(string gameName, string coverPath, CancellationToken cancellationToken)
    {
        Uri url = new($"https://catalog.gog.com/v1/catalog?query=like:{Uri.EscapeDataString(gameName)}&limit=5");
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                string content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                using JsonDocument doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("products", out JsonElement products) && products.GetArrayLength() > 0)
                {
                    JsonElement? bestProduct = null;
                    double bestScore = 0.0;

                    foreach (JsonElement product in products.EnumerateArray())
                    {
                        if (product.TryGetProperty("title", out JsonElement titleProp))
                        {
                            string? candidateTitle = titleProp.GetString();
                            if (!string.IsNullOrEmpty(candidateTitle))
                            {
                                double score = GameTitleSanitizer.ComputeMatchScore(gameName, candidateTitle);
                                if (score > bestScore && score >= 0.55)
                                {
                                    bestScore = score;
                                    bestProduct = product;
                                }
                            }
                        }
                    }

                    if (bestProduct.HasValue)
                    {
                        JsonElement product = bestProduct.Value;

                        if (product.TryGetProperty("coverVertical", out JsonElement cover))
                        {
                            string? template = cover.GetString();
                            if (!string.IsNullOrEmpty(template))
                            {
                                foreach ((string formatter, string ext) in GogCoverFormatters)
                                {
                                    string imgUrl = template.Replace("{formatter}", formatter, StringComparison.Ordinal)
                                                            .Replace("{ext}", ext, StringComparison.Ordinal);
                                    if (await DownloadImageAsync(imgUrl, coverPath, cancellationToken).ConfigureAwait(false))
                                    {
                                        return true;
                                    }
                                }
                            }
                        }

                        if (product.TryGetProperty("coverHorizontal", out JsonElement horizCover))
                        {
                            string? hUrl = horizCover.GetString();
                            if (!string.IsNullOrEmpty(hUrl))
                            {
                                string testUrl = hUrl.Replace("{formatter}", "product_card_v2_mobile_slider_639", StringComparison.Ordinal)
                                                     .Replace("{ext}", "jpg", StringComparison.Ordinal);
                                if (await DownloadImageAsync(testUrl, coverPath, cancellationToken).ConfigureAwait(false))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException or UriFormatException or RegexMatchTimeoutException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            _logger.LogWarning(ex, "GOG cover failed for {GameName}", gameName);
        }

        return false;
    }

    private async Task<bool> TryFetchGsrCoverAsync(string gameName, string coverPath, CancellationToken cancellationToken)
    {
        Uri url = new($"https://gamesystemrequirements.com/games.php?req={Uri.EscapeDataString(gameName)}");
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                string html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                MatchCollection matches = GsrSearchResultRegex().Matches(html);

                string? bestImgUrl = null;
                double bestScore = 0.0;

                foreach (Match match in matches)
                {
                    string candidateTitle = HtmlTagRegex().Replace(match.Groups[2].Value, string.Empty).Trim();
                    double score = GameTitleSanitizer.ComputeMatchScore(gameName, candidateTitle);
                    if (score > bestScore && score >= 0.50)
                    {
                        bestScore = score;
                        bestImgUrl = match.Groups[3].Value;
                    }
                }

                if (bestImgUrl == null && matches.Count > 0)
                {
                    bestImgUrl = matches[0].Groups[3].Value;
                }

                if (!string.IsNullOrEmpty(bestImgUrl))
                {
                    if (bestImgUrl[0] == '/')
                    {
                        bestImgUrl = "https://gamesystemrequirements.com" + bestImgUrl;
                    }

                    return await DownloadImageAsync(bestImgUrl, coverPath, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException or UriFormatException or RegexMatchTimeoutException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            _logger.LogWarning(ex, "GSR cover failed for {GameName}", gameName);
        }

        return false;
    }

    [GeneratedRegex(@"<div class=""game-item"">.*?<a href=""([^""]+)"">(.*?)</a>.*?<img src=""([^""]+)""", RegexOptions.Singleline)]
    private static partial Regex GsrSearchResultRegex();

    private async Task<bool> TryFetchWikipediaCoverAsync(string gameName, string coverPath, CancellationToken cancellationToken)
    {
        string query = $"{gameName} video game";
        Uri url = new($"https://en.wikipedia.org/w/api.php?action=query&generator=search&gsrsearch={Uri.EscapeDataString(query)}&gsrlimit=4&prop=pageimages|description&piprop=original|thumbnail&pithumbsize=800&format=json");

        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                string content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                using JsonDocument doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("query", out JsonElement qProp)
                    && qProp.TryGetProperty("pages", out JsonElement pages))
                {
                    string? bestImageUrl = null;
                    double bestScore = 0.0;

                    foreach (JsonProperty pageProp in pages.EnumerateObject())
                    {
                        if (pageProp.Name == "-1")
                        {
                            continue;
                        }

                        JsonElement page = pageProp.Value;
                        string title = page.GetProperty("title").GetString() ?? string.Empty;
                        string description = page.TryGetProperty("description", out JsonElement descProp)
                            ? descProp.GetString() ?? string.Empty
                            : string.Empty;

                        double score = GameTitleSanitizer.ComputeMatchScore(gameName, title);
                        if (description.Contains("video game", StringComparison.OrdinalIgnoreCase)
                            || description.Contains("game", StringComparison.OrdinalIgnoreCase))
                        {
                            score = Math.Min(1.0, score + 0.25);
                        }

                        if (score > bestScore && score >= 0.50)
                        {
                            string? candidateUrl = null;
                            if (page.TryGetProperty("original", out JsonElement orig) && orig.TryGetProperty("source", out JsonElement origSrc))
                            {
                                candidateUrl = origSrc.GetString();
                            }
                            else if (page.TryGetProperty("thumbnail", out JsonElement thumb) && thumb.TryGetProperty("source", out JsonElement thumbSrc))
                            {
                                candidateUrl = thumbSrc.GetString();
                            }

                            if (!string.IsNullOrEmpty(candidateUrl))
                            {
                                bestScore = score;
                                bestImageUrl = candidateUrl;
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(bestImageUrl)
                        && await DownloadImageAsync(bestImageUrl, coverPath, cancellationToken).ConfigureAwait(false))
                    {
                        return true;
                    }
                }
            }

            Uri summaryUrl = new($"https://en.wikipedia.org/api/rest_v1/page/summary/{Uri.EscapeDataString(gameName)}");
            HttpResponseMessage sumResponse = await _httpClient.GetAsync(summaryUrl, cancellationToken).ConfigureAwait(false);
            if (sumResponse.IsSuccessStatusCode)
            {
                string sumContent = await sumResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                using JsonDocument sumDoc = JsonDocument.Parse(sumContent);
                JsonElement root = sumDoc.RootElement;
                string? imgUrl = null;

                if (root.TryGetProperty("originalimage", out JsonElement origImg) && origImg.TryGetProperty("source", out JsonElement oSrc))
                {
                    imgUrl = oSrc.GetString();
                }
                else if (root.TryGetProperty("thumbnail", out JsonElement thImg) && thImg.TryGetProperty("source", out JsonElement tSrc))
                {
                    imgUrl = tSrc.GetString();
                }

                if (!string.IsNullOrEmpty(imgUrl)
                    && await DownloadImageAsync(imgUrl, coverPath, cancellationToken).ConfigureAwait(false))
                {
                    return true;
                }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException or UriFormatException or RegexMatchTimeoutException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            _logger.LogWarning(ex, "Wikipedia cover failed for {GameName}", gameName);
        }

        return false;
    }

    private async Task<bool> TryFetchPcgwCoverAsync(string gameName, string coverPath, CancellationToken cancellationToken)
    {
        Uri url = new($"https://www.pcgamingwiki.com/w/api.php?action=query&generator=search&gsrsearch={Uri.EscapeDataString(gameName)}&gsrlimit=5&prop=pageimages&pithumbsize=800&format=json");
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                string content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                using JsonDocument doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("query", out JsonElement query) && query.TryGetProperty("pages", out JsonElement pages))
                {
                    string? bestImgUrl = null;
                    double bestScore = 0.0;

                    foreach (JsonProperty pageProp in pages.EnumerateObject())
                    {
                        if (pageProp.Name == "-1")
                        {
                            continue;
                        }

                        JsonElement page = pageProp.Value;
                        string title = page.GetProperty("title").GetString() ?? string.Empty;
                        double score = GameTitleSanitizer.ComputeMatchScore(gameName, title);

                        if (score > bestScore && score >= 0.50
                            && page.TryGetProperty("thumbnail", out JsonElement thumb)
                            && thumb.TryGetProperty("source", out JsonElement source))
                        {
                            string? src = source.GetString();
                            if (!string.IsNullOrEmpty(src))
                            {
                                bestScore = score;
                                bestImgUrl = src;
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(bestImgUrl)
                        && await DownloadImageAsync(bestImgUrl, coverPath, cancellationToken).ConfigureAwait(false))
                    {
                        return true;
                    }
                }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException or UriFormatException or RegexMatchTimeoutException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            _logger.LogWarning(ex, "PCGW cover failed for {GameName}", gameName);
        }

        return false;
    }

    private async Task<bool> TryFetchOpenCriticCoverAsync(string gameName, string coverPath, CancellationToken cancellationToken)
    {
        Uri url = new($"https://api.opencritic.com/api/game/search?criteria={Uri.EscapeDataString(gameName)}");
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                string content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                using JsonDocument doc = JsonDocument.Parse(content);
                if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                {
                    int? bestGameId = null;
                    double bestScore = 0.0;

                    foreach (JsonElement candidate in doc.RootElement.EnumerateArray())
                    {
                        if (candidate.TryGetProperty("name", out JsonElement nameProp)
                            && candidate.TryGetProperty("id", out JsonElement idProp))
                        {
                            string? candidateName = nameProp.GetString();
                            if (!string.IsNullOrEmpty(candidateName))
                            {
                                double score = GameTitleSanitizer.ComputeMatchScore(gameName, candidateName);
                                if (score > bestScore && score >= 0.55)
                                {
                                    bestScore = score;
                                    bestGameId = idProp.GetInt32();
                                }
                            }
                        }
                    }

                    if (bestGameId.HasValue)
                    {
                        Uri gameUrl = new($"https://api.opencritic.com/api/game/{bestGameId.Value}");
                        HttpResponseMessage gameResponse = await _httpClient.GetAsync(gameUrl, cancellationToken).ConfigureAwait(false);
                        if (gameResponse.IsSuccessStatusCode)
                        {
                            string gameContent = await gameResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                            using JsonDocument gameDoc = JsonDocument.Parse(gameContent);
                            JsonElement root = gameDoc.RootElement;

                            string? imgUrl = null;
                            if (root.TryGetProperty("images", out JsonElement images)
                                && images.TryGetProperty("boxArt", out JsonElement boxArt)
                                && boxArt.TryGetProperty("og", out JsonElement og))
                            {
                                imgUrl = $"https://img.opencritic.com/{og.GetString()}";
                            }
                            else if (root.TryGetProperty("bannerImageUrl", out JsonElement banner))
                            {
                                imgUrl = banner.GetString();
                                if (!string.IsNullOrEmpty(imgUrl) && !imgUrl.StartsWith("http", StringComparison.Ordinal))
                                {
                                    imgUrl = $"https://img.opencritic.com/{imgUrl}";
                                }
                            }

                            if (!string.IsNullOrEmpty(imgUrl)
                                && await DownloadImageAsync(imgUrl, coverPath, cancellationToken).ConfigureAwait(false))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException or UriFormatException or RegexMatchTimeoutException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            _logger.LogWarning(ex, "OpenCritic cover failed for {GameName}", gameName);
        }

        return false;
    }

    private async Task<bool> TryFetchLutrisCoverAsync(string gameName, string coverPath, CancellationToken cancellationToken)
    {
        Uri apiUrl = new($"https://lutris.net/api/games?search={Uri.EscapeDataString(gameName)}");
        try
        {
            HttpResponseMessage apiResponse = await _httpClient.GetAsync(apiUrl, cancellationToken).ConfigureAwait(false);
            if (apiResponse.IsSuccessStatusCode)
            {
                string apiContent = await apiResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                using JsonDocument apiDoc = JsonDocument.Parse(apiContent);
                if (apiDoc.RootElement.TryGetProperty("results", out JsonElement results) && results.GetArrayLength() > 0)
                {
                    string? bestCover = null;
                    double bestScore = 0.0;

                    foreach (JsonElement gameItem in results.EnumerateArray())
                    {
                        if (gameItem.TryGetProperty("name", out JsonElement nameProp))
                        {
                            string? candidateName = nameProp.GetString();
                            if (!string.IsNullOrEmpty(candidateName))
                            {
                                double score = GameTitleSanitizer.ComputeMatchScore(gameName, candidateName);
                                if (score > bestScore && score >= 0.55
                                    && gameItem.TryGetProperty("coverart", out JsonElement coverProp))
                                {
                                    string? coverUrl = coverProp.GetString();
                                    if (!string.IsNullOrEmpty(coverUrl))
                                    {
                                        bestScore = score;
                                        bestCover = coverUrl;
                                    }
                                }
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(bestCover)
                        && await DownloadImageAsync(bestCover, coverPath, cancellationToken).ConfigureAwait(false))
                    {
                        return true;
                    }
                }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException or UriFormatException or RegexMatchTimeoutException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            _logger.LogDebug(ex, "Lutris API cover failed for {GameName}, attempting web scrape fallback", gameName);
        }

        Uri pageUrl = new($"https://lutris.net/games/?q={Uri.EscapeDataString(gameName)}");
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(pageUrl, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                string html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                Match match = LutrisSearchResultRegex().Match(html);
                if (match.Success)
                {
                    string imgUrl = match.Groups[1].Value;
                    if (imgUrl.Length > 0 && imgUrl[0] == '/')
                    {
                        imgUrl = "https://lutris.net" + imgUrl;
                    }

                    return await DownloadImageAsync(imgUrl, coverPath, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException or UriFormatException or RegexMatchTimeoutException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            _logger.LogWarning(ex, "Lutris web cover failed for {GameName}", gameName);
        }

        return false;
    }

    [GeneratedRegex(@"<a href=""/games/[^/]+/"".*?<img.*?src=""([^""]+)""", RegexOptions.Singleline)]
    private static partial Regex LutrisSearchResultRegex();
}

