using System.Collections.ObjectModel;
using System.ComponentModel;
using EasyCopier.Features.Shell.Contracts;

namespace EasyCopier.Features.Library.Tabs;

/// <summary>
/// ViewModel for managing the Games library tab view.
/// </summary>
public sealed partial class GamesTabViewModel : LibraryTabViewModelBase, ISearchableTabViewModel
{
    private readonly ILibraryCatalogService _catalog;

    /// <inheritdoc />
    public override LibraryCategory Category => LibraryCategory.Game;

    /// <summary>Gets the filtered list of game entries.</summary>
    public ObservableCollection<GameEntry> Games => _catalog.Games;

    /// <summary>Gets the message displayed when no games match criteria.</summary>
    public string EmptyGamesMessage => _catalog.EmptyGamesMessage;

    /// <summary>Gets a value indicating whether the games collection is empty.</summary>
    public bool IsGamesEmpty => _catalog.IsGamesEmpty;

    /// <summary>Gets the list of available game categories for filtering.</summary>
    public IReadOnlyList<GameCategory> AvailableCategories => _catalog.AvailableCategories;

    /// <summary>Gets or sets the currently selected game category filter.</summary>
    public GameCategory SelectedCategory
    {
        get => _catalog.SelectedCategory;
        set => _catalog.SelectedCategory = value;
    }

    /// <inheritdoc />
    public string SearchText
    {
        get => _catalog.GamesSearchText;
        set => _catalog.GamesSearchText = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GamesTabViewModel"/> class.
    /// </summary>
    public GamesTabViewModel(
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
                OnPropertyChanged(nameof(IsGamesEmpty));
                break;
            case nameof(ILibraryCatalogService.IsGamesEmpty):
                OnPropertyChanged(nameof(IsGamesEmpty));
                break;
            case nameof(ILibraryCatalogService.EmptyGamesMessage):
                OnPropertyChanged(nameof(EmptyGamesMessage));
                break;
            case nameof(ILibraryCatalogService.SelectedCategory):
                OnPropertyChanged(nameof(SelectedCategory));
                break;
            case nameof(ILibraryCatalogService.GamesSearchText):
                OnPropertyChanged(nameof(SearchText));
                break;
        }
    }

    /// <inheritdoc />
    public IEnumerable<string> GetSuggestions(string query)
        => GetSuggestionsFromEntries(Games, query);

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

