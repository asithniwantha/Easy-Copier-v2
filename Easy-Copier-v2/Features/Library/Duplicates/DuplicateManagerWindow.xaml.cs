using EasyCopier.Shared.Interop;
using Microsoft.UI.Xaml;

namespace EasyCopier.Features.Library.Duplicates;

/// <summary>
/// Modal host window displaying detected duplicate library items with one-click cleanup controls.
/// </summary>
public sealed partial class DuplicateManagerWindow : Window
{
    /// <summary>Gets the <see cref="DuplicateManagerViewModel"/> driving this window.</summary>
    public DuplicateManagerViewModel ViewModel { get; }

    private readonly Window _owner;

    /// <summary>Event raised when the duplicate manager window is closed.</summary>
    public event EventHandler? DuplicateManagerClosed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DuplicateManagerWindow"/> class.
    /// </summary>
    public DuplicateManagerWindow(DuplicateManagerViewModel viewModel, Window owner)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));

        InitializeComponent();

        NativeWindowHelper.InitializeModalWindow(this, _owner, ViewModel, 1020, 720);
        Closed += (_, _) => DuplicateManagerClosed?.Invoke(this, EventArgs.Empty);

        if (ViewModel.DuplicateGroups.Count == 0 && !ViewModel.IsScanning)
        {
            _ = ViewModel.ScanDuplicatesAsync();
        }
    }

    private void GroupSelectOlder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DuplicateGroupViewModel group })
        {
            group.SelectOlderCopies();
        }
    }

    private void GroupSelectNewer_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DuplicateGroupViewModel group })
        {
            group.SelectNewerCopies();
        }
    }

    private void GroupDeselectAll_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DuplicateGroupViewModel group })
        {
            group.DeselectAll();
        }
    }
}

