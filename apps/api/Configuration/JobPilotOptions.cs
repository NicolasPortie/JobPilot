namespace JobPilot.Api.Configuration;

public sealed class DatabaseStartupOptions
{
    public const string SectionName = "Database";

    public bool ApplyMigrationsOnStartup { get; init; }
    public bool SeedOnStartup { get; init; }
    public bool RepairLegacyMigrationHistory { get; init; }
}

public sealed class FeatureOptions
{
    public const string SectionName = "Features";

    public bool EnableLegacyRuntime { get; init; }
}
