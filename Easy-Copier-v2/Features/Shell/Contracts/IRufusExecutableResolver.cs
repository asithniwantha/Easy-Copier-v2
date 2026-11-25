namespace EasyCopier.Features.Shell.Contracts;

/// <summary>
/// Resolves configured Rufus executable paths (and newer versioned binaries in the same directory)
/// without coupling consumers like Settings to the <c>OsImageLauncher</c> slice.
/// </summary>
public interface IRufusExecutableResolver
{
    /// <summary>
    /// Resolves the latest Rufus executable path given a configured path string.
    /// </summary>
    string ResolveLatestRufusPath(string? currentRufusPath);
}

/// <summary>
/// Default fallback resolver that expands environment variables until <c>OsImageLauncher</c> registers versioned probing.
/// </summary>
public sealed class DefaultRufusExecutableResolver : IRufusExecutableResolver
{
    /// <inheritdoc />
    public string ResolveLatestRufusPath(string? currentRufusPath)
    {
        return string.IsNullOrWhiteSpace(currentRufusPath)
            ? string.Empty
            : Environment.ExpandEnvironmentVariables(currentRufusPath);
    }
}
