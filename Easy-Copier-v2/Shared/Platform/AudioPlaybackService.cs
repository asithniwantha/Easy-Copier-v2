using System.Media;
using Microsoft.Extensions.Logging;

namespace EasyCopier.Shared.Platform;

/// <summary>
/// Provides functionality to play audio feedback sound files asynchronously.
/// </summary>
public class AudioPlaybackService : IAudioPlaybackService
{
    private readonly ILogger<AudioPlaybackService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AudioPlaybackService"/> class with the specified logger.
    /// </summary>
    /// <param name="logger">The logger instance used to record playback warnings or errors.</param>
    public AudioPlaybackService(ILogger<AudioPlaybackService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public void PlaySuccessSound()
    {
        PlaySoundFile("Assets/successful_task_completion.wav");
    }

    /// <inheritdoc />
    public void PlayFailureSound()
    {
        PlaySoundFile("Assets/Failed_operation.wav");
    }

    private void PlaySoundFile(string relativePath)
    {
        try
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string fullPath = Path.Combine(basePath, relativePath);

            if (File.Exists(fullPath))
            {
                _ = Task.Run(() =>
                {
                    try
                    {
                        using SoundPlayer player = new(fullPath);
                        player.PlaySync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to play sound: {Path}", fullPath);
                    }
                });
            }
            else
            {
                _logger.LogWarning("Sound file not found: {Path}", fullPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating sound playback for {Path}", relativePath);
        }
    }
}

