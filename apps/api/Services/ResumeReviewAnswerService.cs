using System.Text.Json;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Services;

public sealed class ResumeReviewAnswerService(JobPilotDbContext db, ResumeEvidenceMapService evidenceMapService, ResumeTailoringService tailoringService)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ResumeReviewAnswer> SaveAsync(Guid userId, SaveReviewAnswerRequest request)
    {
        var existing = await db.ResumeReviewAnswers
            .FirstOrDefaultAsync(x => x.UserId == userId && x.JobRequirementId == request.JobRequirementId);

        var now = DateTimeOffset.UtcNow;
        if (existing is not null)
        {
            existing.AnswerValue = request.AnswerValue;
            existing.AnswerType = request.AnswerType;
            existing.EvidenceText = request.EvidenceText ?? "";
            existing.UpdatedAt = now;
        }
        else
        {
            existing = new ResumeReviewAnswer
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ResumeEvidenceMapRunId = request.EvidenceMapRunId,
                JobRequirementId = request.JobRequirementId,
                RequirementLabel = request.RequirementLabel,
                QuestionText = request.QuestionText,
                AnswerValue = request.AnswerValue,
                AnswerType = request.AnswerType,
                EvidenceText = request.EvidenceText ?? "",
                CreatedAt = now,
                UpdatedAt = now
            };
            db.ResumeReviewAnswers.Add(existing);
        }

        await db.SaveChangesAsync();
        return existing;
    }

    public async Task<ResumeReviewAnswer[]> ListAsync(Guid userId, Guid? evidenceMapRunId = null)
    {
        var query = db.ResumeReviewAnswers.Where(x => x.UserId == userId);
        if (evidenceMapRunId is { } id)
            query = query.Where(x => x.ResumeEvidenceMapRunId == id);
        return await query.OrderByDescending(x => x.UpdatedAt).ToArrayAsync();
    }

    public async Task<RegenerateAfterAnswersResponse> RegenerateAsync(Guid userId, RegenerateAfterAnswersRequest request)
    {
        var answers = await db.ResumeReviewAnswers
            .Where(x => x.UserId == userId && request.AnswerIds.Contains(x.Id))
            .ToListAsync();

        if (answers.Count == 0)
            throw new InvalidOperationException("Nenhuma resposta encontrada para regenerar.");

        // Build a new evidence map incorporating answers
        var baseVersion = await db.ResumeVersions
            .FirstOrDefaultAsync(x => x.Id == request.BaseResumeVersionId && x.UserId == userId)
            ?? throw new InvalidOperationException("Versao base nao encontrada.");

        var target = await db.JobTargets
            .Include(x => x.Requirements)
            .FirstOrDefaultAsync(x => x.Id == request.JobTargetId && x.UserId == userId)
            ?? throw new InvalidOperationException("Vaga alvo nao encontrada.");

        // Create new evidence map run with answers applied
        var newMap = await evidenceMapService.BuildAsync(userId, new BuildResumeEvidenceMapRequest(baseVersion.Id, target.Id));

        // Apply user answers to the new map items
        var mapRun = await db.ResumeEvidenceMapRuns
            .Include(x => x.Items)
            .FirstAsync(x => x.Id == newMap.Id);

        foreach (var answer in answers)
        {
            var item = mapRun.Items.FirstOrDefault(x => x.JobRequirementId == answer.JobRequirementId);
            if (item is null) continue;

            item.MatchStatus = answer.AnswerType switch
            {
                "proven" => "proven",
                "adjacent" => "adjacent",
                "missing" => "missing",
                _ => item.MatchStatus
            };
            item.EvidenceText = answer.EvidenceText;
            item.EvidenceSource = "user_answer";
            item.Reason = $"Respondido pelo usuario: {answer.AnswerValue}";
            item.Confidence = answer.AnswerType == "proven" ? 0.95m : 0.7m;
        }

        var score = ResumeEvidenceScoreCalculator.Calculate(mapRun.Items);
        mapRun.EvidenceScore = score.Score;
        mapRun.Confidence = score.Confidence;
        mapRun.Status = score.Status;
        mapRun.WarningsJson = JsonSerializer.Serialize(score.Warnings, JsonOptions);

        await db.SaveChangesAsync();

        // Regenerate tailored resume
        var tailored = await tailoringService.TailorAsync(userId, new TailorResumeRequest(
            baseVersion.Id,
            target.Id,
            mapRun.Id,
            request.Layout ?? "ats_classic",
            request.UseAi));

        return new RegenerateAfterAnswersResponse(
            mapRun.Id,
            tailored.TailoredVersionId,
            tailored.Status,
            tailored.AtsScore,
            mapRun.Items.Select(x => new ResumeRequirementEvidenceDto(
                x.Id, x.JobRequirementId, x.RequirementLabel, x.RequirementCategory,
                x.RequirementImportance, x.MatchStatus, x.EvidenceText, x.EvidenceSource,
                x.Reason, x.Confidence, x.SortOrder)).ToArray());
    }
}

public record SaveReviewAnswerRequest(Guid? EvidenceMapRunId, Guid JobRequirementId, string RequirementLabel, string QuestionText, string AnswerValue, string AnswerType, string? EvidenceText = null);
public record RegenerateAfterAnswersRequest(Guid[] AnswerIds, Guid BaseResumeVersionId, Guid JobTargetId, string Layout = "ats_classic", bool UseAi = true);
public record RegenerateAfterAnswersResponse(Guid NewEvidenceMapRunId, Guid NewTailoredVersionId, string Status, int AtsScore, ResumeRequirementEvidenceDto[] UpdatedEvidences);
