using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.ApplicationModel.DataTransfer;

namespace EasyCopier.Shared.Platform;

/// <summary>
/// Windows platform implementation of <see cref="IClipboardService"/> using the WinRT <see cref="Clipboard"/> API.
/// </summary>
public class ClipboardService : IClipboardService
{
    /// <inheritdoc />
    public void SetText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        try
        {
            DataPackage package = new();
            package.SetText(text);
            Clipboard.SetContent(package);
        }
        catch (Exception ex) when (ex is COMException or UnauthorizedAccessException or InvalidOperationException)
        {
            Debug.WriteLine($"Failed to set clipboard content: {ex.Message}");
        }
    }
}

