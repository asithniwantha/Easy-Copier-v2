using Microsoft.UI.Xaml.Controls;

namespace EasyCopier.Features.Shell.Contracts;

/// <summary>
/// Provides extension methods for WinUI <see cref="GridView"/> controls used in library tab views.
/// </summary>
public static class LibraryViewExtensions
{
    public static IEnumerable<GameEntry> GetSelectedEntries(this GridView? gridView)
        => gridView?.SelectedItems?.OfType<GameEntry>() ?? [];

    public static void ClearMultiSelection(this GridView? gridView)
    {
        if (gridView?.SelectedItems?.Count > 0)
        {
            gridView.SelectedItems.Clear();
        }
    }

    public static void ClearSingleSelection(this GridView? gridView)
    {
        gridView?.SelectedItem = null;
    }
}

