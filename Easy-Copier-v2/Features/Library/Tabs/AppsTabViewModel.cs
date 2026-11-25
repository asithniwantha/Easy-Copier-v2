using System.Collections.ObjectModel;
using System.ComponentModel;
using EasyCopier.Features.Shell.Contracts;

namespace EasyCopier.Features.Library.Tabs;

/// <summary>
/// ViewModel for managing the Applications library tab view.
/// </summary>
public sealed partial class AppsTabViewModel : LibraryTabViewModelBase, ISearchableTabViewModel
{
    private readonly ILibraryCatalogService _catalog;

    /// <inheritdoc />
    public override LibraryCategory Category => LibraryCategory.App;

    /// <summary>Gets the filtered list of application entries.</summary>
    public ObservableCollection<GameEntry> Apps => _catalog.Apps;

    /// <summary>Gets the message displayed when no apps match criteria.</summary>
    public string EmptyAppsMessage => _catalog.EmptyAppsMessage;

    /// <summary>Gets a value indicating whether the apps collection is empty.</summary>
    public bool IsAppsEmpty => _catalog.IsAppsEmpty;

    /// <summary>Gets the list of available categories for filtering.</summary>
    public IReadOnlyList<GameCategory> AvailableCategories => _catalog.AvailableCategories;

    /// <summary>Gets or sets the currently selected category filter.</summary>
    public GameCategory SelectedCategory
    {
        get => _catalog.SelectedCategory;
        set => _catalog.SelectedCategory = value;
    }

    /// <inheritdoc />
    public string SearchText
    {
        get => _catalog.AppsSearchText;
        set => _catalog.AppsSearchText = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AppsTabViewModel"/> class.
    /// </summary>
    public AppsTabViewModel(
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
                OnPropertyChanged(nameof(IsAppsEmpty));
                break;
            case nameof(ILibraryCatalogService.IsAppsEmpty):
                OnPropertyChanged(nameof(IsAppsEmpty));
                break;
            case nameof(ILibraryCatalogService.EmptyAppsMessage):
                OnPropertyChanged(nameof(EmptyAppsMessage));
                break;
            case nameof(ILibraryCatalogService.SelectedCategory):
                OnPropertyChanged(nameof(SelectedCategory));
                break;
            case nameof(ILibraryCatalogService.AppsSearchText):
                OnPropertyChanged(nameof(SearchText));
                break;
        }
    }

    /// <inheritdoc />
    public IEnumerable<string> GetSuggestions(string query)
        => GetSuggestionsFromEntries(Apps, query);

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

