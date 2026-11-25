using System.Globalization;
using System.Text;
using EasyCopier.Features.History.Models;
using Microsoft.Extensions.Logging;

namespace EasyCopier.Features.History;

/// <summary>
/// Provides reporting and CSV export services for operation records.
/// </summary>
public class ReportService(ILogger<ReportService> logger) : IReportService
{
    private readonly ILogger<ReportService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public async Task<bool> ExportHistoryToCsvAsync(string filePath, IEnumerable<CopyHistoryRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        try
        {
            await using StreamWriter writer = new(filePath, false, Encoding.UTF8);
            await writer.WriteLineAsync("Id,Timestamp,GameName,TargetDriveLetter,TargetDriveLabel,BytesTransferred,IsSuccess,Amount,SourcePath,DestinationPath,ErrorLog,SubFilesJson,DurationMs,StatusText").ConfigureAwait(false);

            foreach (CopyHistoryRecord record in records)
            {
                string id = record.Id.ToString(CultureInfo.InvariantCulture);
                string timestamp = record.Timestamp.ToString("O", CultureInfo.InvariantCulture);
                string gameName = EscapeCsv(record.GameName);
                string targetDriveLetter = EscapeCsv(record.TargetDriveLetter);
                string targetDriveLabel = EscapeCsv(record.TargetDriveLabel);
                string bytesTransferred = record.BytesTransferred.ToString(CultureInfo.InvariantCulture);
                string isSuccess = record.IsSuccess.ToString();
                string amount = record.Amount.ToString(CultureInfo.InvariantCulture);
                string sourcePath = EscapeCsv(record.SourcePath);
                string destinationPath = EscapeCsv(record.DestinationPath);
                string errorLog = EscapeCsv(record.ErrorLog);
                string subFilesJson = EscapeCsv(record.SubFilesJson);
                string durationMs = record.DurationMs.ToString(CultureInfo.InvariantCulture);
                string statusText = EscapeCsv(record.DisplayStatusText);

                await writer.WriteLineAsync($"{id},{timestamp},{gameName},{targetDriveLetter},{targetDriveLabel},{bytesTransferred},{isSuccess},{amount},{sourcePath},{destinationPath},{errorLog},{subFilesJson},{durationMs},{statusText}").ConfigureAwait(false);
            }

            _logger.LogInformation("Successfully exported history to {FilePath}", filePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export history to CSV at {FilePath}", filePath);
            return false;
        }
    }

    private static string EscapeCsv(string value)
    {
        return string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Contains(',', StringComparison.Ordinal) || value.Contains('"', StringComparison.Ordinal) || value.Contains('\r', StringComparison.Ordinal) || value.Contains('\n', StringComparison.Ordinal)
                ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
                : value;
    }
}
