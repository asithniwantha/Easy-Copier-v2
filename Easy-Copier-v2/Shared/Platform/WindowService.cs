using EasyCopier.Shared.Interop;
using Microsoft.UI.Xaml;

namespace EasyCopier.Shared.Platform;

/// <summary>
/// Implements decoupled window creation and activation using slice-contributed <see cref="WindowRegistration"/> factories.
/// Secondary windows are recreated fresh per call and anchored to <c>MainWindow</c> via HWND ownership.
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

        // Always instantiate a fresh Window via DI factory rather than hiding/reusing closed WinUI Window objects.
        Window window = createWindow(owner, parameter);
        NativeWindowHelper.EnsureOwner(window, owner);

        if (onClosed != null)
        {
            window.Closed += (_, _) => onClosed();
        }

        window.Activate();
    }
}
