using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyCopier.Shared.Platform;

namespace EasyCopier.Features.Settings.About;

/// <summary>
/// ViewModel backing the About dialog, presenting application version info, author details, and GitHub links.
/// </summary>
public partial class AboutViewModel : CloseableViewModelBase
{
    private readonly IProcessService _processService;
    private readonly IClipboardService _clipboardService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AboutViewModel"/> class.
    /// </summary>
    public AboutViewModel(IProcessService processService, IClipboardService clipboardService)
    {
        ArgumentNullException.ThrowIfNull(processService);
        ArgumentNullException.ThrowIfNull(clipboardService);

        _processService = processService;
        _clipboardService = clipboardService;
    }

    /// <summary>
    /// Gets the displayable application version string retrieved from assembly attributes.
    /// </summary>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "ViewModel properties are bound by instance references in XAML.")]
    public string AppVersion
    {
        get
        {
            string? version = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

            if (!string.IsNullOrEmpty(version))
            {
                int plusIndex = version.IndexOf('+', StringComparison.Ordinal);
                return plusIndex > 0 ? version[..plusIndex] : version;
            }

            Version? fallbackVersion = Assembly.GetExecutingAssembly().GetName().Version;
            return fallbackVersion is not null
                ? $"{fallbackVersion.Major}.{fallbackVersion.Minor}.{fallbackVersion.Build}"
                : "Unknown";
        }
    }

    /// <summary>
    /// Gets the developer/author information string.
    /// </summary>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "ViewModel properties are bound by instance references in XAML.")]
    public string DeveloperInfo => "Asith Niwantha";

    /// <summary>
    /// Gets or sets a value indicating whether version information was copied to the clipboard.
    /// </summary>
    [ObservableProperty]
    public partial bool IsCopied { get; set; } = false;

    [RelayCommand]
    private void OpenGitHubRepo()
    {
        _processService.OpenInExplorer("https://github.com/asithniwantha/Easy-Copier-v2");
    }

    [RelayCommand]
    private void OpenGitHubIssues()
    {
        _processService.OpenInExplorer("https://github.com/asithniwantha/Easy-Copier-v2/issues");
    }

    [RelayCommand]
    private void CopyVersionInfo()
    {
        try
        {
            _clipboardService.SetText($"Easy Copier v{AppVersion}\nOS: {Environment.OSVersion}\nRuntime: .NET {Environment.Version}");
            IsCopied = true;
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or UnauthorizedAccessException or ReflectionTypeLoadException)
        {
            IsCopied = false;
        }
    }
}

