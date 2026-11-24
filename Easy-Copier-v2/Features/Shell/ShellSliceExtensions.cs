using Microsoft.Extensions.DependencyInjection;

namespace EasyCopier.Features.Shell;

/// <summary>
/// Registers Shell slice components with the dependency injection container.
/// </summary>
public static class ShellSliceExtensions
{
    public static IServiceCollection AddShellSlice(this IServiceCollection services)
    {
        services.AddSingleton<MainWindow>();
        return services;
    }
}

