using Npgsql;

namespace JobPilot.Api.Tests.Integration;

public sealed class PostgresTestDatabaseTests
{
    [PostgresFact]
    [Trait("Category", "PostgreSQL")]
    public async Task Lifecycle_CreatesAndDeletesOnlyItsOwnDatabase()
    {
        var configured = new NpgsqlConnectionStringBuilder(
            Environment.GetEnvironmentVariable(PostgresTestDatabase.EnvironmentVariable))
        { Pooling = false };
        await using var admin = new NpgsqlConnection(configured.ConnectionString);
        await admin.OpenAsync();
        await using var list = new NpgsqlCommand("SELECT datname FROM pg_database ORDER BY datname", admin);
        var before = new List<string>();
        await using (var reader = await list.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync()) before.Add(reader.GetString(0));
        }

        string ownName;
        await using (var database = await PostgresTestDatabase.CreateAsync())
        {
            ownName = new NpgsqlConnectionStringBuilder(database.ConnectionString).Database!;
            Assert.StartsWith("jobpilot_test_", ownName);
            Assert.DoesNotContain(ownName, before);
            Assert.NotEqual(configured.Database, ownName);
            await using var connection = new NpgsqlConnection(database.ConnectionString);
            await connection.OpenAsync();
            await using var current = new NpgsqlCommand("SELECT current_database()", connection);
            Assert.Equal(ownName, await current.ExecuteScalarAsync());
        }

        await using var exists = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @name)", admin);
        exists.Parameters.AddWithValue("name", ownName);
        Assert.Equal(false, await exists.ExecuteScalarAsync());
        exists.Parameters["name"].Value = configured.Database!;
        Assert.Equal(true, await exists.ExecuteScalarAsync());
    }
}
