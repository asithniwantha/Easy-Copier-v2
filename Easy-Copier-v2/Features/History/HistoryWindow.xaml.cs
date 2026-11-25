using EasyCopier.Shared.Interop;
using Microsoft.UI.Xaml;

namespace EasyCopier.Features.History;

/// <summary>
/// Modal host window displaying file transfer operation history.
/// </summary>
public sealed partial class HistoryWindow : Window
{
    /// <summary>Gets the <see cref="HistoryViewModel"/> associated with this window.</summary>
    public HistoryViewModel ViewModel { get; }

    private readonly Window _owner;

    /// <summary>Event raised when the history window is closed.</summary>
    public event EventHandler? HistoryClosed;

    /// <summary>
    /// Initializes a new instance of the <see cref="HistoryWindow"/> class.
    /// </summary>
    public HistoryWindow(HistoryViewModel viewModel, Window owner)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));

        InitializeComponent();

        NativeWindowHelper.InitializeModalWindow(this, _owner, ViewModel, 1000, 700);
        Closed += (_, _) =>
        {
            ViewModel.IsActive = false;
            HistoryClosed?.Invoke(this, EventArgs.Empty);
        };

        _ = RootFrame.Navigate(typeof(HistoryPage), ViewModel);
    }
}

