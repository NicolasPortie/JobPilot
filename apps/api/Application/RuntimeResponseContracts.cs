using JobPilot.Api.Domain;

namespace JobPilot.Api.Application;

public sealed record AgentRunResponse(Guid Id, Guid? ApplicationJobId, string AgentName, string Mode, string State, string PolicyMode, DateTimeOffset StartedAt, DateTimeOffset? CompletedAt)
{
    public static AgentRunResponse From(AgentRun item) =>
        new(item.Id, item.ApplicationJobId, item.AgentName, item.Mode, item.State, item.PolicyMode, item.StartedAt, item.CompletedAt);
}

public sealed record ApplicationJobResponse(Guid Id, Guid ApplicationRunId, Guid JobLeadId, Guid? ResumeVersionId, Guid? ResumeExportId, string ResumeFilePath, string JobUrl, string State, string? Reason, DateTimeOffset CreatedAt)
{
    public static ApplicationJobResponse From(ApplicationJob item) =>
        new(item.Id, item.ApplicationRunId, item.JobLeadId, item.ResumeVersionId, item.ResumeExportId, item.ResumeFilePath, item.JobUrl, item.State, item.Reason, item.CreatedAt);
}

public sealed record ApplicationRunResponse(Guid Id, string Name, string State, int TotalJobs, DateTimeOffset CreatedAt, IReadOnlyList<ApplicationJobResponse> Jobs)
{
    public static ApplicationRunResponse From(ApplicationRun item) =>
        new(item.Id, item.Name, item.State, item.TotalJobs, item.CreatedAt, item.Jobs.Select(ApplicationJobResponse.From).ToList());
}

public sealed record AgentLearningResponse(Guid Id, Guid? ApplicationJobId, string Domain, string LearningType, string PatternKey, string Title, string Summary, string TriggerJson, string ActionJson, string EvidenceJson, string Status, string RiskLevel, int SuccessCount, int FailureCount, DateTimeOffset CreatedAt, DateTimeOffset? ReviewedAt)
{
    public static AgentLearningResponse From(AgentLearning item) =>
        new(item.Id, item.ApplicationJobId, item.Domain, item.LearningType, item.PatternKey, item.Title, item.Summary, item.TriggerJson, item.ActionJson, item.EvidenceJson, item.Status, item.RiskLevel, item.SuccessCount, item.FailureCount, item.CreatedAt, item.ReviewedAt);
}
