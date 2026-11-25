using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyCopier.Features.Library.Models;
using EasyCopier.Features.Shell.Contracts;

namespace EasyCopier.Features.Library.Tabs;

/// <summary>
/// ViewModel for managing the Films &amp; TV series library tab view.
/// </summary>
public sealed partial class TvAndFilmsTabViewModel : LibraryTabViewModelBase, ISearchableTabViewModel
{
    private readonly ILibraryCatalogService _catalog;

    /// <inheritdoc />
    public override LibraryCategory Category => LibraryCategory.TvAndFilm;

    /// <summary>Gets the filtered list of film and TV series entries.</summary>
    public ObservableCollection<GameEntry> TvAndFilms => _catalog.TvAndFilms;

    /// <summary>Gets the message displayed when no films/TV series match criteria.</summary>
    public string EmptyTvAndFilmsMessage => _catalog.EmptyTvAndFilmsMessage;

    /// <summary>Gets a value indicating whether the films/TV series collection is empty.</summary>
    public bool IsTvAndFilmsEmpty => _catalog.IsTvAndFilmsEmpty;

    /// <summary>Gets the list of available sorting options for TV and films.</summary>
    public IReadOnlyList<TvAndFilmsSortOption> AvailableTvAndFilmsSortOptions => _catalog.AvailableTvAndFilmsSortOptions;

    /// <summary>Gets or sets the selected sorting option for TV and films.</summary>
    public TvAndFilmsSortOption SelectedTvAndFilmsSortOption
    {
        get => _catalog.SelectedTvAndFilmsSortOption;
        set => _catalog.SelectedTvAndFilmsSortOption = value;
    }

    /// <summary>Gets or sets a value indicating whether TV and films are sorted in ascending order.</summary>
    public bool IsTvAndFilmsSortAscending
    {
        get => _catalog.IsTvAndFilmsSortAscending;
        set => _catalog.IsTvAndFilmsSortAscending = value;
    }

    /// <summary>Gets the command to toggle sort direction for TV and films.</summary>
    public IRelayCommand ToggleTvAndFilmsSortDirectionCommand => _catalog.ToggleTvAndFilmsSortDirectionCommand;

    /// <inheritdoc />
    public string SearchText
    {
        get => _catalog.TvAndFilmsSearchText;
        set => _catalog.TvAndFilmsSearchText = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TvAndFilmsTabViewModel"/> class.
    /// </summary>
    public TvAndFilmsTabViewModel(
        ILibrarySelectionCoordinator selectionCoordinator,
        ILibraryCatalogService catalog)
        : base(selectionCoordinator)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        IsScanning = _catalog.IsScanning;
        _catalog.PropertyChanged += OnCatalogPropertyChanged;
    }

    private void OnCatalogPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ILibraryCatalogService.IsScanning):
                IsScanning = _catalog.IsScanning;
                OnPropertyChanged(nameof(IsTvAndFilmsEmpty));
                break;
            case nameof(ILibraryCatalogService.IsTvAndFilmsEmpty):
                OnPropertyChanged(nameof(IsTvAndFilmsEmpty));
                break;
            case nameof(ILibraryCatalogService.EmptyTvAndFilmsMessage):
                OnPropertyChanged(nameof(EmptyTvAndFilmsMessage));
                break;
            case nameof(ILibraryCatalogService.SelectedTvAndFilmsSortOption):
                OnPropertyChanged(nameof(SelectedTvAndFilmsSortOption));
                break;
            case nameof(ILibraryCatalogService.IsTvAndFilmsSortAscending):
                OnPropertyChanged(nameof(IsTvAndFilmsSortAscending));
                break;
            case nameof(ILibraryCatalogService.TvAndFilmsSearchText):
                OnPropertyChanged(nameof(SearchText));
                break;
        }
    }

    /// <inheritdoc />
    public IEnumerable<string> GetSuggestions(string query)
        => GetSuggestionsFromEntries(TvAndFilms, query);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _catalog.PropertyChanged -= OnCatalogPropertyChanged;
        }

        base.Dispose(disposing);
    }
}

