using JobPilot.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JobPilot.Api.Configuration;

public static class DatabaseInitializer
{
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<DatabaseStartupOptions>>().Value;
        if (!options.ApplyMigrationsOnStartup && !options.SeedOnStartup)
            return;

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<JobPilotDbContext>();

        if (options.RepairLegacyMigrationHistory)
            await RepairLegacyMigrationHistoryAsync(db);

        if (options.ApplyMigrationsOnStartup)
            await db.Database.MigrateAsync();

        if (options.SeedOnStartup)
            await DatabaseSeeder.SeedAsync(db, app.Configuration);
    }

    private static Task RepairLegacyMigrationHistoryAsync(JobPilotDbContext db) =>
        db.Database.ExecuteSqlRawAsync("""
            DO $$
            BEGIN
                IF to_regclass('public."Users"') IS NOT NULL AND to_regclass('public."__EFMigrationsHistory"') IS NULL THEN
                    CREATE TABLE "__EFMigrationsHistory" (
                        "MigrationId" character varying(150) NOT NULL,
                        "ProductVersion" character varying(32) NOT NULL,
                        CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
                    );

                    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                    VALUES ('20260522235433_InitialCreate', '10.0.8');
                END IF;
            END $$;
            """);
}
