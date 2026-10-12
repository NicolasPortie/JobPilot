using JobPilot.Api.Application;
using JobPilot.Api.Domain;
using JobPilot.Api.Infrastructure.Persistence;

namespace JobPilot.Api.Services;

public sealed class CheckpointResolutionService(
    IRepository<Checkpoint> checkpoints,
    IUnitOfWork unitOfWork,
    ReusableAnswerService answers)
{
    public async Task<ServiceResult<CheckpointResponse>> ResolveAsync(Guid userId, Guid checkpointId, ResolveCheckpointRequest request)
    {
        var checkpoint = await checkpoints.FirstOrDefaultAsync(x => x.Id == checkpointId && x.UserId == userId);
        if (checkpoint is null) return ServiceResult<CheckpointResponse>.NotFound();

        checkpoint.ResolutionStatus = request.Action;
        checkpoint.ResolutionValue = request.Value;
        checkpoint.ResolvedAt = DateTimeOffset.UtcNow;

        if (ShouldPersistCheckpointAnswer(checkpoint, request))
            await answers.UpsertScreeningAnswerAsync(userId, checkpoint.FieldLabel, request.Value!.Trim());

        await unitOfWork.SaveChangesAsync();
        return ServiceResult<CheckpointResponse>.Ok(CheckpointResponse.From(checkpoint));
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
