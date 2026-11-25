using System.Text.RegularExpressions;
using EasyCopier.Features.Shell.Contracts;

namespace EasyCopier.Features.OsImageLauncher;

/// <summary>
/// Provides utilities for discovering and resolving Rufus executable paths and version information.
/// </summary>
public static partial class RufusResolutionHelper
{
    [GeneratedRegex(@"rufus[^\d]*(?<version>\d+(?:\.\d+)+)", RegexOptions.IgnoreCase)]
    private static partial Regex RufusVersionRegex();

    /// <summary>
    /// Attempts to parse a <see cref="Version"/> object from a Rufus filename (e.g., <c>rufus-4.7.exe</c>).
    /// </summary>
    public static Version? TryParseVersionFromFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        Match match = RufusVersionRegex().Match(fileName);
        if (match.Success)
        {
            string versionStr = match.Groups["version"].Value;
            if (Version.TryParse(versionStr, out Version? parsedVersion))
            {
                return parsedVersion;
            }
        }

        return null;
    }

    /// <summary>
    /// Expands environment variables in <paramref name="currentRufusPath"/>, scans the directory for
    /// <c>rufus*.exe</c> binaries with version numbers, and returns the path with the highest version.
    /// </summary>
    public static string ResolveLatestRufusPath(string? currentRufusPath)
    {
        if (string.IsNullOrWhiteSpace(currentRufusPath))
        {
            return string.Empty;
        }

        string expandedPath = Environment.ExpandEnvironmentVariables(currentRufusPath);
        string? directory = Path.GetDirectoryName(expandedPath);

        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return expandedPath;
        }

        try
        {
            DirectoryInfo dirInfo = new(directory);
            FileInfo[] files = dirInfo.GetFiles("rufus*.exe", SearchOption.TopDirectoryOnly);

            var candidates = files
                .Select(f => new
                {
                    FileInfo = f,
                    Version = TryParseVersionFromFileName(f.Name)
                })
                .Where(c => c.Version is not null)
                .OrderByDescending(c => c.Version)
                .ThenByDescending(c => c.FileInfo.LastWriteTimeUtc)
                .ToList();

            if (candidates.Count > 0)
            {
                return candidates[0].FileInfo.FullName;
            }
        }
        catch
        {
            // Fall back to expandedPath on directory access errors.
        }

        return expandedPath;
    }
}

/// <summary>
/// Registers versioned Rufus path resolution via <see cref="IRufusExecutableResolver"/> without coupling Settings to OsImageLauncher.
/// </summary>
public sealed class RufusExecutableResolverAdapter : IRufusExecutableResolver
{
    /// <inheritdoc />
    public string ResolveLatestRufusPath(string? currentRufusPath) =>
        RufusResolutionHelper.ResolveLatestRufusPath(currentRufusPath);
}

