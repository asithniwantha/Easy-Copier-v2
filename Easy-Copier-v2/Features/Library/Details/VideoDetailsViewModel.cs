using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using EasyCopier.Shared.Platform;
using EasyCopier.Shared.UI.Helpers;

namespace EasyCopier.Features.Library.Details;

/// <summary>
/// Represents a single file listed in the video details flyout.
/// </summary>
public sealed record VideoFileInfo(string Name, string SizeFormatted, bool IsLarge);

/// <summary>
/// ViewModel for the film/TV details flyout, exposing the folder path, file list, and total size.
/// </summary>
public partial class VideoDetailsViewModel : ObservableObject
{
    private const long Fat32MaxFileBytes = 4_294_967_295;

    private readonly IFileSystemService _fileSystemService;

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FolderPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Summary { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsStatusVisible { get; set; }

    /// <summary>Gets the files contained in the video folder.</summary>
    public ObservableCollection<VideoFileInfo> Files { get; } = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="VideoDetailsViewModel"/> class.
    /// </summary>
    public VideoDetailsViewModel(IFileSystemService fileSystemService)
    {
        _fileSystemService = fileSystemService ?? throw new ArgumentNullException(nameof(fileSystemService));
    }

    /// <summary>
    /// Loads the file list for the given folder off the UI thread.
    /// </summary>
    public async Task InitializeAsync(string title, string folderPath)
    {
        Title = title;
        FolderPath = folderPath;
        Files.Clear();

        (List<VideoFileInfo> files, long total, string? error) = await Task.Run(() => LoadFiles(folderPath));

        if (error != null)
        {
            StatusMessage = error;
            IsStatusVisible = true;
            Summary = string.Empty;
            return;
        }

        IsStatusVisible = false;
        foreach (VideoFileInfo file in files)
        {
            Files.Add(file);
        }

        Summary = $"{files.Count} file(s) \u2022 {FormattingHelpers.FormatBytes(total)}";
    }

    private (List<VideoFileInfo> Files, long Total, string? Error) LoadFiles(string folderPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !_fileSystemService.DirectoryExists(folderPath))
            {
                return ([], 0, "Folder not found");
            }

            List<(string RelativePath, long Length)> rawFiles = [.. _fileSystemService.EnumerateFilesRelative(folderPath)];
            List<VideoFileInfo> files = [.. rawFiles
                .OrderBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase)
                .Select(f => new VideoFileInfo(
                    f.RelativePath,
                    FormattingHelpers.FormatBytes(f.Length),
                    f.Length > Fat32MaxFileBytes))];

            return (files, rawFiles.Sum(f => f.Length), null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return ([], 0, $"Unable to read folder: {ex.Message}");
        }
    }
}

