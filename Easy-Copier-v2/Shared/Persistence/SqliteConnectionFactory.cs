using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace EasyCopier.Shared.Persistence;

/// <summary>
/// Default <see cref="ISqliteConnectionFactory"/> targeting <c>%AppData%\EasyCopier\history.db</c>,
/// with an injectable connection-string overload for headless in-memory unit testing.
/// </summary>
public sealed class SqliteConnectionFactory : ISqliteConnectionFactory
{
    private const string DatabaseFileName = "history.db";

    /// <inheritdoc />
    public string DatabasePath { get; }

    /// <inheritdoc />
    public string ConnectionString { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteConnectionFactory"/> class targeting <c>%AppData%\EasyCopier\history.db</c>.
    /// </summary>
    [ActivatorUtilitiesConstructor]
    public SqliteConnectionFactory()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string appFolder = Path.Combine(appData, "EasyCopier");
        _ = Directory.CreateDirectory(appFolder);

        DatabasePath = Path.Combine(appFolder, DatabaseFileName);
        ConnectionString = $"Data Source={DatabasePath}";
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteConnectionFactory"/> class using a custom connection string (for tests).
    /// </summary>
    /// <param name="connectionString">Explicit SQLite connection string (e.g., an in-memory shared cache database).</param>
    /// <param name="databasePath">Optional logical database path label for diagnostics.</param>
    public SqliteConnectionFactory(string connectionString, string? databasePath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ConnectionString = connectionString;
        DatabasePath = databasePath ?? ":memory:";
    }

    /// <inheritdoc />
    public SqliteConnection CreateConnection() => new(ConnectionString);

    /// <inheritdoc />
    public async Task<SqliteConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        SqliteConnection connection = CreateConnection();
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}

