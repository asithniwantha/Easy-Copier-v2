using EasyCopier.Features.Shell.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace EasyCopier.Features.OsImageLauncher;

/// <summary>
/// Supplies <see cref="OsImageDetailsFlyout"/> instances to the unified shell <see cref="IFlyoutService"/>
/// for <see cref="LibraryCategory.OsImage"/> items.
/// </summary>
public sealed class OsImageFlyoutContentProvider : IFlyoutContentProvider
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="OsImageFlyoutContentProvider"/> class.
    /// </summary>
    public OsImageFlyoutContentProvider(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public bool CanHandle(LibraryCategory category) => category == LibraryCategory.OsImage;

    /// <inheritdoc />
    public Task<(UIElement Content, Style? PresenterStyle)?> CreateContentAsync(GameEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        OsImageDetailsViewModel vm = _serviceProvider.GetRequiredService<OsImageDetailsViewModel>();
        vm.Initialize(entry.Name, entry.FolderPath);
        OsImageDetailsFlyout flyout = new(vm);
        return Task.FromResult<(UIElement Content, Style? PresenterStyle)?>((flyout, null));
    }
}
