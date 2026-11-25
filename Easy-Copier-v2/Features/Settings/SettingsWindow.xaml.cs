using EasyCopier.Shared.Interop;
using EasyCopier.Shared.Platform;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace EasyCopier.Features.Settings;

/// <summary>
/// Represents the settings dialog window for configuring source folders and application preferences.
/// </summary>
public sealed partial class SettingsWindow : Window
{
    /// <summary>
    /// Gets the view model managing the settings state and operations.
    /// </summary>
    public SettingsViewModel ViewModel { get; }

    /// <summary>
    /// Occurs when the settings window closes.
    /// </summary>
    public event EventHandler? SettingsClosed;

    private readonly Window _owner;
    private readonly SettingsOpenAction _openAction;
    private readonly ISettingsNavigationService _navigationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsWindow"/> class.
    /// </summary>
    public SettingsWindow(
        SettingsViewModel viewModel,
        Window owner,
        SettingsOpenAction openAction = SettingsOpenAction.None,
        ISettingsNavigationService? navigationService = null)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(owner);

        ViewModel = viewModel;
        _owner = owner;
        _openAction = openAction;
        _navigationService = navigationService ?? new SettingsNavigationService();

        InitializeComponent();
        SettingsRoot.DataContext = ViewModel;
        NativeWindowHelper.InitializeModalWindow(this, _owner, ViewModel, 1200, 720);

        Closed += (_, _) => SettingsClosed?.Invoke(this, EventArgs.Empty);

        _ = LoadAsync(openAction);

        if (Content is FrameworkElement rootElement)
        {
            NativeWindowHelper.EnableDynamicResizing(this, rootElement, 1400, 840);
        }
    }

    private async Task LoadAsync(SettingsOpenAction openAction)
    {
        await ViewModel.LoadSettingsAsync();
        if (openAction.ToFolderCategoryString() is string category)
        {
            await ViewModel.AddNewSourceFolderCommand.ExecuteAsync(category);
        }
    }

    private void SettingsNav_Loaded(object sender, RoutedEventArgs e)
    {
        string targetTag = _navigationService.GetTargetTagForAction(_openAction);

        foreach (object item in SettingsNav.MenuItems)
        {
            if (item is NavigationViewItem navItem && navItem.Tag?.ToString() == targetTag)
            {
                SettingsNav.SelectedItem = navItem;
                return;
            }
        }

        if (SettingsNav.MenuItems.Count > 0)
        {
            SettingsNav.SelectedItem = SettingsNav.MenuItems[0];
        }
    }

    private void SettingsNav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem selectedItem)
        {
            string tag = selectedItem.Tag?.ToString() ?? string.Empty;
            ViewModel.SelectedNavTag = tag;

            Type pageType = _navigationService.GetPageTypeForTag(tag);

            if (ContentFrame.CurrentSourcePageType != pageType)
            {
                _ = ContentFrame.Navigate(pageType, ViewModel, new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight });
            }
        }
    }

    internal static void AttachPageViewModel(
        Page page,
        Microsoft.UI.Xaml.Navigation.NavigationEventArgs e,
        Action<SettingsViewModel> setViewModel,
        Action updateBindings)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.Parameter is SettingsViewModel vm)
        {
            setViewModel(vm);
            page.DataContext = vm;
            updateBindings();
        }
    }
}
