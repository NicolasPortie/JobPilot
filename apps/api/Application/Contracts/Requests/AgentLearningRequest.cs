namespace JobPilot.Api.Application.Contracts;

public record AgentLearningRequest(Guid? ApplicationJobId, string Domain, string LearningType, string PatternKey, string Title, string Summary, string TriggerJson, string ActionJson, string EvidenceJson, string RiskLevel);
