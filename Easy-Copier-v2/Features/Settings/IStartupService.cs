namespace EasyCopier.Features.Settings;

/// <summary>
/// Defines operations for configuring Windows startup behavior on user logon.
/// </summary>
public interface IStartupService
{
    /// <summary>
    /// Registers or unregisters the application to run automatically upon Windows user logon.
    /// </summary>
    /// <param name="enable"><c>true</c> to enable launch at logon; <c>false</c> to disable.</param>
    void UpdateStartOnLogon(bool enable);
}

