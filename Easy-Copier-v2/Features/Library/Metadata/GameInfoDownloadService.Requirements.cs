using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using EasyCopier.Features.Library.Scanning;
using Microsoft.Extensions.Logging;

namespace EasyCopier.Features.Library.Metadata;

/// <summary>
/// Partial class implementation of <see cref="GameInfoDownloadService"/> handling PC system requirements lookup and formatting.
/// </summary>
public sealed partial class GameInfoDownloadService
{
    private async Task<Dictionary<string, Dictionary<string, string>>?> FetchSteamRequirementsAsync(string gameName, CancellationToken cancellationToken)
    {
        string? appId = await FetchSteamAppIdAsync(gameName, cancellationToken).ConfigureAwait(false);
        if (appId == null)
        {
            return null;
        }

        Uri url = new($"https://store.steampowered.com/api/appdetails?appids={appId}");
        HttpResponseMessage response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        string content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using JsonDocument doc = JsonDocument.Parse(content);
        if (!doc.RootElement.TryGetProperty(appId, out JsonElement appData) || !appData.GetProperty("success").GetBoolean())
        {
            return null;
        }

        JsonElement data = appData.GetProperty("data");
        Dictionary<string, Dictionary<string, string>> requirements = [];

        if (data.TryGetProperty("pc_requirements", out JsonElement pcReqs))
        {
            if (pcReqs.TryGetProperty("minimum", out JsonElement minProp))
            {
                Dictionary<string, string> parsed = ParseSteamRequirements(minProp.GetString());
                if (parsed.Count > 0)
                {
                    requirements["minimum"] = parsed;
                }
            }

            if (pcReqs.TryGetProperty("recommended", out JsonElement recProp))
            {
                Dictionary<string, string> parsed = ParseSteamRequirements(recProp.GetString());
                if (parsed.Count > 0)
                {
                    requirements["recommended"] = parsed;
                }
            }
        }

        return requirements.Count > 0 ? requirements : null;
    }

    private static Dictionary<string, string> ParseSteamRequirements(string? html)
    {
        Dictionary<string, string> specs = [];
        if (string.IsNullOrWhiteSpace(html))
        {
            return specs;
        }

        string text = HtmlTagRegex().Replace(html, string.Empty);
        text = MultiNewlineRegex().Replace(text, "\n").Trim();
        string[] lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (string line in lines)
        {
            string t = line.Trim();
            if (string.IsNullOrEmpty(t))
            {
                continue;
            }

            if (CpuSpecRegex().IsMatch(t))
            {
                specs["CPU"] = t;
            }
            else if (GpuSpecRegex().IsMatch(t))
            {
                specs["GPU"] = t;
            }
            else if (RamSpecRegex().IsMatch(t))
            {
                specs["RAM"] = t;
            }
            else if (StorageSpecRegex().IsMatch(t))
            {
                specs["Storage"] = t;
            }
        }

        return specs;
    }

    [GeneratedRegex("\n+")]
    private static partial Regex MultiNewlineRegex();

    [GeneratedRegex("cpu|processor", RegexOptions.IgnoreCase)]
    private static partial Regex CpuSpecRegex();

    [GeneratedRegex("gpu|graphics|video|directx", RegexOptions.IgnoreCase)]
    private static partial Regex GpuSpecRegex();

    [GeneratedRegex("memory|ram|gb", RegexOptions.IgnoreCase)]
    private static partial Regex RamSpecRegex();

    [GeneratedRegex("storage|disk|space", RegexOptions.IgnoreCase)]
    private static partial Regex StorageSpecRegex();

    private static string FormatRequirements(string gameName, Dictionary<string, Dictionary<string, string>> requirements)
    {
        StringBuilder sb = new();
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"SYSTEM REQUIREMENTS FOR: {gameName}");
        _ = sb.AppendLine(new string('=', 70));
        _ = sb.AppendLine();

        _ = sb.AppendLine("MINIMUM REQUIREMENTS:");
        _ = sb.AppendLine(new string('-', 70));
        if (!requirements.TryGetValue("minimum", out Dictionary<string, string>? minSpecs))
        {
            minSpecs = [];
        }

        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"CPU: {(minSpecs.TryGetValue("CPU", out string? cpu) ? cpu : "Not available")}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"GPU: {(minSpecs.TryGetValue("GPU", out string? gpu) ? gpu : "Not available")}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"RAM: {(minSpecs.TryGetValue("RAM", out string? ram) ? ram : "Not available")}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Storage: {(minSpecs.TryGetValue("Storage", out string? storage) ? storage : "Not available")}");
        _ = sb.AppendLine();

        _ = sb.AppendLine("RECOMMENDED REQUIREMENTS:");
        _ = sb.AppendLine(new string('-', 70));
        if (!requirements.TryGetValue("recommended", out Dictionary<string, string>? recSpecs))
        {
            recSpecs = [];
        }

        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"CPU: {(recSpecs.TryGetValue("CPU", out string? rcpu) ? rcpu : "Not available")}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"GPU: {(recSpecs.TryGetValue("GPU", out string? rgpu) ? rgpu : "Not available")}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"RAM: {(recSpecs.TryGetValue("RAM", out string? rram) ? rram : "Not available")}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Storage: {(recSpecs.TryGetValue("Storage", out string? rstorage) ? rstorage : "Not available")}");
        _ = sb.AppendLine();

        _ = sb.AppendLine(new string('-', 70));
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Created on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        _ = sb.AppendLine("Source: Fetched from Steam Web API");

        return sb.ToString();
    }

    private async Task<string?> FetchSteamAppIdAsync(string gameName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(gameName))
        {
            return null;
        }

        IReadOnlyList<string> candidates = GameTitleSanitizer.GenerateSearchCandidates(gameName);
        string? bestAppId = null;
        double bestScore = 0.0;

        foreach (string candidate in candidates)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                Uri searchAppsUrl = new($"https://steamcommunity.com/actions/SearchApps/{Uri.EscapeDataString(candidate)}");
                HttpResponseMessage response = await _httpClient.GetAsync(searchAppsUrl, cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    string content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    using JsonDocument doc = JsonDocument.Parse(content);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement item in doc.RootElement.EnumerateArray())
                        {
                            if (item.TryGetProperty("name", out JsonElement nameProp) && item.TryGetProperty("appid", out JsonElement appidProp))
                            {
                                string? appName = nameProp.GetString();
                                string? appId = appidProp.GetString();
                                if (!string.IsNullOrEmpty(appName) && !string.IsNullOrEmpty(appId))
                                {
                                    double score = GameTitleSanitizer.ComputeMatchScore(candidate, appName);
                                    if (score > bestScore && score >= 0.55)
                                    {
                                        bestScore = score;
                                        bestAppId = appId;
                                        if (score >= 0.95)
                                        {
                                            return bestAppId;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Steam Community SearchApps failed for candidate: {Candidate}", candidate);
            }

            if (bestScore >= 0.85 && bestAppId != null)
            {
                return bestAppId;
            }

            try
            {
                Uri storeSearchUrl = new($"https://store.steampowered.com/api/storesearch/?term={Uri.EscapeDataString(candidate)}&l=english&cc=US");
                HttpResponseMessage storeResp = await _httpClient.GetAsync(storeSearchUrl, cancellationToken).ConfigureAwait(false);
                if (storeResp.IsSuccessStatusCode)
                {
                    string storeContent = await storeResp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    using JsonDocument storeDoc = JsonDocument.Parse(storeContent);
                    if (storeDoc.RootElement.TryGetProperty("items", out JsonElement items) && items.GetArrayLength() > 0)
                    {
                        foreach (JsonElement item in items.EnumerateArray())
                        {
                            if (item.TryGetProperty("name", out JsonElement nameProp) && item.TryGetProperty("id", out JsonElement idProp))
                            {
                                string? appName = nameProp.GetString();
                                int appId = idProp.GetInt32();
                                if (!string.IsNullOrEmpty(appName) && appId > 0)
                                {
                                    double score = GameTitleSanitizer.ComputeMatchScore(candidate, appName);
                                    if (score > bestScore && score >= 0.55)
                                    {
                                        bestScore = score;
                                        bestAppId = appId.ToString(CultureInfo.InvariantCulture);
                                        if (score >= 0.95)
                                        {
                                            return bestAppId;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Steam Store search failed for candidate: {Candidate}", candidate);
            }

            if (bestScore >= 0.80 && bestAppId != null)
            {
                return bestAppId;
            }

            try
            {
                Uri cheapSharkUrl = new($"https://www.cheapshark.com/api/1.0/games?title={Uri.EscapeDataString(candidate)}&limit=5");
                HttpResponseMessage csResp = await _httpClient.GetAsync(cheapSharkUrl, cancellationToken).ConfigureAwait(false);
                if (csResp.IsSuccessStatusCode)
                {
                    string csContent = await csResp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    using JsonDocument csDoc = JsonDocument.Parse(csContent);
                    if (csDoc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement item in csDoc.RootElement.EnumerateArray())
                        {
                            if (item.TryGetProperty("external", out JsonElement extProp) && item.TryGetProperty("steamAppID", out JsonElement steamProp))
                            {
                                string? extName = extProp.GetString();
                                string? steamId = steamProp.GetString();
                                if (!string.IsNullOrEmpty(extName) && !string.IsNullOrEmpty(steamId) && steamId != "0")
                                {
                                    double score = GameTitleSanitizer.ComputeMatchScore(candidate, extName);
                                    if (score > bestScore && score >= 0.60)
                                    {
                                        bestScore = score;
                                        bestAppId = steamId;
                                        if (score >= 0.95)
                                        {
                                            return bestAppId;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "CheapShark AppId lookup failed for candidate: {Candidate}", candidate);
            }

            if (bestAppId != null && bestScore >= 0.65)
            {
                return bestAppId;
            }
        }

        return bestAppId;
    }
}

