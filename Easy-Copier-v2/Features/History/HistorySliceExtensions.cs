using EasyCopier.Shared.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace EasyCopier.Features.History;

/// <summary>
/// Dependency injection registration extensions for the History feature slice.
/// </summary>
public static class HistorySliceExtensions
{
    /// <summary>
    /// Registers History database persistence, reporting, analytics, ViewModels, and HistoryWindow route.
    /// </summary>
    public static IServiceCollection AddHistorySlice(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ICopyHistoryService, CopyHistoryService>();
        services.AddSingleton<IReportService, ReportService>();
        services.AddSingleton<IHistoryAnalysisService, HistoryAnalysisService>();
        services.AddTransient<HistoryViewModel>();

        services.AddSingleton(sp => new WindowRegistration(
            AppWindowKind.History,
            (owner, _) => new HistoryWindow(sp.GetRequiredService<HistoryViewModel>(), owner)));

        return services;
    }
}
