namespace EasyCopier.Shared.Platform;

/// <summary>
/// Defines file system inspection and metadata operations, abstracting direct disk I/O calls from ViewModels.
/// </summary>
public interface IFileSystemService
{
    bool DirectoryExists(string? path);
    bool FileExists(string? path);
    string[] GetDirectories(string path);
    string[] GetFiles(string path);
    long CalculateDirectorySize(string path);
    (DateTime? Created, DateTime? Modified, DateTime? Accessed) GetPathMetadata(string path);
    IEnumerable<(string RelativePath, long Length)> EnumerateFilesRelative(string path);
}

