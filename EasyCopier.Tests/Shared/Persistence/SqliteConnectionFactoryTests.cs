using EasyCopier.Shared.Persistence;
using Microsoft.Data.Sqlite;
using Xunit;

namespace EasyCopier.Tests.Shared.Persistence;

public sealed class SqliteConnectionFactoryTests
{
    [Fact]
    public async Task CustomConnectionString_CreatesAndOpensInMemoryDatabase()
    {
        string dbName = $"TestDb_{Guid.NewGuid():N}";
        string connString = $"Data Source={dbName};Mode=Memory;Cache=Shared";

        using SqliteConnection anchor = new(connString);
        await anchor.OpenAsync();

        var factory = new SqliteConnectionFactory(connString);
        Assert.Equal(":memory:", factory.DatabasePath);

        using SqliteConnection conn1 = await factory.CreateOpenConnectionAsync();
        using (SqliteCommand createCmd = conn1.CreateCommand())
        {
            createCmd.CommandText = "CREATE TABLE Ping (Id INTEGER PRIMARY KEY, Val TEXT NOT NULL); INSERT INTO Ping (Val) VALUES ('ok');";
            _ = await createCmd.ExecuteNonQueryAsync();
        }

        using SqliteConnection conn2 = await factory.CreateOpenConnectionAsync();
        using SqliteCommand readCmd = conn2.CreateCommand();
        readCmd.CommandText = "SELECT Val FROM Ping LIMIT 1;";
        object? val = await readCmd.ExecuteScalarAsync();

        Assert.Equal("ok", val);
    }

    [Fact]
    public void ParameterlessConstructor_ResolvesAppDataHistoryDbPath()
    {
        var factory = new SqliteConnectionFactory();

        Assert.EndsWith(Path.Combine("EasyCopier", "history.db"), factory.DatabasePath, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Data Source=", factory.ConnectionString, StringComparison.Ordinal);
    }
}

