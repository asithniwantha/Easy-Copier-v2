using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Xaml.Interactivity;

namespace EasyCopier.Features.Shell.Contracts;

/// <summary>
/// Behavior that attaches to an <see cref="AutoSuggestBox"/> to manage autocomplete suggestions,
/// query submissions, and background highlights for an <see cref="ISearchableTabViewModel"/>.
/// </summary>
public sealed class AutoSuggestBoxSearchBehavior : Behavior<AutoSuggestBox>
{
    private static readonly SolidColorBrush SearchHighlightBrush =
        new(ColorHelper.FromArgb(0x3C, 0xFF, 0xC1, 0x07));

    public static readonly DependencyProperty SearchableViewModelProperty =
        DependencyProperty.Register(
            nameof(SearchableViewModel),
            typeof(ISearchableTabViewModel),
            typeof(AutoSuggestBoxSearchBehavior),
            new PropertyMetadata(null));

    public ISearchableTabViewModel? SearchableViewModel
    {
        get => (ISearchableTabViewModel?)GetValue(SearchableViewModelProperty);
        set => SetValue(SearchableViewModelProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.TextChanged += OnTextChanged;
        AssociatedObject.SuggestionChosen += OnSuggestionChosen;
        AssociatedObject.QuerySubmitted += OnQuerySubmitted;
        UpdateHighlight(AssociatedObject.Text);
    }

    protected override void OnDetaching()
    {
        AssociatedObject.TextChanged -= OnTextChanged;
        AssociatedObject.SuggestionChosen -= OnSuggestionChosen;
        AssociatedObject.QuerySubmitted -= OnQuerySubmitted;
        base.OnDetaching();
    }

    private void OnTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput && SearchableViewModel != null)
        {
            sender.ItemsSource = SearchableViewModel.GetSuggestions(sender.Text);
        }

        UpdateHighlight(sender.Text);
    }

    private void OnSuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (SearchableViewModel != null && args?.SelectedItem is string chosenText)
        {
            SearchableViewModel.SearchText = chosenText;
        }
    }

    private void OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (SearchableViewModel != null && args != null)
        {
            SearchableViewModel.SearchText = args.QueryText;
        }
    }

    private void UpdateHighlight(string? text)
    {
        if (AssociatedObject == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(text))
        {
            AssociatedObject.Resources["TextControlBackground"] = SearchHighlightBrush;
            AssociatedObject.Resources["TextControlBackgroundPointerOver"] = SearchHighlightBrush;
            AssociatedObject.Resources["TextControlBackgroundFocused"] = SearchHighlightBrush;
            AssociatedObject.Background = SearchHighlightBrush;
        }
        else
        {
            _ = AssociatedObject.Resources.Remove("TextControlBackground");
            _ = AssociatedObject.Resources.Remove("TextControlBackgroundPointerOver");
            _ = AssociatedObject.Resources.Remove("TextControlBackgroundFocused");
            AssociatedObject.ClearValue(Control.BackgroundProperty);
        }
    }
}

