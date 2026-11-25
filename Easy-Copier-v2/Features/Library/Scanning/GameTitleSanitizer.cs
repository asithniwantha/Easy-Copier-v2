using System.Globalization;
using System.Text.RegularExpressions;

namespace EasyCopier.Features.Library.Scanning;

/// <summary>
/// Utility class providing game title sanitization, candidate title generation, sequel installment analysis,
/// title match scoring, and image payload validation for cover art retrieval.
/// </summary>
public static partial class GameTitleSanitizer
{
    [GeneratedRegex(@"\[.*?(?:repack|fitgirl|dodi|chovka|kaos|tinyrepack|gog|steam|epic|elamigos|rip|portable|cracked|lossless|multi\d*).*?\]", RegexOptions.IgnoreCase)]
    private static partial Regex RepackAndBracketTagsRegex();

    [GeneratedRegex(@"-(?:codex|empress|cpy|skidrow|reloaded|prophet|plaza|rune|tenoke|flt|razor1911|tinyiso|deviance|darksiders|hoodlum|vace|dino|fairlight|i_know|unleashed|outlaws|alias|goldberg|rld|bat|simplex|chronos|elamigos)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SceneGroupSuffixRegex();

    [GeneratedRegex(@"(?:\b(?:v|build|update|patch|hotfix)\s*[\d\.]+[a-z]?\b)|(?:\(build\s*\d+\))|(?:\(v[\d\.]+\))", RegexOptions.IgnoreCase)]
    private static partial Regex VersionAndBuildRegex();

    [GeneratedRegex(@"\((?!(?:19|20)\d{2}\b)[^)]*(?:dlc|pack|portable|cracked|setup|rip|bonus|multi|v\d)[^)]*\)", RegexOptions.IgnoreCase)]
    private static partial Regex MetadataParenthesesRegex();

    [GeneratedRegex(@"\b(?:digital\s+deluxe|deluxe|complete|definitive|collector'?s?|game\s+of\s+the\s+year|goty|standard|enhanced|director'?s?\s+cut|remastered|anniversary|special|day\s+one|legacy|premium|ultimate|prepare\s+to\s+die|scholar\s+of\s+the\s+first\s+sin)\s+edition\b|\bgoty\b|\bremastered\b|\bremake\b|\bdirector'?s?\s+cut\b|\bprepare\s+to\s+die\b", RegexOptions.IgnoreCase)]
    private static partial Regex EditionSuffixesRegex();

    [GeneratedRegex(@"\b\((?:19|20)\d{2}\)|\b(?:19|20)\d{2}\b")]
    private static partial Regex YearRegex();

    [GeneratedRegex(@"(?<=[a-zA-Z])[\._](?=[a-zA-Z0-9])|(?<=[a-zA-Z0-9])[\._](?=[a-zA-Z])")]
    private static partial Regex WordSeparatorRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"[^\w]+")]
    private static partial Regex NonWordRegex();

    [GeneratedRegex(@"\b(?:part|episode|vol|volume|chapter)\s+([ivx\d]+)\b", RegexOptions.IgnoreCase)]
    private static partial Regex InstallmentPartRegex();

    private static readonly Dictionary<string, int> RomanNumerals = new(StringComparer.OrdinalIgnoreCase)
    {
        { "I", 1 }, { "II", 2 }, { "III", 3 }, { "IV", 4 }, { "V", 5 },
        { "VI", 6 }, { "VII", 7 }, { "VIII", 8 }, { "IX", 9 }, { "X", 10 },
        { "XI", 11 }, { "XII", 12 }, { "XIII", 13 }, { "XIV", 14 }, { "XV", 15 },
        { "XVI", 16 }, { "XVII", 17 }, { "XVIII", 18 }, { "XIX", 19 }, { "XX", 20 }
    };

    private static readonly string[] NegativeKeywords =
    [
        "soundtrack", "original soundtrack", " ost ", "score", "dlc", "expansion pass",
        "season pass", "artbook", "artwork", "playtest", "dedicated server", " server",
        "trailer", "teaser", "wallpaper", "demo", "benchmark"
    ];

    /// <summary>
    /// Generates a prioritized list of search candidate queries for a given raw game folder or title string.
    /// </summary>
    public static IReadOnlyList<string> GenerateSearchCandidates(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return [];
        }

        List<string> candidates = [];

        string cleaned = CleanTitle(rawName);
        if (!string.IsNullOrWhiteSpace(cleaned))
        {
            candidates.Add(cleaned);

            string withoutEdition = StripEditions(cleaned);
            if (!string.IsNullOrWhiteSpace(withoutEdition) && !string.Equals(withoutEdition, cleaned, StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add(withoutEdition);
            }

            string withoutYear = YearRegex().Replace(cleaned, string.Empty).Trim();
            withoutYear = CleanPunctuation(withoutYear);
            if (!string.IsNullOrWhiteSpace(withoutYear) && !string.Equals(withoutYear, cleaned, StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add(withoutYear);

                string withoutYearAndEdition = StripEditions(withoutYear);
                if (!string.IsNullOrWhiteSpace(withoutYearAndEdition) && !string.Equals(withoutYearAndEdition, withoutYear, StringComparison.OrdinalIgnoreCase))
                {
                    candidates.Add(withoutYearAndEdition);
                }
            }

            int sepIndex = cleaned.IndexOfAny([':', '\u2013', '-']);
            if (sepIndex > 3)
            {
                string baseBeforeSep = CleanPunctuation(cleaned[..sepIndex]);
                if (!string.IsNullOrWhiteSpace(baseBeforeSep) && baseBeforeSep.Length >= 4)
                {
                    candidates.Add(baseBeforeSep);
                }
            }
        }

        string rawTrimmed = rawName.Trim();
        if (!string.IsNullOrWhiteSpace(rawTrimmed))
        {
            candidates.Add(rawTrimmed);
        }

        return candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Cleans a raw folder or file name by stripping repack tags, scene suffixes, version tags, and normalizes separators.
    /// </summary>
    public static string CleanTitle(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return string.Empty;
        }

        string result = rawName;
        result = RepackAndBracketTagsRegex().Replace(result, " ");
        result = SceneGroupSuffixRegex().Replace(result, " ");
        result = VersionAndBuildRegex().Replace(result, " ");
        result = MetadataParenthesesRegex().Replace(result, " ");
        result = WordSeparatorRegex().Replace(result, " ");

        return CleanPunctuation(result);
    }

    /// <summary>
    /// Strips common game edition keywords from a title.
    /// </summary>
    public static string StripEditions(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        string stripped = EditionSuffixesRegex().Replace(title, " ");
        return CleanPunctuation(stripped);
    }

    private static string CleanPunctuation(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        string text = WhitespaceRegex().Replace(input, " ").Trim();
        return text.Trim('-', '\u2013', '_', '.', ':', ';', ',', '~', ' ');
    }

    /// <summary>
    /// Extracts an explicit sequel or installment number (Arabic or Roman numeral) from a game title.
    /// Returns <see langword="null"/> if no sequel number is detected.
    /// </summary>
    public static int? ExtractSequelNumber(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        string cleaned = StripEditions(title);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            cleaned = title;
        }

        cleaned = YearRegex().Replace(cleaned, " ").Trim();

        int subtitleIdx = cleaned.IndexOfAny([':', '\u2013', '-']);
        string basePart = subtitleIdx > 0 ? cleaned[..subtitleIdx].Trim() : cleaned;

        string[] tokens = NonWordRegex().Split(basePart).Where(t => !string.IsNullOrWhiteSpace(t)).ToArray();
        if (tokens.Length == 0)
        {
            return null;
        }

        string lastToken = tokens[^1];
        if (RomanNumerals.TryGetValue(lastToken, out int romanVal))
        {
            return romanVal;
        }

        if (int.TryParse(lastToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out int arabicVal)
            && arabicVal is >= 1 and <= 99)
        {
            return arabicVal;
        }

        Match partMatch = InstallmentPartRegex().Match(title);
        if (partMatch.Success)
        {
            string numStr = partMatch.Groups[1].Value;
            if (RomanNumerals.TryGetValue(numStr, out int pRoman))
            {
                return pRoman;
            }

            if (int.TryParse(numStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int pArabic)
                && pArabic is >= 1 and <= 99)
            {
                return pArabic;
            }
        }

        return null;
    }

    /// <summary>
    /// Computes a title match similarity score between a target query title and an online candidate title (0.0 to 1.0).
    /// Enforces strict sequel installment matching.
    /// </summary>
    public static double ComputeMatchScore(string targetTitle, string candidateTitle)
    {
        if (string.IsNullOrWhiteSpace(targetTitle) || string.IsNullOrWhiteSpace(candidateTitle))
        {
            return 0.0;
        }

        int? targetSequel = ExtractSequelNumber(targetTitle);
        int? candidateSequel = ExtractSequelNumber(candidateTitle);

        if ((targetSequel == null || targetSequel == 1) && candidateSequel is >= 2)
        {
            return 0.0;
        }

        if (targetSequel is >= 2 && (candidateSequel == null || candidateSequel == 1))
        {
            return 0.0;
        }

        if (targetSequel is >= 2 && candidateSequel is >= 2 && targetSequel != candidateSequel)
        {
            return 0.0;
        }

        string normTarget = NormalizeForComparison(targetTitle);
        string normCandidate = NormalizeForComparison(candidateTitle);

        if (normTarget.Length == 0 || normCandidate.Length == 0)
        {
            return 0.0;
        }

        foreach (string neg in NegativeKeywords)
        {
            if (normCandidate.Contains(neg, StringComparison.OrdinalIgnoreCase) &&
                !normTarget.Contains(neg, StringComparison.OrdinalIgnoreCase))
            {
                return 0.0;
            }
        }

        if (string.Equals(normTarget, normCandidate, StringComparison.OrdinalIgnoreCase))
        {
            return 1.0;
        }

        string[] targetTokens = normTarget.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string[] candidateTokens = normCandidate.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (targetTokens.Length == 0 || candidateTokens.Length == 0)
        {
            return 0.0;
        }

        int matchingTokens = targetTokens.Count(t => candidateTokens.Contains(t, StringComparer.OrdinalIgnoreCase));
        double tokenDice = 2.0 * matchingTokens / (targetTokens.Length + candidateTokens.Length);

        double targetCoverage = (double)matchingTokens / targetTokens.Length;
        if (targetCoverage < 0.65)
        {
            tokenDice *= targetCoverage;
        }

        if (normCandidate.StartsWith(normTarget + " ", StringComparison.OrdinalIgnoreCase) ||
            normTarget.StartsWith(normCandidate + " ", StringComparison.OrdinalIgnoreCase))
        {
            double lengthRatio = Math.Min((double)normTarget.Length / normCandidate.Length, (double)normCandidate.Length / normTarget.Length);
            return Math.Max(tokenDice, (0.70 * lengthRatio) + 0.30);
        }

        return tokenDice;
    }

    private static string NormalizeForComparison(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        string upper = input.ToUpperInvariant();
        char[] chars = upper.Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray();
        string cleaned = new(chars);
        return WhitespaceRegex().Replace(cleaned, " ").Trim();
    }

    /// <summary>
    /// Validates that raw byte data represents a legitimate non-empty image (JPEG, PNG, WebP, BMP).
    /// </summary>
    public static bool IsValidImage(byte[]? bytes)
    {
        if (bytes == null || bytes.Length < 1500)
        {
            return false;
        }

        if (bytes[0] is (byte)'<' or (byte)'{')
        {
            return false;
        }

        if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return true;
        }

        if (bytes.Length >= 8 &&
            bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
            bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
        {
            return true;
        }

        if (bytes.Length >= 12 &&
            bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
            bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
        {
            return true;
        }

        return bytes[0] == 0x42 && bytes[1] == 0x4D;
    }
}

