namespace EasyCopier.Shared.Platform;

/// <summary>
/// Defines operations for playing audio feedback notifications within the application.
/// </summary>
public interface IAudioPlaybackService
{
    /// <summary>
    /// Plays the sound effect designated for successful operation completion.
    /// </summary>
    void PlaySuccessSound();

    /// <summary>
    /// Plays the sound effect designated for failed operations.
    /// </summary>
    void PlayFailureSound();
}

