using EasyCopier.Features.Library.Metadata;
using EasyCopier.Features.Library.Models;
using EasyCopier.Features.Library.Scanning;
using EasyCopier.Features.Shell.Contracts;
using EasyCopier.Shared.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EasyCopier.Tests.Features.Library;

public sealed class LibraryServiceAndSanitizerTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"EasyCopierLibTest_{Guid.NewGuid():N}");

    public LibraryServiceAndSanitizerTests()
    {
        _ = Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Fact]
    public void GameTitleSanitizer_StripsRepackTagsAndMatchesExactSequelsOnly()
    {
        string cleaned = GameTitleSanitizer.CleanTitle("Dark.Souls.II.Scholar.of.the.First.Sin.[FitGirl Repack]-CODEX");
        Assert.Contains("Dark Souls II", cleaned, StringComparison.OrdinalIgnoreCase);

        double sequelMismatch = GameTitleSanitizer.ComputeMatchScore("Dark Souls", "Dark Souls II");
        double soundtrackReject = GameTitleSanitizer.ComputeMatchScore("Elden Ring", "Elden Ring Original Soundtrack");
        double exactMatch = GameTitleSanitizer.ComputeMatchScore("Dark Souls III", "Dark Souls III");

        Assert.Equal(0.0, sequelMismatch);
        Assert.Equal(0.0, soundtrackReject);
        Assert.Equal(1.0, exactMatch);
    }

    [Fact]
    public void GameTitleSanitizer_ValidatesImageMagicBytesAndRejectsHtmlErrors()
    {
        byte[] validJpeg = new byte[2000];
        validJpeg[0] = 0xFF;
        validJpeg[1] = 0xD8;
        validJpeg[2] = 0xFF;

        byte[] htmlError = "<html lang=\"en\">404 Not Found</html>"u8.ToArray();

        Assert.True(GameTitleSanitizer.IsValidImage(validJpeg));
        Assert.False(GameTitleSanitizer.IsValidImage(htmlError));
        Assert.False(GameTitleSanitizer.IsValidImage(new byte[100]));
    }

    [Fact]
    public void SysReqFormatter_FormatsHardwareLabelsAndEntities()
    {
        string raw = "Minimum: Processor: Intel i5 Graphics: RTX 3060 Memory: 16 GB RAM OS: Windows 11 &amp; later";
        string formatted = SysReqFormatter.FormatText(raw);

        Assert.Contains("CPU:", formatted, StringComparison.Ordinal);
        Assert.Contains("GPU:", formatted, StringComparison.Ordinal);
        Assert.Contains("RAM:", formatted, StringComparison.Ordinal);
        Assert.Contains("OS:", formatted, StringComparison.Ordinal);
        Assert.DoesNotContain("&amp;", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void LibraryFilterService_FiltersByQueryAndCategory_AndSortsTvAndFilms()
    {
        LibraryFilterService filterService = new();
        List<GameEntry> items =
        [
            new("Halo Infinite", "C:\\Games\\Halo", 100, null, new DateTime(2023, 1, 1), false, LibraryCategory.Game, [GameCategory.Shooter]),
            new("Elden Ring", "C:\\Games\\EldenRing", 200, null, new DateTime(2024, 5, 1), false, LibraryCategory.Game, [GameCategory.RPG]),
            new("Alpha Show", "C:\\Video\\Alpha", 50, null, new DateTime(2022, 1, 1), false, LibraryCategory.TvAndFilm),
            new("Zebra Film", "C:\\Video\\Zebra", 80, null, new DateTime(2025, 1, 1), false, LibraryCategory.TvAndFilm)
        ];

        List<GameEntry> rpgs = filterService.FilterEntries(items, "elden", GameCategory.RPG).ToList();
        Assert.Single(rpgs);
        Assert.Equal("Elden Ring", rpgs[0].Name);

        List<GameEntry> sortedVideos = filterService.SortTvAndFilms(
            items.Where(i => i.Category == LibraryCategory.TvAndFilm),
            TvAndFilmsSortOption.DateCreated,
            isAscending: false).ToList();
        Assert.Equal("Zebra Film", sortedVideos[0].Name);
        Assert.Equal("Alpha Show", sortedVideos[1].Name);
    }

    [Fact]
    public async Task LibraryCacheService_SaveLoadAndValidateDetectsChanges()
    {
        string gameRoot = Path.Combine(_tempRoot, "Games");
        string gameFolder = Path.Combine(gameRoot, "SampleGame");
        _ = Directory.CreateDirectory(gameFolder);
        await File.WriteAllTextAsync(Path.Combine(gameFolder, "data.bin"), "hello");

        string cachePath = Path.Combine(_tempRoot, "library_cache.json");
        LibraryCacheService cacheService = new(NullLogger<LibraryCacheService>.Instance, cachePath);

        AppSettings settings = new() { GameSourceFolders = [gameRoot] };
        GameEntry entry = new("SampleGame", gameFolder, 5, null, DateTime.UtcNow, false, LibraryCategory.Game);

        string? warning = await cacheService.CreateAndSaveSnapshotAsync([entry], settings);
        Assert.Null(warning);

        LibraryCacheSnapshot? loaded = await cacheService.LoadCacheAsync();
        Assert.NotNull(loaded);
        Assert.Single(loaded.Games);

        CacheValidationOutcome validOutcome = await cacheService.ValidateCacheAsync(loaded, settings);
        Assert.Equal(CacheValidationResult.Valid, validOutcome.Result);

        await File.WriteAllTextAsync(Path.Combine(gameFolder, "data.bin"), "hello updated content");
        CacheValidationOutcome changedOutcome = await cacheService.ValidateCacheAsync(loaded, settings);
        Assert.Equal(CacheValidationResult.ItemsChanged, changedOutcome.Result);
    }
}

