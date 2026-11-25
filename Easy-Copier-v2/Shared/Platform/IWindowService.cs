using Microsoft.UI.Xaml;

namespace EasyCopier.Shared.Platform;

/// <summary>
/// Identifies secondary modal windows exposed across application feature slices.
/// </summary>
public enum AppWindowKind
{
    /// <summary>The application Settings window.</summary>
    Settings,

    /// <summary>The transfer History window.</summary>
    History,

    /// <summary>The SmartAdder calculation history window.</summary>
    SmartAdderHistory,

    /// <summary>The About application dialog window.</summary>
    About,

    /// <summary>The Duplicate Library Manager window.</summary>
    DuplicateManager
}

/// <summary>
/// Specifies actions or tabs to activate when opening the settings window.
/// </summary>
public enum SettingsOpenAction
{
    /// <summary>No specific action or auto-add tab requested.</summary>
    None,

    /// <summary>Automatically prompt to add a new Game folder path.</summary>
    AddGameFolder,

    /// <summary>Automatically prompt to add a new Application folder path.</summary>
    AddAppFolder,

    /// <summary>Automatically prompt to add a new TV and Film folder path.</summary>
    AddTvAndFilmFolder,

    /// <summary>Automatically prompt to add a new OS Image folder path.</summary>
    AddOsImageFolder
}

/// <summary>
/// Extension methods for <see cref="SettingsOpenAction"/>.
/// </summary>
public static class SettingsOpenActionExtensions
{
    /// <summary>
    /// Maps the <see cref="SettingsOpenAction"/> enum value to its corresponding folder category string parameter.
    /// </summary>
    public static string? ToFolderCategoryString(this SettingsOpenAction action)
    {
        return action switch
        {
            SettingsOpenAction.AddAppFolder => "App",
            SettingsOpenAction.AddGameFolder => "Game",
            SettingsOpenAction.AddTvAndFilmFolder => "TvAndFilm",
            SettingsOpenAction.AddOsImageFolder => "OsImage",
            _ => null
        };
    }
}

/// <summary>
/// Represents a slice-contributed window factory registration for <see cref="IWindowService"/>.
/// </summary>
/// <param name="Kind">The window kind handled by this registration.</param>
/// <param name="CreateWindow">Factory creating a fresh <see cref="Window"/> instance anchored to the owner window.</param>
public sealed record WindowRegistration(AppWindowKind Kind, Func<Window, object?, Window> CreateWindow);

/// <summary>
/// Provides abstract methods for creating and displaying secondary windows in the application.
/// </summary>
public interface IWindowService
{
    /// <summary>
    /// Creates and activates a fresh secondary window for the specified <paramref name="kind"/>.
    /// </summary>
    /// <param name="kind">The window kind to open.</param>
    /// <param name="parameter">Optional launch parameter (e.g., <see cref="SettingsOpenAction"/>).</param>
    /// <param name="onClosed">Optional callback invoked when the created window closes.</param>
    void ShowWindow(AppWindowKind kind, object? parameter = null, Action? onClosed = null);
}

