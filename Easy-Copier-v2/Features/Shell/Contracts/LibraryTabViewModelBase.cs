using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

namespace EasyCopier.Features.Shell.Contracts;

/// <summary>
/// Abstract base ViewModel for tab-specific ViewModels, coordinating selection and autocomplete suggestions without cross-slice coupling.
/// </summary>
public abstract partial class LibraryTabViewModelBase : ObservableRecipient, IDisposable
{
    private bool _isDisposed;

    /// <summary>Gets the shared selection coordinator across library tabs.</summary>
    protected ILibrarySelectionCoordinator SelectionCoordinator { get; }

    /// <summary>Gets the <see cref="LibraryCategory"/> represented by this tab.</summary>
    public abstract LibraryCategory Category { get; }

    /// <summary>Occurs when an external action requests clearing the active selection in this tab.</summary>
    public event EventHandler? ClearSelectionRequested;

    /// <summary>Gets or sets a value indicating whether library scanning is active for this tab.</summary>
    [ObservableProperty]
    public partial bool IsScanning { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="LibraryTabViewModelBase"/> class.
    /// </summary>
    protected LibraryTabViewModelBase(ILibrarySelectionCoordinator selectionCoordinator)
    {
        SelectionCoordinator = selectionCoordinator ?? throw new ArgumentNullException(nameof(selectionCoordinator));
        SelectionCoordinator.ClearRequested += OnCoordinatorClearRequested;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LibraryTabViewModelBase"/> class using an explicit messenger instance.
    /// </summary>
    protected LibraryTabViewModelBase(ILibrarySelectionCoordinator selectionCoordinator, IMessenger messenger)
        : base(messenger)
    {
        SelectionCoordinator = selectionCoordinator ?? throw new ArgumentNullException(nameof(selectionCoordinator));
        SelectionCoordinator.ClearRequested += OnCoordinatorClearRequested;
    }

    /// <summary>Updates the selection coordinator with the selected entries for this tab category.</summary>
    public void UpdateSelection(IEnumerable<GameEntry> entries)
    {
        SelectionCoordinator.SetCategorySelection(Category, entries);
    }

    /// <summary>Requests this tab view to clear its item selection.</summary>
    public void RequestClearSelection()
    {
        ClearSelectionRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Filters item entries to generate a distinct list of up to 10 autocomplete suggestions matching the query string.
    /// </summary>
    public static IEnumerable<string> GetSuggestionsFromEntries(IEnumerable<GameEntry> entries, string query)
    {
        return string.IsNullOrWhiteSpace(query)
            ? []
            : entries
                .Select(i => i.Name)
                .Where(n => n.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .Take(10);
    }

    private void OnCoordinatorClearRequested(object? sender, EventArgs e) => RequestClearSelection();

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            return;
        }

        if (disposing)
        {
            IsActive = false;
            SelectionCoordinator.ClearRequested -= OnCoordinatorClearRequested;
        }

        _isDisposed = true;
    }
}

