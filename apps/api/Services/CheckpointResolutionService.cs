using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Services;

public sealed class CheckpointResolutionService
{
    private readonly JobPilotDbContext _db;
    private readonly ReusableAnswerService _answers;

    public CheckpointResolutionService(JobPilotDbContext db, ReusableAnswerService answers)
    {
        _db = db;
        _answers = answers;
    }

    public async Task<Checkpoint?> ResolveAsync(Guid userId, Guid checkpointId, ResolveCheckpointRequest request)
    {
        var checkpoint = await _db.Checkpoints.FirstOrDefaultAsync(x => x.Id == checkpointId && x.UserId == userId);
        if (checkpoint is null) return null;

        checkpoint.ResolutionStatus = request.Action;
        checkpoint.ResolutionValue = request.Value;
        checkpoint.ResolvedAt = DateTimeOffset.UtcNow;

        if (ShouldPersistCheckpointAnswer(checkpoint, request))
            await _answers.UpsertScreeningAnswerAsync(userId, checkpoint.FieldLabel, request.Value!.Trim());

        await _db.SaveChangesAsync();
        return checkpoint;
    }

    private static bool ShouldPersistCheckpointAnswer(Checkpoint checkpoint, ResolveCheckpointRequest request)
    {
        if (!checkpoint.ReasonCode.Equals("answer_needs_confirmation", StringComparison.OrdinalIgnoreCase)) return false;
        if (string.IsNullOrWhiteSpace(checkpoint.FieldLabel) || string.IsNullOrWhiteSpace(request.Value)) return false;

        return request.Action.Equals("resolved", StringComparison.OrdinalIgnoreCase) ||
               request.Action.Equals("save_answer", StringComparison.OrdinalIgnoreCase) ||
               request.Action.Equals("answered", StringComparison.OrdinalIgnoreCase);
    }
}
