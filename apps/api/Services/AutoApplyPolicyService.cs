using JobPilot.Api.Domain;
using JobPilot.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Services;

public sealed class AutoApplyPolicyService(IRepository<JobTarget> targets)
{
    public async Task<AutoApplyPolicyResult> EvaluateAsync(Guid userId, JobLead lead, CandidateProfile? profile, JobFilter filter)
    {
        // 1. Score threshold
        if (filter.MinScore > 0 && lead.Score < filter.MinScore)
            return AutoApplyPolicyResult.Block($"Score {lead.Score} abaixo do minimo {filter.MinScore}.");

        // 2. Auto-ignore low score
        if (filter.AutoIgnoreLowScore && lead.Score < 30)
            return AutoApplyPolicyResult.Block("Score muito baixo e auto-ignore habilitado.");

        // 3. Workplace type filter (only if explicitly set)
        if (filter.BlockedWorkplaceTypes.Length > 0)
        {
            var workplace = (lead.IsRemote ? "remote" : lead.Location?.ToLowerInvariant() ?? "");
            if (filter.BlockedWorkplaceTypes.Contains("remote") && lead.IsRemote)
                return AutoApplyPolicyResult.Block("Remoto bloqueado pelo filtro.");
        }

        // 4. Check for unknown must-have requirements if job was organized
        var jobTarget = await targets.Query()
            .Include(x => x.Requirements)
            .Where(x => x.UserId == userId && x.JobLeadId == lead.Id)
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync();

        if (jobTarget is not null)
        {
            var hasUnknownMustHave = jobTarget.Requirements
                .Any(r => r.Importance == "must_have" && r.EvidenceText == "unknown");

            if (hasUnknownMustHave)
                return AutoApplyPolicyResult.Block("Requisito must-have com evidencia unknown. Requer revisao antes de aplicar.");
        }

        return AutoApplyPolicyResult.Allow();
    }

}

public record AutoApplyPolicyResult(bool CanApply, string Reason)
{
    public static AutoApplyPolicyResult Allow() => new(true, "");
    public static AutoApplyPolicyResult Block(string reason) => new(false, reason);
}
