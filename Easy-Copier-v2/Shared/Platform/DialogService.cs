using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace EasyCopier.Shared.Platform;

/// <summary>
/// Implements UI dialog display services using WinUI 3 <see cref="ContentDialog"/> controls.
/// </summary>
public class DialogService : IDialogService
{
    private readonly IAppWindowContext _appWindowContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialogService"/> class.
    /// </summary>
    public DialogService(IAppWindowContext appWindowContext)
    {
        _appWindowContext = appWindowContext ?? throw new ArgumentNullException(nameof(appWindowContext));
    }

    /// <inheritdoc />
    public async Task ShowMessageDialogAsync(string title, string message, string closeButtonText = "OK")
    {
        if (_appWindowContext.MainXamlRoot is not XamlRoot xamlRoot)
        {
            return;
        }

        ContentDialog dialog = new()
        {
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            CloseButtonText = closeButtonText,
            XamlRoot = xamlRoot
        };

        _ = await dialog.ShowAsync();
    }

    /// <inheritdoc />
    public async Task<bool> ShowConfirmationDialogAsync(string title, string message, string primaryButtonText = "Yes", string closeButtonText = "No")
    {
        if (_appWindowContext.MainXamlRoot is not XamlRoot xamlRoot)
        {
            return false;
        }

        ContentDialog dialog = new()
        {
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = closeButtonText,
            XamlRoot = xamlRoot,
            DefaultButton = ContentDialogButton.Primary
        };

        ContentDialogResult result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    /// <inheritdoc />
    public async Task<DialogOutcome> ShowCustomContentDialogAsync(
        string title,
        object content,
        string primaryButtonText,
        string? secondaryButtonText = null,
        string closeButtonText = "Cancel")
    {
        if (_appWindowContext.MainXamlRoot is not XamlRoot xamlRoot)
        {
            return DialogOutcome.Close;
        }

        ContentDialog dialog = new()
        {
            Title = title,
            Content = content,
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = closeButtonText,
            XamlRoot = xamlRoot,
            DefaultButton = ContentDialogButton.Primary
        };

        if (!string.IsNullOrEmpty(secondaryButtonText))
        {
            dialog.SecondaryButtonText = secondaryButtonText;
        }

        ContentDialogResult result = await dialog.ShowAsync();
        return result switch
        {
            ContentDialogResult.Primary => DialogOutcome.Primary,
            ContentDialogResult.Secondary => DialogOutcome.Secondary,
            _ => DialogOutcome.Close
        };
    }
}

