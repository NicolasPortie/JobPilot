namespace JobPilot.Api.Configuration;

public sealed class DatabaseStartupOptions
{
    public const string SectionName = "Database";

    public bool ApplyMigrationsOnStartup { get; init; }
    public bool SeedOnStartup { get; init; }
    public bool RepairLegacyMigrationHistory { get; init; }
}
