using CommunityToolkit.Mvvm.Messaging;
using EasyCopier.Features.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace EasyCopier;

/// <summary>
/// Configures the application's dependency injection container across shared foundation and feature slices.
/// </summary>
public static class CompositionRoot
{
    public static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
        services.AddShellSlice();

        return services.BuildServiceProvider();
    }
}

