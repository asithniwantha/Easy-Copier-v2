namespace EasyCopier.Features.Shell.Contracts;

/// <summary>
/// Contract for library tab ViewModels that support search text query binding and autocomplete suggestions.
/// </summary>
public interface ISearchableTabViewModel
{
    /// <summary>Gets or sets the search filter text query.</summary>
    string SearchText { get; set; }

    /// <summary>Retrieves autocomplete suggestions matching the specified query string.</summary>
    IEnumerable<string> GetSuggestions(string query);
}

