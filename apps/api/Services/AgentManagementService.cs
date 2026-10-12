using JobPilot.Api.Application;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using JobPilot.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Services;

public sealed class AgentManagementService(
    IRepository<AgentPolicy> policies,
    IRepository<AgentDevice> devices,
    IRepository<CandidateProfile> profiles,
    IRepository<AgentRun> runs,
    IRepository<AgentStep> steps,
    IRepository<ApplicationField> fields,
    IRepository<ApplicationAnswer> answers,
    IRepository<SiteWorkflowMemory> memories,
    IUnitOfWork unitOfWork,
    IAiChatService ai,
    JwtTokenService tokens)
{
    public async Task<AgentPolicyResponse> GetPolicyAsync(Guid userId)
    {
        var policy = await policies.FirstOrDefaultAsync(candidate => candidate.UserId == userId);
        if (policy is null)
        {
            policy = new AgentPolicy { Id = Guid.NewGuid(), UserId = userId };
            policies.Add(policy);
            await unitOfWork.SaveChangesAsync();
        }

        return ToResponse(policy);
    }

    public async Task<AgentPolicyResponse> SavePolicyAsync(Guid userId, AgentPolicyRequest request)
    {
        var policy = await policies.FirstOrDefaultAsync(candidate => candidate.UserId == userId);
        if (policy is null)
        {
            policy = new AgentPolicy { Id = Guid.NewGuid(), UserId = userId };
            policies.Add(policy);
        }

        policy.Apply(new AgentPolicy
        {
            AutoSubmitMode = request.AutoSubmitMode,
            ExecutionTarget = request.ExecutionTarget,
            MaxApplicationsPerRun = request.MaxApplicationsPerRun,
            MaxApplicationsPerDay = request.MaxApplicationsPerDay,
            AllowedDomains = request.AllowedDomains,
            BlockedDomains = request.BlockedDomains,
            IsPaused = request.IsPaused
        });
        await unitOfWork.SaveChangesAsync();
        return ToResponse(policy);
    }

    public async Task<AgentHistoryResponse> GetHistoryAsync(Guid userId) =>
        new(
            (await runs.Query().Where(run => run.UserId == userId).OrderByDescending(run => run.StartedAt).Take(20).ToListAsync())
                .Select(run => new AgentRunHistoryItem(run.Id, run.ApplicationJobId, run.AgentName, run.Mode, run.State, run.PolicyMode, run.StartedAt, run.CompletedAt)).ToList(),
            (await steps.Query().Where(step => step.UserId == userId).OrderByDescending(step => step.CreatedAt).Take(40).ToListAsync())
                .Select(step => new AgentStepHistoryItem(step.Id, step.AgentRunId, step.ApplicationJobId, step.StepNumber, step.AgentName, step.PageState, step.ActionType, step.TargetLabel, step.Result, step.EvidencePath, step.Confidence, step.CreatedAt)).ToList(),
            (await fields.Query().Where(field => field.UserId == userId).OrderByDescending(field => field.CreatedAt).Take(40).ToListAsync())
                .Select(field => new ApplicationFieldHistoryItem(field.Id, field.ApplicationJobId, field.FieldKey, field.Label, field.FieldType, field.ValueSource, field.Confidence, field.CreatedAt)).ToList(),
            (await answers.Query().Where(answer => answer.UserId == userId).OrderByDescending(answer => answer.CreatedAt).Take(40).ToListAsync())
                .Select(answer => new ApplicationAnswerHistoryItem(answer.Id, answer.ApplicationJobId, answer.QuestionText, answer.AnswerValue, answer.Source, answer.ProviderModel, answer.Confidence, answer.CreatedAt)).ToList(),
            (await memories.Query().Where(memory => memory.UserId == userId).OrderByDescending(memory => memory.UpdatedAt).Take(40).ToListAsync())
                .Select(memory => new SiteWorkflowMemoryHistoryItem(memory.Id, memory.Domain, memory.PatternKey, memory.ActionJson, memory.SuccessCount, memory.UpdatedAt)).ToList());

    public async Task<AnswerDraftResponse> DraftAnswerAsync(Guid userId, AnswerDraftRequest request)
    {
        var profile = await profiles.FirstOrDefaultAsync(candidate => candidate.UserId == userId);
        return await ai.DraftAnswerAsync(userId, request.QuestionText, request.JobContext, profile);
    }

    public async Task<PairDeviceResponse> PairDeviceAsync(Guid userId, PairDeviceRequest request)
    {
        var device = new AgentDevice
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = string.IsNullOrWhiteSpace(request.Name) ? Environment.MachineName : request.Name,
            Status = "active"
        };
        devices.Add(device);
        await unitOfWork.SaveChangesAsync();
        return new PairDeviceResponse(device.Id, tokens.CreateToken(device.Id, device.Name, "agent", userId));
    }

    private static AgentPolicyResponse ToResponse(AgentPolicy policy) =>
        new(
            policy.Id,
            policy.AutoSubmitMode,
            policy.ExecutionTarget,
            policy.MaxApplicationsPerRun,
            policy.MaxApplicationsPerDay,
            policy.AllowedDomains,
            policy.BlockedDomains,
            policy.IsPaused,
            policy.UpdatedAt);
}
