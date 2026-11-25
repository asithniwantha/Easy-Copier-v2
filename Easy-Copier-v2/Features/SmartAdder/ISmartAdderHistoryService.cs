using EasyCopier.Features.SmartAdder.Models;

namespace EasyCopier.Features.SmartAdder;

/// <summary>
/// Defines operations for persisting and retrieving Smart Adder calculations in SQLite storage.
/// </summary>
public interface ISmartAdderHistoryService
{
    /// <summary>
    /// Asynchronously initializes the <c>SmartAdderHistory</c> table in the SQLite database.
    /// </summary>
    /// <returns>A task representing the asynchronous initialization operation.</returns>
    Task InitializeAsync();

    /// <summary>
    /// Asynchronously inserts a Smart Adder calculation record into the SQLite database.
    /// </summary>
    /// <param name="record">The <see cref="SmartAdderHistoryRecord"/> to insert.</param>
    /// <returns>A task representing the asynchronous insert operation.</returns>
    Task AddRecordAsync(SmartAdderHistoryRecord record);

    /// <summary>
    /// Asynchronously retrieves the most recent Smart Adder calculation history records.
    /// </summary>
    /// <param name="count">The maximum number of recent records to retrieve.</param>
    /// <returns>A task containing a list of recent <see cref="SmartAdderHistoryRecord"/> items.</returns>
    Task<List<SmartAdderHistoryRecord>> GetRecentRecordsAsync(int count);
}
