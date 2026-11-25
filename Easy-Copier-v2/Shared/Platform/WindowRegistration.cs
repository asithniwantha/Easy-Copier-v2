using Microsoft.UI.Xaml;

namespace EasyCopier.Shared.Platform;

/// <summary>
/// Represents a slice-contributed window factory registration for <see cref="IWindowService"/>.
/// </summary>
/// <param name="Kind">The window kind handled by this registration.</param>
/// <param name="CreateWindow">Factory creating a fresh <see cref="Window"/> instance anchored to the owner window.</param>
public sealed record WindowRegistration(AppWindowKind Kind, Func<Window, object?, Window> CreateWindow);

