using Microsoft.Data.Sqlite;
using Xunit;

namespace EasyCopier.Tests.Shared;

public sealed class SetupSmokeTests
{
    [Fact]
    public void InMemorySqlite_ExecutesHeadlessly()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1;";
        var result = command.ExecuteScalar();

        Assert.Equal(1L, result);
    }
}

