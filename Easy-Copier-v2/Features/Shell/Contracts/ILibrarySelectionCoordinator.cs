namespace EasyCopier.Features.Shell.Contracts;

/// <summary>
/// Coordinates item selection across library tabs, enforcing tab exclusivity rules and computing selection aggregates.
/// </summary>
public interface ILibrarySelectionCoordinator
{
    IReadOnlyList<GameEntry> SelectedEntries { get; }
    int SelectedCount { get; }
    long SelectedTotalBytes { get; }
    int SelectedTotalPrice { get; }
    bool HasSelection { get; }

    void SetCategorySelection(LibraryCategory category, IEnumerable<GameEntry> entries);
    void SetActiveTab(bool isOsImagesTabActive);
    void ClearAll();

    event EventHandler? SelectionChanged;
    event EventHandler? ClearRequested;
}

