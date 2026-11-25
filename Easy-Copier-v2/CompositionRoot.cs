using EasyCopier.Features.Shell;
using EasyCopier.Shared;
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

        services.AddSharedFoundation();
        services.AddShellSlice();

        return services.BuildServiceProvider();
    }
}
