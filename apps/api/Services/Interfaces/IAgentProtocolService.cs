using JobPilot.Api.Application;
using JobPilot.Api.Domain;

namespace JobPilot.Api.Services;

public interface IAgentProtocolService
{
    Task<AnswerDraftResponse> DraftAnswerAsync(Guid userId, AnswerDraftRequest request);
    Task<BrowserActionPlanResponse> PlanBrowserActionAsync(Guid userId, BrowserActionPlanRequest request);
    Task<AgentRun> StartRunAsync(Guid userId, AgentRunRequest request);
    Task<ServiceResult<AgentRun>> CompleteRunAsync(Guid userId, Guid id, AgentRunCompleteRequest request);
    Task SaveStepAsync(Guid userId, AgentStepRequest request);
    Task<Guid> SaveTraceStepAsync(Guid userId, AgentTraceStepRequest request);
    Task SaveApplicationFieldAsync(Guid userId, ApplicationFieldRequest request);
    Task SaveApplicationAnswerAsync(Guid userId, ApplicationAnswerRequest request);
    Task SaveWorkflowMemoryAsync(Guid userId, SiteWorkflowMemoryRequest request);
    Task SaveEventAsync(Guid userId, ExecutionEventRequest input);
    Task<Checkpoint> CreateCheckpointAsync(Guid userId, AgentCheckpointRequest request);
}
