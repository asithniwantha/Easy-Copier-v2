using EasyCopier.Features.TransferQueue.Models;
using EasyCopier.Shared.Platform;

namespace EasyCopier.Features.TransferQueue;

/// <summary>
/// WinUI dialog implementation of <see cref="IConflictPromptService"/> displaying <see cref="ConflictDialogContent"/>.
/// </summary>
public sealed class ConflictPromptService : IConflictPromptService
{
    private readonly IDialogService _dialogService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConflictPromptService"/> class.
    /// </summary>
    public ConflictPromptService(IDialogService dialogService)
    {
        ArgumentNullException.ThrowIfNull(dialogService);
        _dialogService = dialogService;
    }

    /// <inheritdoc />
    public async Task<(CopyAction Action, bool ApplyToAll)> PromptConflictAsync(
        string itemName,
        long srcSize,
        int srcCount,
        long destSize,
        int destCount)
    {
        ConflictDialogContent content = new(itemName, srcSize, srcCount, destSize, destCount);

        DialogOutcome outcome = await _dialogService.ShowCustomContentDialogAsync(
            "Destination Folder Conflict",
            content,
            primaryButtonText: "Replace",
            secondaryButtonText: "Merge",
            closeButtonText: "Skip");

        CopyAction action = outcome switch
        {
            DialogOutcome.Primary => CopyAction.Replace,
            DialogOutcome.Secondary => CopyAction.Merge,
            _ => CopyAction.Skip
        };

        return (action, content.IsApplyToAllChecked);
    }
}
