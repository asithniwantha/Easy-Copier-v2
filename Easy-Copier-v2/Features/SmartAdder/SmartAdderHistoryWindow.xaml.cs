using EasyCopier.Shared.Interop;
using Microsoft.UI.Xaml;

namespace EasyCopier.Features.SmartAdder;

/// <summary>
/// Modal host window displaying saved SmartAdder calculation history records.
/// </summary>
public sealed partial class SmartAdderHistoryWindow : Window
{
    /// <summary>
    /// Gets the <see cref="SmartAdderHistoryViewModel"/> backing this view.
    /// </summary>
    public SmartAdderHistoryViewModel ViewModel { get; }

    private readonly Window _owner;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmartAdderHistoryWindow"/> class.
    /// </summary>
    /// <param name="viewModel">The view model for SmartAdder history.</param>
    /// <param name="owner">The parent owner window for modal placement.</param>
    public SmartAdderHistoryWindow(SmartAdderHistoryViewModel viewModel, Window owner)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(owner);

        ViewModel = viewModel;
        _owner = owner;
        InitializeComponent();
        NativeWindowHelper.InitializeModalWindow(this, _owner, ViewModel, 360, 720);

        _ = ViewModel.InitializeAsync();
    }
}

