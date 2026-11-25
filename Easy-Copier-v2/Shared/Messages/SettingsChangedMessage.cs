namespace EasyCopier.Shared.Messages;

/// <summary>
/// Cross-slice message broadcast when settings affecting source library paths or price tiers are saved.
/// </summary>
public sealed record SettingsChangedMessage(
    bool SourcePathsUpdated,
    bool PriceTiersUpdated);
