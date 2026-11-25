using EasyCopier.Shared.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace EasyCopier.Features.TransferQueue;

/// <summary>
/// Dependency injection registration extensions for the TransferQueue feature slice.
/// </summary>
public static class TransferQueueSliceExtensions
{
    /// <summary>
    /// Registers drive discovery, validation, verification, transfer execution, notifications,
    /// background queue orchestration, system tray activity monitor, and <see cref="TransferQueueViewModel"/>.
    /// </summary>
    public static IServiceCollection AddTransferQueueSlice(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IDriveDiscoveryService, DriveDiscoveryService>();
        services.AddSingleton<IDriveValidationService, DriveValidationService>();
        services.AddSingleton<IDriveCoordinatorService, DriveCoordinatorService>();
        services.AddSingleton<IVerificationService, VerificationService>();
        services.AddSingleton<IFileTransferService, WindowsShellTransferService>();
        services.AddSingleton<IBatchNotificationService, BatchNotificationService>();
        services.AddSingleton<ITransferQueueService, TransferQueueService>();
        services.AddSingleton<IConflictPromptService, ConflictPromptService>();
        services.AddSingleton<ITransferActivityMonitor, TransferActivityMonitor>();
        services.AddSingleton<TransferQueueViewModel>();

        return services;
    }
}

