using EasyCopier.Features.Library.Details;
using EasyCopier.Features.Library.Duplicates;
using EasyCopier.Features.Library.Metadata;
using EasyCopier.Features.Library.Scanning;
using EasyCopier.Features.Library.Tabs;
using EasyCopier.Features.Shell.Contracts;
using EasyCopier.Shared.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EasyCopier.Features.Library;

/// <summary>
/// Dependency injection registration extensions for the Library and Duplicates feature slice.
/// </summary>
public static class LibrarySliceExtensions
{
    /// <summary>
    /// Registers Library scanning, caching, metadata download, flyouts, duplicates cleanup, and tab ViewModels.
    /// </summary>
    public static IServiceCollection AddLibrarySlice(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IThumbnailService, ThumbnailService>();
        services.AddSingleton<IGameScannerService, GameScannerService>();
        services.AddSingleton<ILibraryScannerService, LibraryScannerService>();
        services.AddSingleton<ILibraryFilterService, LibraryFilterService>();
        services.AddSingleton<ILibraryCacheService, LibraryCacheService>();
        services.AddSingleton<ISourceLibraryService, SourceLibraryService>();
        services.AddSingleton<IGameRequirementsService, GameRequirementsService>();
        services.AddSingleton<ISafeDeletionService, SafeDeletionService>();

        services.AddSingleton<GameInfoDownloadService>();
        services.AddSingleton<IGameInfoDownloadService>(sp => sp.GetRequiredService<GameInfoDownloadService>());
        services.Replace(ServiceDescriptor.Singleton<IGameMetadataDownloader>(sp => sp.GetRequiredService<GameInfoDownloadService>()));

        services.AddSingleton<ILibraryCatalogService, LibraryCatalogService>();
        services.AddSingleton<GamesTabViewModel>();
        services.AddSingleton<AppsTabViewModel>();
        services.AddSingleton<TvAndFilmsTabViewModel>();

        services.AddTransient<GameDetailsViewModel>();
        services.AddTransient<VideoDetailsViewModel>();
        services.AddTransient<DuplicateManagerViewModel>();

        services.AddSingleton<IFlyoutContentProvider, LibraryFlyoutContentProvider>();

        services.AddSingleton(sp => new WindowRegistration(
            AppWindowKind.DuplicateManager,
            (owner, _) => new DuplicateManagerWindow(sp.GetRequiredService<DuplicateManagerViewModel>(), owner)));

        return services;
    }
}

