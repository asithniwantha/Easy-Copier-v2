using EasyCopier.Shared.Platform;

namespace EasyCopier.Features.Settings;

/// <summary>
/// Service for resolving and managing navigation in the settings view.
/// </summary>
public interface ISettingsNavigationService
{
    /// <summary>
    /// Resolves the navigation tag for a specified startup open action.
    /// </summary>
    string GetTargetTagForAction(SettingsOpenAction action);

    /// <summary>
    /// Resolves the Page type corresponding to a navigation tag.
    /// </summary>
    Type GetPageTypeForTag(string? tag);
}

