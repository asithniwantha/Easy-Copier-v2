using Microsoft.UI.Xaml.Controls;

namespace EasyCopier.Features.Library.Details;

/// <summary>
/// Flyout content showing the path, files, and sizes of a film or TV series folder.
/// </summary>
public sealed partial class VideoDetailsFlyout : UserControl
{
    /// <summary>Gets the <see cref="VideoDetailsViewModel"/> backing this flyout.</summary>
    public VideoDetailsViewModel ViewModel { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="VideoDetailsFlyout"/> class.
    /// </summary>
    public VideoDetailsFlyout(VideoDetailsViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
    }
}

