using EasyCopier.Features.TransferQueue.Models;

namespace EasyCopier.Features.TransferQueue;

/// <summary>
/// Presents conflict resolution prompts when destination files or folders already exist,
/// decoupling <see cref="TransferQueueViewModel"/> from WinUI controls.
/// </summary>
public interface IConflictPromptService
{
    /// <summary>
    /// Displays a conflict prompt comparing source and destination statistics and returns the chosen action.
    /// </summary>
    Task<(CopyAction Action, bool ApplyToAll)> PromptConflictAsync(
        string itemName,
        long srcSize,
        int srcCount,
        long destSize,
        int destCount);
}

