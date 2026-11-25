namespace EasyCopier.Shared.Platform;

/// <summary>
/// Represents the button selection outcome of a modal content dialog.
/// </summary>
public enum DialogOutcome
{
    /// <summary>The dialog was dismissed or the close button was selected.</summary>
    Close,

    /// <summary>The primary action button was selected.</summary>
    Primary,

    /// <summary>The secondary action button was selected.</summary>
    Secondary
}

/// <summary>
/// Provides an abstraction for displaying UI dialogs and prompts from ViewModels.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Displays a standard modal message dialog with a title, message body, and close button text.
    /// </summary>
    Task ShowMessageDialogAsync(string title, string message, string closeButtonText = "OK");

    /// <summary>
    /// Displays a confirmation dialog with primary and cancel action buttons.
    /// </summary>
    Task<bool> ShowConfirmationDialogAsync(string title, string message, string primaryButtonText = "Yes", string closeButtonText = "No");

    /// <summary>
    /// Displays a custom UI content dialog with primary, optional secondary, and close buttons.
    /// </summary>
    Task<DialogOutcome> ShowCustomContentDialogAsync(
        string title,
        object content,
        string primaryButtonText,
        string? secondaryButtonText = null,
        string closeButtonText = "Cancel");
}

