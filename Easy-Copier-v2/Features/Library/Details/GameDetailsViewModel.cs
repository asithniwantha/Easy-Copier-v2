using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using EasyCopier.Features.Shell.Contracts;
using EasyCopier.Shared.Platform;
using EasyCopier.Shared.UI.Helpers;

namespace EasyCopier.Features.Library.Details;

/// <summary>
/// ViewModel for displaying detailed folder contents and status information of a game entry.
/// </summary>
public partial class GameDetailsViewModel : ObservableObject
{
    private readonly IDispatcherService _dispatcherService;
    private readonly IFileSystemService _fileSystemService;

    /// <summary>Gets the collection of file system items contained in the target game folder.</summary>
    public ObservableCollection<FileSystemItem> FolderContents { get; } = [];

    [ObservableProperty]
    public partial string FolderStatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsFolderStatusVisible { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GameDetailsViewModel"/> class.
    /// </summary>
    public GameDetailsViewModel(IDispatcherService dispatcherService, IFileSystemService fileSystemService)
    {
        _dispatcherService = dispatcherService ?? throw new ArgumentNullException(nameof(dispatcherService));
        _fileSystemService = fileSystemService ?? throw new ArgumentNullException(nameof(fileSystemService));
    }

    /// <summary>
    /// Asynchronously loads directory and file items from the specified folder path.
    /// </summary>
    public Task LoadFolderContentsAsync(string folderPath)
    {
        FolderContents.Clear();
        IsFolderStatusVisible = false;

        try
        {
            if (_fileSystemService.DirectoryExists(folderPath))
            {
                IOrderedEnumerable<string> dirs = _fileSystemService.GetDirectories(folderPath).OrderBy(d => d);
                IOrderedEnumerable<string> files = _fileSystemService.GetFiles(folderPath).OrderBy(f => f);

                foreach (string dir in dirs)
                {
                    FileSystemItem item = new(dir, true);
                    FolderContents.Add(item);
                    _ = CalculateFolderSizeAsync(item);
                }

                foreach (string file in files)
                {
                    FolderContents.Add(new FileSystemItem(file, false));
                }

                if (FolderContents.Count == 0)
                {
                    FolderStatusMessage = "Empty folder";
                    IsFolderStatusVisible = true;
                }
            }
            else
            {
                FolderStatusMessage = "Folder not found";
                IsFolderStatusVisible = true;
            }
        }
        catch (Exception ex)
        {
            FolderStatusMessage = $"Error loading folder: {ex.Message}";
            IsFolderStatusVisible = true;
        }

        return Task.CompletedTask;
    }

    private async Task CalculateFolderSizeAsync(FileSystemItem item)
    {
        if (!item.IsFolder)
        {
            return;
        }

        await Task.Run(() =>
        {
            try
            {
                long size = _fileSystemService.CalculateDirectorySize(item.Path);
                _ = _dispatcherService.TryEnqueue(() =>
                {
                    item.SizeFormatted = FormattingHelpers.FormatBytes(size);
                });
            }
            catch
            {
                _ = _dispatcherService.TryEnqueue(() =>
                {
                    item.SizeFormatted = "Unknown";
                });
            }
        }).ConfigureAwait(false);
    }
}

