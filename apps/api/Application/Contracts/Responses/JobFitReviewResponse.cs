namespace JobPilot.Api.Application.Contracts;

public record JobFitReviewResponse(string Decision, int FitScore, string Reason, string DetectedSeniority, string DetectedWorkplace, string ApplyType, string[] Strengths, string[] Risks, decimal Confidence);
