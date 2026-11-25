namespace EasyCopier.Shared.Models;

/// <summary>
/// Represents the severity level of a validation result.
/// </summary>
public enum ValidationSeverity
{
    /// <summary>Success condition.</summary>
    Success,

    /// <summary>Informational validation message.</summary>
    Info,

    /// <summary>Warning condition that may require user attention.</summary>
    Warning,

    /// <summary>Error condition that prevents the operation from proceeding.</summary>
    Error
}

/// <summary>
/// Represents the result of a validation operation, including whether it is valid, the severity of the validation, and an associated message.
/// </summary>
/// <param name="IsValid">Indicates whether the validation passed without critical errors.</param>
/// <param name="Severity">The severity level of the validation outcome.</param>
/// <param name="Message">A descriptive validation message explaining the result.</param>
public record ValidationResult(
    bool IsValid,
    ValidationSeverity Severity,
    string Message);

