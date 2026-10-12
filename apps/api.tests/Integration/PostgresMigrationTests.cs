using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace JobPilot.Api.Tests.Integration;

public sealed class PostgresMigrationTests
{
    private const string PreviousMigration = "20260602102000_AddApplicationJobResume";
    private const string ReviewMigration = "20260602175220_AddResumeReviewAnswers";
    private static readonly string[] ExistingTables = [
        "ResumeImports", "ResumeDocuments", "ResumeVersions", "ResumeSections", "ResumeExports",
        "ResumeTailoringRuns", "JobTargets", "JobRequirements", "ResumeEvidenceMapRuns",
        "ResumeRequirementEvidences", "ApplicationJobs"
    ];

    [PostgresFact]
    [Trait("Category", "PostgreSQL")]
    public async Task EmptyDatabase_AppliesEntireMigrationChain()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var db = database.CreateContext();

        await db.Database.MigrateAsync();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Empty(await db.ResumeReviewAnswers.ToListAsync());
        Assert.Empty(await db.AiUsageLogs.ToListAsync());
    }

    [PostgresFact]
    [Trait("Category", "PostgreSQL")]
    public async Task PreviousVersion_UpgradeRollbackAndReapply_PreserveExistingDataAndObjects()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var db = database.CreateContext();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);
        var answer = await SeedExistingDataAsync(db);
        var originalData = await ReadDataAsync(database, ExistingTables);
        var originalSchema = await ReadSchemaAsync(database);

        await migrator.MigrateAsync(ReviewMigration);
        db.ResumeReviewAnswers.Add(answer);
        await db.SaveChangesAsync();
        Assert.Equal("Experiência comprovada", (await db.ResumeReviewAnswers.AsNoTracking().SingleAsync()).AnswerValue);
        Assert.Equal(originalData, await ReadDataAsync(database, ExistingTables));

        await migrator.MigrateAsync(PreviousMigration);
        Assert.Equal(originalData, await ReadDataAsync(database, ExistingTables));
        Assert.Equal(originalSchema, await ReadSchemaAsync(database));
        Assert.Equal(PreviousMigration, (await db.Database.GetAppliedMigrationsAsync()).Last());

        await db.Database.MigrateAsync();
        Assert.Equal(originalData, await ReadDataAsync(database, ExistingTables));
        db.ChangeTracker.Clear();
        db.ResumeReviewAnswers.Add(answer);
        await db.SaveChangesAsync();
        var tables = ExistingTables.Append("ResumeReviewAnswers").ToArray();
        var upgradedData = await ReadDataAsync(database, tables);
        var upgradedSchema = await ReadSchemaAsync(database);
        var history = await db.Database.GetAppliedMigrationsAsync();

        await db.Database.MigrateAsync();
        Assert.Equal(upgradedData, await ReadDataAsync(database, tables));
        Assert.Equal(upgradedSchema, await ReadSchemaAsync(database));
        Assert.Equal(history, await db.Database.GetAppliedMigrationsAsync());
    }

    [PostgresFact]
    [Trait("Category", "PostgreSQL")]
    public async Task IdempotentSql_InstallsAndReappliesWithoutChangingDataSchemaOrHistory()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var db = database.CreateContext();
        var script = db.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
        await ExecuteScriptAsync(database, script);
        var answer = await SeedExistingDataAsync(db);
        db.ResumeReviewAnswers.Add(answer);
        await db.SaveChangesAsync();
        var tables = ExistingTables.Append("ResumeReviewAnswers").Append("__EFMigrationsHistory").ToArray();
        var data = await ReadDataAsync(database, tables);
        var schema = await ReadSchemaAsync(database);

        await ExecuteScriptAsync(database, script);

        Assert.Equal(data, await ReadDataAsync(database, tables));
        Assert.Equal(schema, await ReadSchemaAsync(database));
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    private static async Task<ResumeReviewAnswer> SeedExistingDataAsync(JobPilotDbContext db)
    {
        var userId = Guid.NewGuid();
        // Use the historical columns: lockout fields only appear in later migrations.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Users" ("Id", "Email", "PasswordHash", "CreatedAt")
            VALUES ({userId}, {$"migration-{userId:N}@example.test"}, {"test-only-hash"}, {DateTimeOffset.UtcNow})
            """);
        var import = new ResumeImport { Id = Guid.NewGuid(), UserId = userId, RawText = "Currículo original" };
        var document = new ResumeDocument { Id = Guid.NewGuid(), UserId = userId, ResumeImportId = import.Id, Title = "Base" };
        var version = new ResumeVersion { Id = Guid.NewGuid(), UserId = userId, Document = document, PlainText = "Experiência PostgreSQL" };
        var export = new ResumeExport { Id = Guid.NewGuid(), UserId = userId, ResumeVersionId = version.Id, FilePath = "private/test.pdf", Format = "pdf" };
        var target = new JobTarget { Id = Guid.NewGuid(), UserId = userId, SourceText = "Vaga PostgreSQL" };
        var requirement = new JobRequirement { Id = Guid.NewGuid(), UserId = userId, JobTarget = target, Label = "PostgreSQL", ExactPhrases = ["PostgreSQL"] };
        var map = new ResumeEvidenceMapRun { Id = Guid.NewGuid(), UserId = userId, ResumeVersionId = version.Id, JobTargetId = target.Id };
        var run = new ApplicationRun { Id = Guid.NewGuid(), UserId = userId, Name = "Histórico" };
        var lead = new JobLead { Id = Guid.NewGuid(), UserId = userId, Title = "Oportunidade" };
        db.AddRange(import, document, version, export, target, requirement, map, run, lead,
            new ResumeSection { Id = Guid.NewGuid(), UserId = userId, Version = version, ContentJson = "{\"text\":\"Experiência\"}" },
            new ResumeTailoringRun { Id = Guid.NewGuid(), UserId = userId, ResumeVersionId = version.Id, ResultJson = "{\"preserved\":true}" },
            new ResumeRequirementEvidence { Id = Guid.NewGuid(), UserId = userId, Run = map, JobRequirementId = requirement.Id, EvidenceText = "Evidência original" },
            new ApplicationJob { Id = Guid.NewGuid(), UserId = userId, ApplicationRun = run, JobLead = lead, ResumeVersionId = version.Id, ResumeExportId = export.Id, ResumeFilePath = export.FilePath });
        await db.SaveChangesAsync();
        return new ResumeReviewAnswer { Id = Guid.NewGuid(), UserId = userId, JobRequirementId = requirement.Id, ResumeEvidenceMapRunId = map.Id, AnswerValue = "Experiência comprovada" };
    }

    private static async Task ExecuteScriptAsync(PostgresTestDatabase database, string script)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(script, connection);
        await command.ExecuteNonQueryAsync();
    }

    // Persistence is the seam: compare full rows, including IDs, timestamps and references.
    private static async Task<string[]> ReadDataAsync(PostgresTestDatabase database, string[] tables)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        var rows = new List<string>();
        foreach (var table in tables)
        {
            await using var command = new NpgsqlCommand($"SELECT to_jsonb(t)::text FROM \"{table}\" t ORDER BY to_jsonb(t)::text", connection);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) rows.Add(table + ":" + reader.GetString(0));
        }
        return rows.ToArray();
    }

    private static async Task<string[]> ReadSchemaAsync(PostgresTestDatabase database)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT description FROM (
                SELECT 'column:' || table_name || ':' || column_name || ':' || ordinal_position || ':' ||
                    udt_name || ':' || is_nullable || ':' || coalesce(column_default, '') AS description
                FROM information_schema.columns WHERE table_schema = 'public'
                UNION ALL
                SELECT 'index:' || tablename || ':' || indexname || ':' || indexdef
                FROM pg_indexes WHERE schemaname = 'public'
                UNION ALL
                SELECT 'constraint:' || c.relname || ':' || k.conname || ':' || pg_get_constraintdef(k.oid)
                FROM pg_constraint k JOIN pg_class c ON c.oid = k.conrelid
                JOIN pg_namespace n ON n.oid = c.relnamespace WHERE n.nspname = 'public'
            ) objects ORDER BY description
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<string>();
        while (await reader.ReadAsync()) rows.Add(reader.GetString(0));
        return rows.ToArray();
    }
}
