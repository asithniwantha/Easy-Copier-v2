using EasyCopier.Shared.Platform;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Foundation;

namespace EasyCopier.Features.Shell.Contracts;

/// <summary>
/// Implements <see cref="IFlyoutService"/> using slice-registered <see cref="IFlyoutContentProvider"/> instances.
/// </summary>
public sealed class FlyoutService : IFlyoutService
{
    private readonly IProcessService _processService;
    private readonly List<IFlyoutContentProvider> _providers;

    public FlyoutService(IProcessService processService, IEnumerable<IFlyoutContentProvider> providers)
    {
        _processService = processService ?? throw new ArgumentNullException(nameof(processService));
        ArgumentNullException.ThrowIfNull(providers);
        _providers = [.. providers];
    }

    /// <inheritdoc />
    public void HandleOpenFolderClick(object sender)
    {
        if (sender is Button { DataContext: GameEntry entry } && !string.IsNullOrEmpty(entry.FolderPath))
        {
            _processService.OpenInExplorer(entry.FolderPath);
        }
    }

    /// <inheritdoc />
    public async Task ShowDetailsFlyoutAsync(object sender, Point? position, GameEntry? entry = null)
    {
        if (sender is not FrameworkElement fe)
        {
            return;
        }

        entry ??= fe.DataContext as GameEntry;
        if (entry == null)
        {
            return;
        }

        IFlyoutContentProvider? provider = _providers.FirstOrDefault(p => p.CanHandle(entry.Category));
        if (provider == null)
        {
            return;
        }

        (UIElement Content, Style? PresenterStyle)? result = await provider.CreateContentAsync(entry);
        if (result == null)
        {
            return;
        }

        PresentFlyout(fe, position, result.Value.Content, result.Value.PresenterStyle);
    }

    private static void PresentFlyout(FrameworkElement targetElement, Point? position, UIElement content, Style? presenterStyle)
    {
        Flyout flyout = new()
        {
            Content = content,
            Placement = FlyoutPlacementMode.RightEdgeAlignedTop
        };

        if (presenterStyle != null)
        {
            flyout.FlyoutPresenterStyle = presenterStyle;
        }

        FlyoutShowOptions? options = position.HasValue ? new FlyoutShowOptions { Position = position.Value } : null;
        flyout.ShowAt(targetElement, options);
    }
}

