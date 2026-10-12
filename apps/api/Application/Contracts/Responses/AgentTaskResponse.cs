using JobPilot.Api.Domain;

namespace JobPilot.Api.Application.Contracts;

public sealed record AgentTaskResponse(
    ApplicationJobResponse Task,
    JobLeadResponse Lead,
    AgentCandidateProfileResponse? Profile,
    IReadOnlyList<ReusableAnswerResponse> Answers)
{
    public static AgentTaskResponse From(ApplicationJob task, JobLead lead, CandidateProfile? profile, IEnumerable<ReusableAnswer> answers) =>
        new(
            ApplicationJobResponse.From(task),
            JobLeadResponse.From(lead),
            profile is null ? null : AgentCandidateProfileResponse.From(profile),
            answers.Select(ReusableAnswerResponse.From).ToList());
}
