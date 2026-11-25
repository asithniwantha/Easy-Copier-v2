using CommunityToolkit.Mvvm.ComponentModel;
using EasyCopier.Shared.UI.Helpers;

namespace EasyCopier.Features.Shell.Contracts;

/// <summary>
/// Specifies the active library tab index in the main application layout.
/// </summary>
public enum LibraryTabIndex
{
    Games = 0,
    Apps = 1,
    TvAndFilms = 2,
    OsImages = 3
}

/// <summary>
/// Represents genres/categories for classifying games.
/// </summary>
public enum GameCategory
{
    All,
    Uncategorized,
    Shooter,
    Racing,
    RPG,
    Strategy,
    Adventure,
    Simulation,
    Sports,
    Puzzle,
    Horror,
    Platformer
}

/// <summary>
/// Represents the high-level library type for media and software items.
/// </summary>
public enum LibraryCategory
{
    Game,
    App,
    TvAndFilm,
    OsImage
}

/// <summary>
/// Represents a single scanned library entry across any category (game, app, TV/film, or OS image).
/// </summary>
public record GameEntry(
    string Name,
    string FolderPath,
    long TotalBytes,
    string? CoverImagePath,
    DateTime DateCreated,
    bool HasLargeFiles,
    LibraryCategory Category = LibraryCategory.Game,
    IReadOnlyList<GameCategory>? Categories = null)
{
    /// <summary>Gets a value indicating whether this entry has an associated cover image path.</summary>
    public bool HasCover => !string.IsNullOrEmpty(CoverImagePath);

    /// <summary>Gets a value indicating whether this entry does not have an associated cover image.</summary>
    public bool HasNoCover => !HasCover;

    /// <summary>Gets the fallback icon glyph corresponding to this entry's content category.</summary>
    public string FallbackGlyph => Category switch
    {
        LibraryCategory.Game => "\uE7FC",
        LibraryCategory.App => "\uE71D",
        LibraryCategory.TvAndFilm => "\uE714",
        LibraryCategory.OsImage => "\uE7C5",
        _ => "\uE7FC"
    };
}

/// <summary>
/// Represents a file system item (folder or file) for display in navigation or selection controls.
/// </summary>
public partial class FileSystemItem : ObservableObject
{
    public string Name { get; }
    public string Path { get; }
    public bool IsFolder { get; }
    public string IconGlyph => IsFolder ? "\uE8D5" : "\uE7C3";

    [ObservableProperty]
    public partial string SizeFormatted { get; set; } = "Calculating...";

    public FileSystemItem(string path, bool isFolder)
    {
        Path = path;
        Name = System.IO.Path.GetFileName(path);
        IsFolder = isFolder;

        if (!IsFolder)
        {
            try
            {
                long size = new FileInfo(path).Length;
                SizeFormatted = FormattingHelpers.FormatBytes(size);
            }
            catch
            {
                SizeFormatted = "Unknown";
            }
        }
    }
}

