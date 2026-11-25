using EasyCopier.Shared.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace EasyCopier.Features.SmartAdder;

/// <summary>
/// Dependency injection registration extensions for the SmartAdder feature slice.
/// </summary>
public static class SmartAdderSliceExtensions
{
    /// <summary>
    /// Registers SmartAdder services, ViewModels, and modal window factories.
    /// </summary>
    public static IServiceCollection AddSmartAdderSlice(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ISmartAdderHistoryService, SmartAdderHistoryService>();
        services.AddTransient<SmartAdderViewModel>();
        services.AddTransient<SmartAdderHistoryViewModel>();
        services.AddSingleton(sp => new WindowRegistration(
            AppWindowKind.SmartAdderHistory,
            (owner, _) => new SmartAdderHistoryWindow(sp.GetRequiredService<SmartAdderHistoryViewModel>(), owner)));

        return services;
    }
}
