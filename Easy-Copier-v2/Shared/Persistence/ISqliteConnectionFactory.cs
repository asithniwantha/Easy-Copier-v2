using Microsoft.Data.Sqlite;

namespace EasyCopier.Shared.Persistence;

/// <summary>
/// Encapsulates creating and opening SQLite connections for local persistence and in-memory unit testing.
/// </summary>
public interface ISqliteConnectionFactory
{
    /// <summary>
    /// Gets the resolved local SQLite database file path (or <c>:memory:</c> / custom source for tests).
    /// </summary>
    string DatabasePath { get; }

    /// <summary>
    /// Gets the SQLite connection string used by this factory.
    /// </summary>
    string ConnectionString { get; }

    /// <summary>
    /// Creates a new closed <see cref="SqliteConnection"/> configured with <see cref="ConnectionString"/>.
    /// </summary>
    SqliteConnection CreateConnection();

    /// <summary>
    /// Creates and asynchronously opens a new <see cref="SqliteConnection"/>.
    /// </summary>
    Task<SqliteConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default);
}

