using EasyCopier.Features.Shell.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace EasyCopier.Features.OsImageLauncher;

/// <summary>
/// Dependency injection registration extensions for the OsImageLauncher feature slice.
/// </summary>
public static class OsImageLauncherSliceExtensions
{
    /// <summary>
    /// Registers OS image scanning, Rufus launching, details flyouts, and ViewModels.
    /// </summary>
    public static IServiceCollection AddOsImageLauncherSlice(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IRufusExecutableResolver, RufusExecutableResolverAdapter>();
        services.AddSingleton<IIsoBootstrapperService, IsoBootstrapperService>();
        services.AddSingleton<OsImagesTabViewModel>();
        services.AddTransient<OsImageDetailsViewModel>();
        services.AddSingleton<IFlyoutContentProvider, OsImageFlyoutContentProvider>();

        return services;
    }
}
