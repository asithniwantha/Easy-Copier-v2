using EasyCopier.Features.Library.Models;
using EasyCopier.Features.Shell.Contracts;

namespace EasyCopier.Features.Library.Scanning;

/// <summary>
/// Defines filtering and sorting operations for library collections (Games, Apps, and Films/TV).
/// </summary>
public interface ILibraryFilterService
{
    /// <summary>
    /// Filters library entries by search query and game category.
    /// </summary>
    IEnumerable<GameEntry> FilterEntries(IEnumerable<GameEntry> source, string? query, GameCategory categoryFilter);

    /// <summary>
    /// Sorts TV and film entries according to the specified sort criterion and direction.
    /// </summary>
    IEnumerable<GameEntry> SortTvAndFilms(IEnumerable<GameEntry> entries, TvAndFilmsSortOption sortOption, bool isAscending);
}

