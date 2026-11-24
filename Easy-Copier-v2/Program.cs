using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Velopack;

namespace EasyCopier;

/// <summary>
/// Custom application entry point required to execute Velopack lifecycle hooks
/// before WinUI 3 initializes XAML.
/// </summary>
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().Run();

        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(static _ =>
        {
            var context = new DispatcherQueueSynchronizationContext(
                DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            new App();
        });
    }
}
