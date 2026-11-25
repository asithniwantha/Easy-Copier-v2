using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace EasyCopier.Features.Settings;

/// <summary>
/// Represents the films and TV settings page in the settings navigation.
/// </summary>
public sealed partial class FilmAndTvSettingsPage : Page
{
    /// <summary>
    /// Gets the settings view model associated with this page.
    /// </summary>
    public SettingsViewModel? ViewModel { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FilmAndTvSettingsPage"/> class.
    /// </summary>
    public FilmAndTvSettingsPage()
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

