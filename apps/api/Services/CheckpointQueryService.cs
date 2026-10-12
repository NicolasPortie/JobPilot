using JobPilot.Api.Application;
using JobPilot.Api.Domain;
using JobPilot.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Services;

public sealed class CheckpointQueryService(IRepository<Checkpoint> checkpoints)
{
    public async Task<List<CheckpointResponse>> ListOpenAsync(Guid userId) =>
        (await checkpoints.Query()
            .Where(checkpoint => checkpoint.UserId == userId && checkpoint.ResolutionStatus == "open")
            .OrderByDescending(checkpoint => checkpoint.CreatedAt)
            .ToListAsync())
        .Select(CheckpointResponse.From)
        .ToList();
}
