using EasyCopier.Features.Shell.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace EasyCopier.Features.Library.Tabs;

/// <summary>
/// UserControl representing the Films &amp; TV series tab in the library view.
/// </summary>
public sealed partial class TvAndFilmsTabView : UserControl, ILibraryTabView
{
    /// <summary>Identifies the <see cref="ViewModel"/> dependency property.</summary>
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(TvAndFilmsTabViewModel),
            typeof(TvAndFilmsTabView),
            new PropertyMetadata(null, OnViewModelPropertyChanged));

    /// <summary>Gets or sets the <see cref="TvAndFilmsTabViewModel"/> for this control.</summary>
    public TvAndFilmsTabViewModel? ViewModel
    {
        get => (TvAndFilmsTabViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    /// <summary>Event raised when the selection in the TV and films grid changes.</summary>
    public event SelectionChangedEventHandler? SelectionChanged;

    /// <summary>Gets the selected items from the TV and films grid.</summary>
    public IList<object> SelectedItems => TvAndFilmsGridView.SelectedItems;

    /// <inheritdoc />
    public bool IsOsImagesTab => false;

    /// <summary>
    /// Initializes a new instance of the <see cref="TvAndFilmsTabView"/> class.
    /// </summary>
    public TvAndFilmsTabView()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    public IEnumerable<GameEntry> GetSelectedEntries() => TvAndFilmsGridView.GetSelectedEntries();

    /// <inheritdoc />
    public void ClearSelection() => TvAndFilmsGridView.ClearMultiSelection();

    private static void OnViewModelPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TvAndFilmsTabView view)
        {
            if (e.OldValue is TvAndFilmsTabViewModel oldVm)
            {
                oldVm.ClearSelectionRequested -= view.OnClearSelectionRequested;
            }

            if (e.NewValue is TvAndFilmsTabViewModel newVm)
            {
                newVm.ClearSelectionRequested += view.OnClearSelectionRequested;
            }
        }
    }

    private void OnClearSelectionRequested(object? sender, EventArgs e) => ClearSelection();

    private void TvAndFilmsGridView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel?.UpdateSelection(TvAndFilmsGridView.GetSelectedEntries());
        SelectionChanged?.Invoke(this, e);
    }
}

