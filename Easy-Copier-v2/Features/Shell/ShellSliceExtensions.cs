using EasyCopier.Features.Shell.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace EasyCopier.Features.Shell;

/// <summary>
/// Registers Shell slice components and shared tab contracts with the dependency injection container.
/// </summary>
public static class ShellSliceExtensions
{
    public static IServiceCollection AddShellSlice(this IServiceCollection services)
    {
        services.AddSingleton<ILibrarySelectionCoordinator, LibrarySelectionCoordinator>();
        services.AddSingleton<IFlyoutService, FlyoutService>();
        services.AddSingleton<MainWindow>();
        return services;
    }
}
