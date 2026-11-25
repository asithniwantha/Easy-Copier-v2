using EasyCopier.Features.Library.Models;
using EasyCopier.Features.Shell.Contracts;

namespace EasyCopier.Features.Library.Scanning;

/// <summary>
/// Service that handles filtering and sorting operations for library entries.
/// </summary>
public class LibraryFilterService : ILibraryFilterService
{
    /// <inheritdoc />
    public IEnumerable<GameEntry> FilterEntries(IEnumerable<GameEntry> source, string? query, GameCategory categoryFilter)
    {
        ArgumentNullException.ThrowIfNull(source);

        string searchText = query?.Trim() ?? string.Empty;

        IEnumerable<GameEntry> filtered = string.IsNullOrEmpty(searchText)
            ? source
            : source.Where(g => g.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase));

        if (categoryFilter != GameCategory.All)
        {
            filtered = filtered.Where(g => g.Categories != null && g.Categories.Contains(categoryFilter));
        }

        return filtered;
    }

    /// <inheritdoc />
    public IEnumerable<GameEntry> SortTvAndFilms(IEnumerable<GameEntry> entries, TvAndFilmsSortOption sortOption, bool isAscending)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return sortOption switch
        {
            TvAndFilmsSortOption.Name => isAscending
                ? entries.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                : entries.OrderByDescending(e => e.Name, StringComparer.OrdinalIgnoreCase),
            TvAndFilmsSortOption.DateCreated => isAscending
                ? entries.OrderBy(e => e.DateCreated).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                : entries.OrderByDescending(e => e.DateCreated).ThenByDescending(e => e.Name, StringComparer.OrdinalIgnoreCase),
            _ => entries
        };
    }
}

