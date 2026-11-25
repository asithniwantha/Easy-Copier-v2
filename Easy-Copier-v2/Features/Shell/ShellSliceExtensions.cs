using EasyCopier.Features.Shell.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EasyCopier.Features.Shell;

/// <summary>
/// Registers Shell slice components and shared tab contracts with the dependency injection container.
/// </summary>
public static class ShellSliceExtensions
{
    public static IServiceCollection AddShellSlice(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ILibrarySelectionCoordinator, LibrarySelectionCoordinator>();
        services.AddSingleton<IFlyoutService, FlyoutService>();
        services.TryAddSingleton<IRufusExecutableResolver, DefaultRufusExecutableResolver>();
        services.TryAddSingleton<IGameMetadataDownloader, NullGameMetadataDownloader>();
        services.AddSingleton<MainWindow>();
        services.AddSingleton<ShellViewModel>();
        return services;
    }
}
