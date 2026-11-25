using EasyCopier.Features.Shell.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace EasyCopier.Features.Library.Tabs;

/// <summary>
/// UserControl representing the Games tab in the library view.
/// </summary>
public sealed partial class GamesTabView : UserControl, ILibraryTabView
{
    /// <summary>Identifies the <see cref="ViewModel"/> dependency property.</summary>
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(GamesTabViewModel),
            typeof(GamesTabView),
            new PropertyMetadata(null, OnViewModelPropertyChanged));

    /// <summary>Gets or sets the <see cref="GamesTabViewModel"/> for this control.</summary>
    public GamesTabViewModel? ViewModel
    {
        get => (GamesTabViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    /// <summary>Event raised when the selection in the games grid changes.</summary>
    public event SelectionChangedEventHandler? SelectionChanged;

    /// <summary>Gets the selected items from the games grid.</summary>
    public IList<object> SelectedItems => GamesGridView.SelectedItems;

    /// <inheritdoc />
    public bool IsOsImagesTab => false;

    /// <summary>
    /// Initializes a new instance of the <see cref="GamesTabView"/> class.
    /// </summary>
    public GamesTabView()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    public IEnumerable<GameEntry> GetSelectedEntries() => GamesGridView.GetSelectedEntries();

    /// <inheritdoc />
    public void ClearSelection() => GamesGridView.ClearMultiSelection();

    private static void OnViewModelPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GamesTabView view)
        {
            if (e.OldValue is GamesTabViewModel oldVm)
            {
                oldVm.ClearSelectionRequested -= view.OnClearSelectionRequested;
            }

            if (e.NewValue is GamesTabViewModel newVm)
            {
                newVm.ClearSelectionRequested += view.OnClearSelectionRequested;
            }
        }
    }

    private void OnClearSelectionRequested(object? sender, EventArgs e) => ClearSelection();

    private void GamesGridView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel?.UpdateSelection(GamesGridView.GetSelectedEntries());
        SelectionChanged?.Invoke(this, e);
    }
}

