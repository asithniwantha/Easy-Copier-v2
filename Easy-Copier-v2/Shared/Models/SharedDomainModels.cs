namespace EasyCopier.Shared.Models;

/// <summary>
/// Represents a monitored source folder for library content scanning.
/// </summary>
public record SourceFolder(string FolderPath, bool IsValid, DateTime LastScanned);

/// <summary>
/// Specifies source folder category types for settings configuration.
/// </summary>
public enum SourceFolderCategory
{
    Game,
    App,
    TvAndFilm,
    OsImage
}

/// <summary>
/// Represents a paginated query result containing a slice of items and total count metadata.
/// </summary>
public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    /// <summary>Gets the total number of pages available.</summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;

    /// <summary>Gets a value indicating whether there is a subsequent page.</summary>
    public bool HasNextPage => Page < TotalPages;

    /// <summary>Gets a value indicating whether there is a preceding page.</summary>
    public bool HasPreviousPage => Page > 1;
}

