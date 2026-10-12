namespace JobPilot.Api.Configuration;

public static class SecurityConfiguration
{
    public const string Issuer = "JobPilot";
    public const string Audience = "JobPilot";

    public static string JwtKey(IConfiguration configuration) =>
        GetRequiredSecret(configuration, "Auth:JwtKey", "Auth__JwtKey");

    public static string DataProtectionKey(IConfiguration configuration) =>
        FirstConfigured(
            configuration["Security:SecretKey"],
            Environment.GetEnvironmentVariable("JOBPILOT_SECRET_KEY"),
            Environment.GetEnvironmentVariable("JOBPILOT_SECRET_KEY", EnvironmentVariableTarget.User))
        ?? (AllowsLegacyJwtDataProtection(configuration)
            ? GetRequiredSecret(configuration, "Auth:JwtKey", "Auth__JwtKey")
            : throw new InvalidOperationException("Security:SecretKey is required and must be independent from Auth:JwtKey."));

    public static bool AllowsLegacyJwtDataProtection(IConfiguration configuration) =>
        configuration.GetValue("Security:AllowLegacyJwtDataProtection", false);

    public static void Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        ValidateSecret(JwtKey(configuration), "Auth:JwtKey");
        ValidateSecret(DataProtectionKey(configuration), "Security:SecretKey");

        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("DefaultConnection")))
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

        if (!environment.IsDevelopment() &&
            configuration.GetValue("Database:SeedOnStartup", false) &&
            (string.IsNullOrWhiteSpace(configuration["Auth:SeedEmail"]) ||
             string.IsNullOrWhiteSpace(configuration["Auth:SeedPassword"])))
        {
            throw new InvalidOperationException("Auth seed credentials are required when Database:SeedOnStartup is enabled.");
        }

        if (!environment.IsDevelopment() &&
            string.Equals(configuration["Auth:SeedPassword"], "admin123", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Auth:SeedPassword must not use the development default outside Development.");
        }
    }

    private static string GetRequiredSecret(IConfiguration configuration, string key, string environmentName)
    {
        var value = FirstConfigured(configuration[key], Environment.GetEnvironmentVariable(environmentName));
        return value is not null
            ? value
            : throw new InvalidOperationException($"{key} is required.");
    }

    private static string? FirstConfigured(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static void ValidateSecret(string value, string key)
    {
        if (value.Length < 32)
            throw new InvalidOperationException($"{key} must contain at least 32 characters.");
    }
}
