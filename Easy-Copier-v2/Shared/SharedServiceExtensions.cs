using System.Globalization;
using CommunityToolkit.Mvvm.Messaging;
using EasyCopier.Shared.Configuration;
using EasyCopier.Shared.Persistence;
using EasyCopier.Shared.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Serilog;

namespace EasyCopier.Shared;

/// <summary>
/// Dependency injection registration extensions for the cross-cutting <c>Shared/</c> foundation.
/// </summary>
public static class SharedServiceExtensions
{
    /// <summary>
    /// Registers logging, cross-slice messaging, SQLite persistence factory, configuration, and platform services.
    /// </summary>
    public static IServiceCollection AddSharedFoundation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string logFolder = Path.Combine(appDataFolder, "EasyCopier", "Logs");
        _ = Directory.CreateDirectory(logFolder);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Debug(formatProvider: CultureInfo.InvariantCulture)
            .WriteTo.File(
                Path.Combine(logFolder, "log-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                formatProvider: CultureInfo.InvariantCulture)
            .CreateLogger();

        _ = services.AddLogging(builder =>
        {
            _ = builder.ClearProviders();
            _ = builder.AddSerilog(dispose: true);
        });

        services.TryAddSingleton<IMessenger>(WeakReferenceMessenger.Default);
        services.TryAddSingleton<ISqliteConnectionFactory, SqliteConnectionFactory>();
        services.TryAddSingleton<ISettingsService, SettingsService>();

        services.TryAddSingleton<IAppWindowContext, AppWindowContext>();
        services.TryAddSingleton<IDispatcherService, DispatcherService>();
        services.TryAddSingleton<IFolderPickerService, FolderPickerService>();
        services.TryAddSingleton<IFilePickerService, FilePickerService>();
        services.TryAddSingleton<IWindowService, WindowService>();
        services.TryAddSingleton<IDialogService, DialogService>();
        services.TryAddSingleton<IAudioPlaybackService, AudioPlaybackService>();
        services.TryAddSingleton<IProcessService, ProcessService>();
        services.TryAddSingleton<IFileSystemService, FileSystemService>();
        services.TryAddSingleton<ISourceFolderValidator, SourceFolderValidator>();
        services.TryAddSingleton<IClipboardService, ClipboardService>();
        services.TryAddSingleton<ITransferActivityMonitor, NullTransferActivityMonitor>();
        services.TryAddSingleton<ISystemTrayService, SystemTrayService>();

        return services;
    }
}

