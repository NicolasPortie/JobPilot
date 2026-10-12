using JobPilot.Api.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace JobPilot.Api.Tests.Integration;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PostgresTestDatabase.EnvironmentVariable)))
            Skip = $"PostgreSQL disabled locally: set {PostgresTestDatabase.EnvironmentVariable} to a test cluster admin connection.";
    }
}

// The supplied database is only used for administration. Never migrate or delete it.
public sealed class PostgresTestDatabase : IAsyncDisposable
{
    public const string EnvironmentVariable = "JOBPILOT_TEST_POSTGRES";
    private readonly string adminConnectionString;
    private readonly string databaseName = "jobpilot_test_" + Guid.NewGuid().ToString("N");
    private bool created;

    private PostgresTestDatabase(string adminConnectionString)
    {
        var admin = new NpgsqlConnectionStringBuilder(adminConnectionString) { Pooling = false };
        if (string.IsNullOrWhiteSpace(admin.Host) || string.IsNullOrWhiteSpace(admin.Database))
            throw new ArgumentException("Specify a test cluster host and administrative database.");
        this.adminConnectionString = admin.ConnectionString;
        admin.Database = databaseName;
        ConnectionString = admin.ConnectionString;
    }

    public string ConnectionString { get; }

    public static async Task<PostgresTestDatabase> CreateAsync()
    {
        var connection = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException($"Set {EnvironmentVariable} explicitly to a test cluster admin connection.");
        var database = new PostgresTestDatabase(connection);
        await using var admin = new NpgsqlConnection(database.adminConnectionString);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{database.databaseName}\"", admin);
        await command.ExecuteNonQueryAsync();
        database.created = true;
        return database;
    }

    public JobPilotDbContext CreateContext() => new(
        new DbContextOptionsBuilder<JobPilotDbContext>().UseNpgsql(ConnectionString).Options);

    public async ValueTask DisposeAsync()
    {
        if (!created) return;
        // Only this instance's successfully created, random database is eligible for cleanup.
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", admin);
        await command.ExecuteNonQueryAsync();
        created = false;
    }
}
