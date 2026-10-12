namespace JobPilot.Api.Application.Contracts;

public record ResumeScoreComponent(string Key, string Label, int Score, int MaxScore, string Status, string Reason);
