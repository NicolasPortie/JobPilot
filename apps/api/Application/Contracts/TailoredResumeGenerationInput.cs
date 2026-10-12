namespace JobPilot.Api.Application.Contracts;

public record TailoredResumeGenerationInput(
    string Layout,
    string CandidateJson,
    string JobTargetJson,
    string BaseResumeText,
    string DeterministicDraft,
    ResumeRequirementEvidenceDto[] EvidenceItems,
    string[] IncludedKeywords,
    string[] AdjacentKeywords,
    string[] ExcludedKeywords,
    string[] Checkpoints);
