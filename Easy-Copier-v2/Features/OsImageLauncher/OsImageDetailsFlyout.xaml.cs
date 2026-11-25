using Microsoft.UI.Xaml.Controls;

namespace EasyCopier.Features.OsImageLauncher;

/// <summary>
/// User control displaying detailed file metadata for an OS image item in a flyout.
/// </summary>
public sealed partial class OsImageDetailsFlyout : UserControl
{
    /// <summary>
    /// Gets the <see cref="OsImageDetailsViewModel"/> backing this details view.
    /// </summary>
    public OsImageDetailsViewModel ViewModel { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OsImageDetailsFlyout"/> class.
    /// </summary>
    public OsImageDetailsFlyout(OsImageDetailsViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ViewModel = viewModel;
        InitializeComponent();
    }
}

