using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using EasyCopier.Shared.Platform;

namespace EasyCopier.Features.OsImageLauncher;

/// <summary>
/// ViewModel for the OS Image details flyout, providing properties for display dates, name, and status information.
/// </summary>
public sealed partial class OsImageDetailsViewModel : ObservableObject
{
    private readonly IFileSystemService _fileSystemService;

    [ObservableProperty]
    public partial string ImageName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DateCreatedFormatted { get; set; } = "N/A";

    [ObservableProperty]
    public partial string DateModifiedFormatted { get; set; } = "N/A";

    [ObservableProperty]
    public partial string DateAccessedFormatted { get; set; } = "N/A";

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsStatusVisible { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OsImageDetailsViewModel"/> class.
    /// </summary>
    public OsImageDetailsViewModel(IFileSystemService fileSystemService)
    {
        ArgumentNullException.ThrowIfNull(fileSystemService);
        _fileSystemService = fileSystemService;
    }

    /// <summary>
    /// Initializes the ViewModel details by reading file/directory metadata from the specified file system path.
    /// </summary>
    public void Initialize(string name, string path)
    {
        ImageName = name;

        (DateTime? created, DateTime? modified, DateTime? accessed) = _fileSystemService.GetPathMetadata(path);

        if (!created.HasValue && !modified.HasValue && !accessed.HasValue)
        {
            StatusMessage = "File or folder not found";
            IsStatusVisible = true;
            DateCreatedFormatted = "N/A";
            DateModifiedFormatted = "N/A";
            DateAccessedFormatted = "N/A";
            return;
        }

        IsStatusVisible = false;
        StatusMessage = string.Empty;
        const string dateFormat = "dd/MM/yyyy hh:mm tt";

        DateCreatedFormatted = created.HasValue
            ? created.Value.ToString(dateFormat, CultureInfo.CurrentCulture)
            : "N/A";

        DateModifiedFormatted = modified.HasValue
            ? modified.Value.ToString(dateFormat, CultureInfo.CurrentCulture)
            : "N/A";

        DateAccessedFormatted = accessed.HasValue
            ? accessed.Value.ToString(dateFormat, CultureInfo.CurrentCulture)
            : "N/A";
    }
}

