using EasyCopier.Features.History;
using EasyCopier.Features.Shell;
using EasyCopier.Features.Shell.Contracts;
using EasyCopier.Features.SmartAdder;
using EasyCopier.Shared.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppNotifications;
using Serilog;

namespace EasyCopier;

/// <summary>
/// Provides application-specific startup, unhandled exception capture, notification registration, and DI lifecycle management.
/// </summary>
public partial class App : Application
{
    private Window? _window;
    private IServiceProvider? _serviceProvider;
    private bool _servicesDisposed;
    private bool _notificationsRegistered;

    /// <summary>Gets the current <see cref="App"/> instance.</summary>
    public static new App Current => (App)Application.Current;

    /// <summary>Gets the primary application <see cref="Window"/> instance.</summary>
    public Window? MainWindow { get; private set; }

    /// <summary>Gets the configured dependency injection <see cref="IServiceProvider"/>.</summary>
    public IServiceProvider Services => _serviceProvider ?? throw new InvalidOperationException("Services not initialized");

    /// <summary>
    /// Initializes the singleton application object, configures DI services, and wires global exception handlers.
    /// </summary>
    public App()
    {
        InitializeComponent();
        _serviceProvider = CompositionRoot.ConfigureServices();

        UnhandledException += App_UnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        if (_serviceProvider is not null)
        {
            ILogger<App> logger = _serviceProvider.GetRequiredService<ILogger<App>>();
            logger.LogCritical(e.Exception, "A fatal XAML exception occurred.");
        }

        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        if (_serviceProvider is not null && e.ExceptionObject is Exception ex)
        {
            ILogger<App> logger = _serviceProvider.GetRequiredService<ILogger<App>>();
            logger.LogCritical(ex, "A fatal application domain exception occurred.");
        }
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        if (e.Exception.InnerExceptions.Any(ex => ex is TaskCanceledException or OperationCanceledException))
        {
            e.SetObserved();
            return;
        }

        if (_serviceProvider is not null)
        {
            ILogger<App> logger = _serviceProvider.GetRequiredService<ILogger<App>>();
            logger.LogWarning(e.Exception, "An unobserved task exception occurred.");
        }

        e.SetObserved();
    }

    /// <summary>Invoked when the application is launched.</summary>
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        ILogger<App>? logger = null;
        try
        {
            logger = Services.GetRequiredService<ILogger<App>>();

            bool isToastSupported = AppNotificationManager.IsSupported();
            if (isToastSupported)
            {
                logger.LogInformation("AppNotificationManager is supported. Registering...");
                try
                {
                    AppNotificationManager.Default.NotificationInvoked += AppNotificationManager_NotificationInvoked;
                    AppNotificationManager.Default.Register();
                    _notificationsRegistered = true;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to register AppNotificationManager during startup.");
                }
            }
            else
            {
                logger.LogWarning("AppNotificationManager.IsSupported() returned false. Toast notifications will not be displayed.");
            }

            logger.LogInformation("Easy Copier application starting up.");

            IFlyoutService flyoutService = Services.GetRequiredService<IFlyoutService>();
            FlyoutHelper.Initialize(flyoutService);

            ShellViewModel shellViewModel = Services.GetRequiredService<ShellViewModel>();
            if (!isToastSupported || !_notificationsRegistered)
            {
                shellViewModel.ShowGlobalNotification(
                    "Desktop Notifications Disabled",
                    "Windows desktop toast notifications are unavailable (running elevated or missing Windows App SDK components). In-app notifications will be used instead.",
                    isSuccess: false);
            }

            ISystemTrayService systemTrayService = Services.GetRequiredService<ISystemTrayService>();

            ICopyHistoryService copyHistoryService = Services.GetRequiredService<ICopyHistoryService>();
            await copyHistoryService.InitializeAsync();

            ISmartAdderHistoryService smartAdderHistoryService = Services.GetRequiredService<ISmartAdderHistoryService>();
            await smartAdderHistoryService.InitializeAsync();

            _window = Services.GetRequiredService<MainWindow>();
            MainWindow = _window;

            _window.Closed += (_, _) =>
            {
                logger.LogInformation("Easy Copier application shutting down.");
                systemTrayService.Dispose();

                if (_notificationsRegistered && AppNotificationManager.IsSupported())
                {
                    AppNotificationManager.Default.Unregister();
                    AppNotificationManager.Default.NotificationInvoked -= AppNotificationManager_NotificationInvoked;
                    _notificationsRegistered = false;
                }

                DisposeServices();
            };

            _window.Activate();
        }
        catch (Exception ex)
        {
            if (logger is not null)
            {
                logger.LogCritical(ex, "Fatal error occurred during application startup sequence.");
            }
            else
            {
                System.Diagnostics.Trace.TraceError("Fatal error during OnLaunched before logger was acquired: {0}", ex);
            }
        }
    }

    private void AppNotificationManager_NotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
    {
        if (_serviceProvider is not null)
        {
            ILogger<App> logger = _serviceProvider.GetRequiredService<ILogger<App>>();
            logger.LogInformation("App notification invoked: {Arguments}", args.Argument);
        }
    }

    /// <summary>Disposes the dependency injection service provider and flushes pending log sinks.</summary>
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
