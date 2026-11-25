using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace EasyCopier.Features.SmartAdder;

/// <summary>
/// User control providing the SmartAdder grid overlay UI for interactive price calculation.
/// </summary>
public sealed partial class SmartAdderControl : UserControl
{
    /// <summary>
    /// Identifies the <see cref="ViewModel"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(SmartAdderViewModel),
            typeof(SmartAdderControl),
            new PropertyMetadata(null, OnViewModelChanged));

    /// <summary>
    /// Gets or sets the <see cref="SmartAdderViewModel"/> for this control.
    /// </summary>
    public SmartAdderViewModel? ViewModel
    {
        get => (SmartAdderViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SmartAdderControl"/> class.
    /// </summary>
    public SmartAdderControl()
    {
        InitializeComponent();
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SmartAdderControl control && e.NewValue is SmartAdderViewModel newVm)
        {
            control.DataContext = newVm;
        }
    }
}

