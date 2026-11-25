using EasyCopier.Shared.UI.Helpers;
using Microsoft.UI.Xaml.Controls;

namespace EasyCopier.Features.TransferQueue;

/// <summary>
/// UserControl content for folder conflict resolution dialogs.
/// </summary>
public sealed partial class ConflictDialogContent : UserControl
{
    public string ItemNameMessage { get; }
    public string SourceSizeText { get; }
    public string SourceFilesText { get; }
    public string DestinationSizeText { get; }
    public string DestinationFilesText { get; }
    public bool IsApplyToAllChecked { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConflictDialogContent"/> class.
    /// </summary>
    public ConflictDialogContent(string itemName, long srcSize, int srcCount, long destSize, int destCount)
    {
        ItemNameMessage = $"The destination already contains an item named '{itemName}'.";
        SourceSizeText = $"Size: {FormattingHelpers.FormatBytes(srcSize)}";
        SourceFilesText = $"Files: {srcCount}";
        DestinationSizeText = $"Size: {FormattingHelpers.FormatBytes(destSize)}";
        DestinationFilesText = $"Files: {destCount}";

        InitializeComponent();
    }
}

