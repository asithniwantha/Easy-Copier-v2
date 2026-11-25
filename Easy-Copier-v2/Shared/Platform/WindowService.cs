using Microsoft.UI.Xaml;

namespace EasyCopier.Shared.Platform;

/// <summary>
/// Implements decoupled window creation and activation using slice-contributed <see cref="WindowRegistration"/> factories.
/// </summary>
public class WindowService : IWindowService
{
    private readonly IAppWindowContext _appWindowContext;
    private readonly Dictionary<AppWindowKind, Func<Window, object?, Window>> _factories;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowService"/> class.
    /// </summary>
    public WindowService(IAppWindowContext appWindowContext, IEnumerable<WindowRegistration> registrations)
    {
        _appWindowContext = appWindowContext ?? throw new ArgumentNullException(nameof(appWindowContext));
        ArgumentNullException.ThrowIfNull(registrations);

        _factories = new Dictionary<AppWindowKind, Func<Window, object?, Window>>();
        foreach (WindowRegistration registration in registrations)
        {
            _factories[registration.Kind] = registration.CreateWindow;
        }
    }

    /// <inheritdoc />
    public void ShowWindow(AppWindowKind kind, object? parameter = null, Action? onClosed = null)
    {
        if (!_factories.TryGetValue(kind, out Func<Window, object?, Window>? createWindow))
        {
            throw new InvalidOperationException($"No window registration is configured for {kind}.");
        }

        if (_appWindowContext.MainWindow is not Window owner)
        {
            throw new InvalidOperationException("Cannot open a secondary window before MainWindow is available.");
        }

        Window window = createWindow(owner, parameter);
        if (onClosed != null)
        {
            window.Closed += (_, _) => onClosed();
        }

        window.Activate();
    }
}

