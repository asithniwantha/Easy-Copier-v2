using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace EasyCopier.Features.Shell;

/// <summary>
/// The main content page housing library tabs, the Action Center queue panel, and SmartAdder overlay.
/// </summary>
public sealed partial class MainPage : Page
{
    /// <summary>Gets the <see cref="ShellViewModel"/> bound to this page.</summary>
    public ShellViewModel ViewModel { get; private set; } = null!;

    /// <summary>Initializes a new instance of the <see cref="MainPage"/> class.</summary>
    public MainPage()
    {
        InitializeComponent();
    }

    private void GlobalNotification_CloseButtonClick(InfoBar sender, object args)
    {
        if (ViewModel is not null)
        {
            ViewModel.IsGlobalNotificationVisible = false;
        }
    }

    /// <inheritdoc />
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        base.OnNavigatedTo(e);
        if (e.Parameter is ShellViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = ViewModel;
            Bindings.Update();

            _ = ViewModel.InitializeAsync();
        }
    }
}
