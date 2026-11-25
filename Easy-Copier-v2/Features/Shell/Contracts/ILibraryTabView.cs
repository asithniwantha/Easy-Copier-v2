namespace EasyCopier.Features.Shell.Contracts;

/// <summary>
/// Contract implemented by library tab user controls for unified selection management.
/// </summary>
public interface ILibraryTabView
{
    /// <summary>Gets a value indicating whether this library tab represents OS Images.</summary>
    bool IsOsImagesTab { get; }

    /// <summary>Retrieves the collection of currently selected <see cref="GameEntry"/> items in this view.</summary>
    IEnumerable<GameEntry> GetSelectedEntries();

    /// <summary>Clears all active item selections in this view.</summary>
    void ClearSelection();
}

