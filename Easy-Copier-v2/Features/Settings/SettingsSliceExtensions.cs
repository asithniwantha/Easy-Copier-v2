using EasyCopier.Features.Settings.About;
using EasyCopier.Shared.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace EasyCopier.Features.Settings;

/// <summary>
/// Dependency injection registration extensions for the Settings and About feature slice.
/// </summary>
public static class SettingsSliceExtensions
{
    /// <summary>
    /// Registers Settings and About services, ViewModels, and modal window factories.
    /// </summary>
    public static IServiceCollection AddSettingsSlice(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IStartupService, StartupService>();
        services.AddSingleton<IUpdateService, UpdateService>();
        services.AddSingleton<ISettingsNavigationService, SettingsNavigationService>();

        services.AddTransient<SettingsViewModel>();
        services.AddTransient<AboutViewModel>();

        services.AddSingleton(sp => new WindowRegistration(
            AppWindowKind.Settings,
            (owner, param) => new SettingsWindow(
                sp.GetRequiredService<SettingsViewModel>(),
                owner,
                param is SettingsOpenAction action ? action : SettingsOpenAction.None,
                sp.GetRequiredService<ISettingsNavigationService>())));

        services.AddSingleton(sp => new WindowRegistration(
            AppWindowKind.About,
            (owner, _) => new AboutWindow(sp.GetRequiredService<AboutViewModel>(), owner)));

        return services;
    }
}
