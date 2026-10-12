using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using JobPilot.Api.Domain;
using JobPilot.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Services;

public sealed class AgentAnswerResolutionService(
    IRepository<CandidateProfile> profiles,
    IRepository<ApplicationAnswer> answers,
    IRepository<Checkpoint> checkpoints,
    IUnitOfWork unitOfWork,
    AnswerResolver resolver,
    IAiChatService ai)
{
    private static readonly string[] SensitiveSignals =
    [
        "cpf", "rg", "documento", "visa", "visto", "autorizacao legal", "autorizado legalmente",
        "deficiencia", "pcd", "raca", "genero", "religiao", "certificacao", "diploma", "formacao"
    ];

    public async Task<ResolveAgentAnswerResponse> ResolveAsync(Guid userId, ResolveAgentAnswerRequest request)
    {
        if (RequiresManualSource(request))
        {
            await CreateCheckpointAsync(userId, request, "sensitive_answer_missing_source", "Pergunta sensivel sem fonte explicita no perfil.");
            return new("", "manual-required", 0.95m, "Pergunta sensivel exige confirmacao manual.", true);
        }

        var profile = await profiles.FirstOrDefaultAsync(x => x.UserId == userId);
        var resolved = await resolver.ResolveAsync(userId, request.QuestionText, request.JobContext, profile);
        if (!string.IsNullOrWhiteSpace(resolved.AnswerValue) && resolved.Confidence >= 0.65m)
        {
            var source = NormalizeSource(resolved.Source);
            await SaveAnswerAsync(userId, request, resolved.AnswerValue, source, resolved.Confidence);
            return new(resolved.AnswerValue, source, resolved.Confidence, resolved.Reason, false);
        }

        if (RequiresNumericAnswer(request))
        {
            await CreateCheckpointAsync(userId, request, "manual-required", "Pergunta numerica sem fonte explicita no perfil ou em respostas salvas.");
            return new("", "manual-required", resolved.Confidence, "Pergunta numerica exige fonte explicita antes de responder.", true);
        }

        var draft = await ai.DraftAnswerAsync(userId, request.QuestionText, request.JobContext, profile);
        if (!string.IsNullOrWhiteSpace(draft.Text))
        {
            await SaveAnswerAsync(userId, request, draft.Text, "ai-draft", draft.UsedKimi ? 0.6m : 0.45m);
            await CreateCheckpointAsync(userId, request, "answer_needs_confirmation", "Resposta provisoria gerada para validacao posterior.");
            return new(draft.Text, "ai-draft", draft.UsedKimi ? 0.6m : 0.45m, "Resposta provisoria gerada e registrada como pendencia.", true);
        }

        await CreateCheckpointAsync(userId, request, "manual-required", resolved.Reason);
        return new("", "manual-required", resolved.Confidence, resolved.Reason, true);
    }

    private async Task SaveAnswerAsync(Guid userId, ResolveAgentAnswerRequest request, string answer, string source, decimal confidence)
    {
        answers.Add(new ApplicationAnswer
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ApplicationJobId = request.ApplicationJobId,
            QuestionText = request.QuestionText,
            AnswerValue = answer,
            Source = source,
            ProviderModel = source,
            Confidence = confidence
        });
        await unitOfWork.SaveChangesAsync();
    }

    private async Task CreateCheckpointAsync(Guid userId, ResolveAgentAnswerRequest request, string reasonCode, string detail)
    {
        var exists = await checkpoints.AnyAsync(x =>
            x.UserId == userId &&
            x.ApplicationJobId == request.ApplicationJobId &&
            x.ReasonCode == reasonCode &&
            x.FieldLabel == request.QuestionText &&
            x.ResolutionStatus == "open");
        if (exists) return;

        checkpoints.Add(new Checkpoint
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ApplicationJobId = request.ApplicationJobId,
            ReasonCode = reasonCode,
            Stage = "answer_resolution",
            FieldLabel = request.QuestionText,
            Detail = detail,
            ResolutionStatus = "open"
        });
        await unitOfWork.SaveChangesAsync();
    }

    private static bool RequiresManualSource(ResolveAgentAnswerRequest request)
    {
        var text = Normalize($"{request.QuestionText} {request.FieldContext}");
        return SensitiveSignals.Any(text.Contains);
    }

    private static bool RequiresNumericAnswer(ResolveAgentAnswerRequest request)
    {
        var text = Normalize($"{request.QuestionText} {request.FieldContext} {request.FieldType}");
        if (text.Contains("number") || text.Contains("numeric") || text.Contains("numero")) return true;
        return (text.Contains("anos") || text.Contains("years") || text.Contains("tempo")) &&
               (text.Contains("experiencia") || text.Contains("experience") || text.Contains("trabalha"));
    }

    private static string NormalizeSource(string source) => source switch
    {
        "profile_template" or "profile_field" => "candidate-profile",
        "reusable_template" or "reusable" => "reusable-answer",
        "ai_draft" => "ai-draft",
        "checkpoint" => "manual-required",
        _ => source
    };

    private static string Normalize(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                builder.Append(ch);
        }
        return Regex.Replace(builder.ToString().ToLowerInvariant(), @"[^a-z0-9]+", " ").Trim();
    }
}
