using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;

namespace EasyCopier.Features.History;

/// <summary>
/// Page content displaying the 3-tier spatial transfer history dashboard, workspace table, and details drawer.
/// </summary>
public sealed partial class HistoryPage : Page
{
    /// <summary>Gets the <see cref="HistoryViewModel"/> associated with this page.</summary>
    public HistoryViewModel ViewModel { get; private set; } = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="HistoryPage"/> class.
    /// </summary>
    public HistoryPage()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        base.OnNavigatedTo(e);
        if (e.Parameter is HistoryViewModel viewModel)
        {
            ViewModel = viewModel;
            Bindings.Update();
            _ = ViewModel.InitializeAsync();
        }
    }

    private void EscapeAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (ViewModel?.IsDrawerOpen == true && ViewModel.CloseDrawerCommand.CanExecute(null))
        {
            ViewModel.CloseDrawerCommand.Execute(null);
            args.Handled = true;
        }
    }
}
