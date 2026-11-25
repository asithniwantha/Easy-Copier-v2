using EasyCopier.Features.Shell.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace EasyCopier.Features.Library.Tabs;

/// <summary>
/// UserControl representing the Applications tab in the library view.
/// </summary>
public sealed partial class AppsTabView : UserControl, ILibraryTabView
{
    /// <summary>Identifies the <see cref="ViewModel"/> dependency property.</summary>
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(AppsTabViewModel),
            typeof(AppsTabView),
            new PropertyMetadata(null, OnViewModelPropertyChanged));

    /// <summary>Gets or sets the <see cref="AppsTabViewModel"/> for this control.</summary>
    public AppsTabViewModel? ViewModel
    {
        get => (AppsTabViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    /// <summary>Event raised when the selection in the apps grid changes.</summary>
    public event SelectionChangedEventHandler? SelectionChanged;

    /// <summary>Gets the selected items from the apps grid.</summary>
    public IList<object> SelectedItems => AppsGridView.SelectedItems;

    /// <inheritdoc />
    public bool IsOsImagesTab => false;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppsTabView"/> class.
    /// </summary>
    public AppsTabView()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    public IEnumerable<GameEntry> GetSelectedEntries() => AppsGridView.GetSelectedEntries();

    /// <inheritdoc />
    public void ClearSelection() => AppsGridView.ClearMultiSelection();

    private static void OnViewModelPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AppsTabView view)
        {
            if (e.OldValue is AppsTabViewModel oldVm)
            {
                oldVm.ClearSelectionRequested -= view.OnClearSelectionRequested;
            }

            if (e.NewValue is AppsTabViewModel newVm)
            {
                newVm.ClearSelectionRequested += view.OnClearSelectionRequested;
            }
        }
    }

    private void OnClearSelectionRequested(object? sender, EventArgs e) => ClearSelection();

    private void AppsGridView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel?.UpdateSelection(AppsGridView.GetSelectedEntries());
        SelectionChanged?.Invoke(this, e);
    }
}

