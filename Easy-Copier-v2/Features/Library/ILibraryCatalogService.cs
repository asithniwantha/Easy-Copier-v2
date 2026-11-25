using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyCopier.Features.Library.Models;
using EasyCopier.Features.Shell.Contracts;

namespace EasyCopier.Features.Library;

/// <summary>
/// Manages core library catalog state across Games, Apps, and Films/TV tabs including scanning, caching, filtering, and sorting.
/// </summary>
public interface ILibraryCatalogService : INotifyPropertyChanged, IDisposable
{
    ObservableCollection<GameEntry> Games { get; }
    ObservableCollection<GameEntry> Apps { get; }
    ObservableCollection<GameEntry> TvAndFilms { get; }

    bool IsScanning { get; }
    string StatusMessage { get; }

    GameCategory SelectedCategory { get; set; }
    IReadOnlyList<GameCategory> AvailableCategories { get; }

    string GamesSearchText { get; set; }
    string AppsSearchText { get; set; }
    string TvAndFilmsSearchText { get; set; }

    TvAndFilmsSortOption SelectedTvAndFilmsSortOption { get; set; }
    bool IsTvAndFilmsSortAscending { get; set; }
    IReadOnlyList<TvAndFilmsSortOption> AvailableTvAndFilmsSortOptions { get; }

    IRelayCommand ToggleTvAndFilmsSortDirectionCommand { get; }

    bool IsGamesEmpty { get; }
    bool IsAppsEmpty { get; }
    bool IsTvAndFilmsEmpty { get; }

    string EmptyGamesMessage { get; }
    string EmptyAppsMessage { get; }
    string EmptyTvAndFilmsMessage { get; }

    /// <summary>
    /// Loads persisted library cache (if valid) or triggers an initial scan when configured.
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Scans all configured game, app, and film/TV source directories and updates the catalog.
    /// </summary>
    Task ScanLibraryAsync(CancellationToken cancellationToken = default);
}

