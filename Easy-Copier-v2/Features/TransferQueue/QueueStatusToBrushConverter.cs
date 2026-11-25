using EasyCopier.Features.TransferQueue.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace EasyCopier.Features.TransferQueue;

/// <summary>
/// Converts a <see cref="TransferQueueItemStatus"/> to a theme accent brush reflecting status severity.
/// </summary>
public sealed class QueueStatusToBrushConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        string key = value is TransferQueueItemStatus status
            ? status switch
            {
                TransferQueueItemStatus.InProgress => "AccentFillColorDefaultBrush",
                TransferQueueItemStatus.Completed => "SystemFillColorSuccessBrush",
                TransferQueueItemStatus.Failed => "SystemFillColorCriticalBrush",
                TransferQueueItemStatus.Cancelled => "SystemFillColorCautionBrush",
                _ => "TextFillColorTertiaryBrush"
            }
            : "TextFillColorTertiaryBrush";

        return Application.Current.Resources[key];
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

