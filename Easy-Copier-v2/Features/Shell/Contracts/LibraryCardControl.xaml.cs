using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace EasyCopier.Features.Shell.Contracts;

/// <summary>
/// Reusable card control for displaying a library entry (game, application, TV/film, or OS image)
/// within grid views, encapsulating thumbnail presentation, metadata badges, hover video previews,
/// and contextual flyout interactions.
/// </summary>
public sealed partial class LibraryCardControl : UserControl
{
    private const int PreviewDelayMs = 500;

    private static readonly HashSet<string> PreviewExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".m4v", ".webm"
    };

    private CancellationTokenSource? _previewCts;
    private MediaPlayerElement? _previewElement;

    public static readonly DependencyProperty EntryProperty =
        DependencyProperty.Register(
            nameof(Entry),
            typeof(GameEntry),
            typeof(LibraryCardControl),
            new PropertyMetadata(null));

    public GameEntry? Entry
    {
        get => (GameEntry?)GetValue(EntryProperty);
        set => SetValue(EntryProperty, value);
    }

    public LibraryCardControl()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            if (DataContext is GameEntry entry)
            {
                Entry = entry;
                CoverImage?.Opacity = 0.0;
            }
        };

        Unloaded += (_, _) => StopPreview();
    }

    private void Card_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        OpenFolderButton.Opacity = 1.0;
        if (Application.Current.Resources.TryGetValue("CardBackgroundFillColorDefaultBrush", out object? brush) && brush is Brush b)
        {
            CardBorder.Background = b;
        }
    }

    private void Card_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        OpenFolderButton.Opacity = 0.0;
        if (Application.Current.Resources.TryGetValue("CardBackgroundFillColorSecondaryBrush", out object? brush) && brush is Brush b)
        {
            CardBorder.Background = b;
        }
    }

    private void CoverImage_ImageOpened(object sender, RoutedEventArgs e)
    {
        if (sender is Image img)
        {
            img.Opacity = 1.0;
        }
    }

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext == null && Entry != null)
        {
            fe.DataContext = Entry;
        }

        FlyoutHelper.HandleOpenFolderClick(sender);
    }

    private async void Card_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        await FlyoutHelper.ShowDetailsFlyoutAsync(sender, e, Entry);
    }

    private async void CoverArea_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (Entry?.Category != LibraryCategory.TvAndFilm ||
            e.Pointer.PointerDeviceType != PointerDeviceType.Mouse)
        {
            return;
        }

        StopPreview();
        CancellationTokenSource cts = new();
        _previewCts = cts;

        MediaPlayer? player = null;
        try
        {
            await Task.Delay(PreviewDelayMs, cts.Token);
            string? folderPath = Entry.FolderPath;
            if (string.IsNullOrEmpty(folderPath))
            {
                return;
            }

            string? videoPath = await Task.Run(() => FindPreviewVideo(folderPath), cts.Token);
            if (videoPath is null || cts.IsCancellationRequested)
            {
                return;
            }

            player = new MediaPlayer
            {
                IsMuted = true,
                IsLoopingEnabled = true,
                AutoPlay = true,
                Source = MediaSource.CreateFromUri(new Uri(videoPath))
            };

            player.MediaOpened += (s, _) =>
            {
                TimeSpan duration = s.PlaybackSession.NaturalDuration;
                if (duration > TimeSpan.FromSeconds(60))
                {
                    s.PlaybackSession.Position = duration * 0.2;
                }
            };

            if (cts.IsCancellationRequested)
            {
                return;
            }

            MediaPlayerElement element = new()
            {
                Stretch = Stretch.Uniform,
                Background = new SolidColorBrush(Colors.Black),
                AreTransportControlsEnabled = false,
                IsHitTestVisible = false
            };
            element.SetMediaPlayer(player);

            CoverAreaGrid.Children.Insert(Math.Min(2, CoverAreaGrid.Children.Count), element);
            _previewElement = element;
            player = null;
        }
        catch (OperationCanceledException)
        {
            // Pointer left before preview initialization completed
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Hover video preview failed: {ex.Message}");
        }
        finally
        {
            player?.Dispose();
        }
    }

    private void CoverArea_PointerExited(object sender, PointerRoutedEventArgs e) => StopPreview();

    private void StopPreview()
    {
        if (_previewCts is not null)
        {
            _previewCts.Cancel();
            _previewCts.Dispose();
            _previewCts = null;
        }

        if (_previewElement is not null)
        {
            _ = CoverAreaGrid.Children.Remove(_previewElement);
            _previewElement.MediaPlayer?.Dispose();
            _previewElement.SetMediaPlayer(null);
            _previewElement = null;
        }
    }

    private static string? FindPreviewVideo(string path)
    {
        try
        {
            return File.Exists(path)
                ? PreviewExtensions.Contains(Path.GetExtension(path)) ? path : null
                : !Directory.Exists(path)
                ? null
                : Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                    .Where(f => PreviewExtensions.Contains(Path.GetExtension(f)))
                    .OrderByDescending(f => new FileInfo(f).Length)
                    .FirstOrDefault();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }
}

