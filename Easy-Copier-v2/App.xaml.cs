using EasyCopier.Features.Shell;
using EasyCopier.Features.SmartAdder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Serilog;

namespace EasyCopier;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;
    private IServiceProvider? _serviceProvider;
    private bool _servicesDisposed;

    public static new App Current => (App)Application.Current;

    public Window? MainWindow { get; private set; }

    public IServiceProvider Services => _serviceProvider ?? throw new InvalidOperationException("Services not initialized");

    /// <summary>
    /// Initializes the singleton application object and configures the DI service provider.
    /// </summary>
    public App()
    {
        InitializeComponent();
        _serviceProvider = CompositionRoot.ConfigureServices();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = Services.GetRequiredService<MainWindow>();
        MainWindow = _window;
        _window.Closed += (_, _) => DisposeServices();
        _window.Activate();

        _ = Services.GetRequiredService<ISmartAdderHistoryService>().InitializeAsync();
    }

    /// <summary>
    /// Disposes the dependency injection service provider and flushes pending log sinks.
    /// </summary>
    public void DisposeServices()
    {
        if (_servicesDisposed)
        {
            return;
        }

        _servicesDisposed = true;
        MainWindow = null;

        if (_serviceProvider is IDisposable disposable)
        {
            _serviceProvider = null;
            disposable.Dispose();
        }

        Log.CloseAndFlush();
    }
}
