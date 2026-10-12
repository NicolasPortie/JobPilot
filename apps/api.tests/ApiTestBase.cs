using JobPilot.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JobPilot.Api.Tests;

public sealed class ApiTestFixture
{
    public WebApplicationFactory<Program> Factory { get; } = new ApiTestFactory();
}

public sealed class ApiTestFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"test-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Auth:JwtKey", "test-jwt-key-with-more-than-thirty-two-characters-long");
        builder.UseSetting("Security:SecretKey", "test-secret-key-with-more-than-thirty-two-characters");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=unused;Database=unused");
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "false");
        builder.UseSetting("Database:SeedOnStartup", "false");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<JobPilotDbContext>();
            services.RemoveAll<DbContextOptions<JobPilotDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<JobPilotDbContext>>();
            services.AddDbContext<JobPilotDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
        });
    }
}
