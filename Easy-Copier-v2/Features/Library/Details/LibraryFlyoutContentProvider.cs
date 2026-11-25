using EasyCopier.Features.Library.Metadata;
using EasyCopier.Features.Shell.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace EasyCopier.Features.Library.Details;

/// <summary>
/// Supplies <see cref="GameDetailsFlyout"/> and <see cref="VideoDetailsFlyout"/> instances to the unified shell <see cref="IFlyoutService"/>.
/// </summary>
public sealed class LibraryFlyoutContentProvider : IFlyoutContentProvider
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IGameRequirementsService _gameRequirementsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="LibraryFlyoutContentProvider"/> class.
    /// </summary>
    public LibraryFlyoutContentProvider(IServiceProvider serviceProvider, IGameRequirementsService gameRequirementsService)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _gameRequirementsService = gameRequirementsService ?? throw new ArgumentNullException(nameof(gameRequirementsService));
    }

    /// <inheritdoc />
    public bool CanHandle(LibraryCategory category)
        => category is LibraryCategory.Game or LibraryCategory.App or LibraryCategory.TvAndFilm;

    /// <inheritdoc />
    public async Task<(UIElement Content, Style? PresenterStyle)?> CreateContentAsync(GameEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.Category == LibraryCategory.TvAndFilm)
        {
            VideoDetailsViewModel videoVm = _serviceProvider.GetRequiredService<VideoDetailsViewModel>();
            VideoDetailsFlyout videoFlyout = new(videoVm);
            _ = videoVm.InitializeAsync(entry.Name, entry.FolderPath);
            return (videoFlyout, null);
        }

        string formattedText = await _gameRequirementsService.GetFormattedRequirementsAsync(entry.FolderPath);
        GameDetailsViewModel gameDetailsViewModel = _serviceProvider.GetRequiredService<GameDetailsViewModel>();
        GameDetailsFlyout detailsFlyout = new(gameDetailsViewModel, formattedText, entry.FolderPath);

        Style flyoutStyle = new(typeof(FlyoutPresenter));
        flyoutStyle.Setters.Add(new Setter(FrameworkElement.MaxWidthProperty, double.PositiveInfinity));

        return (detailsFlyout, flyoutStyle);
    }
}

