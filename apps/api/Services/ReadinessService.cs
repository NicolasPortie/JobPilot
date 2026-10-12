using JobPilot.Api.Data;

namespace JobPilot.Api.Services;

public sealed class ReadinessService(JobPilotDbContext db)
{
    public Task<bool> CanConnectAsync(CancellationToken cancellationToken) =>
        db.Database.CanConnectAsync(cancellationToken);
}
