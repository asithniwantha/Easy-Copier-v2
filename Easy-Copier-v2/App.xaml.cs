using EasyCopier.Features.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace EasyCopier;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;

    public static new App Current => (App)Application.Current;

    public IServiceProvider Services { get; }

    /// <summary>
    /// Initializes the singleton application object and configures the DI service provider.
    /// </summary>
    public App()
    {
        InitializeComponent();
        Services = CompositionRoot.ConfigureServices();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = Services.GetRequiredService<MainWindow>();
        _window.Activate();
    }
}
