using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace EasyCopier.Features.Settings;

/// <summary>
/// Represents the general settings page in the settings navigation.
/// </summary>
public sealed partial class GeneralSettingsPage : Page
{
    /// <summary>
    /// Gets the settings view model associated with this page.
    /// </summary>
    public SettingsViewModel? ViewModel { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GeneralSettingsPage"/> class.
    /// </summary>
    public GeneralSettingsPage()
    {
        InitializeComponent();
        NavigationCacheMode = NavigationCacheMode.Required;
    }

    /// <inheritdoc/>
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        SettingsWindow.AttachPageViewModel(this, e, vm => ViewModel = vm, Bindings.Update);
    }
}

