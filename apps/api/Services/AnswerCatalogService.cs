using JobPilot.Api.Application;
using JobPilot.Api.Domain;
using JobPilot.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Services;

public sealed class AnswerCatalogService(
    IRepository<ReusableAnswer> answers,
    IRepository<QuestionPattern> patterns,
    IRepository<CandidateProfile> profiles,
    IUnitOfWork unitOfWork,
    AnswerResolver resolver)
{
    public async Task<List<ReusableAnswerResponse>> ListAnswersAsync(Guid userId) =>
        (await answers.Query()
            .Where(answer => answer.UserId == userId)
            .OrderBy(answer => answer.QuestionKey)
            .ToListAsync())
        .Select(ReusableAnswerResponse.From)
        .ToList();

    public async Task<ServiceResult<ReusableAnswerResponse>> CreateAnswerAsync(Guid userId, ReusableAnswerRequest request)
    {
        var answer = new ReusableAnswer
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            QuestionKey = request.QuestionKey,
            QuestionText = request.QuestionText,
            AnswerValue = request.AnswerValue,
            Category = request.Category
        };
        answers.Add(answer);
        await unitOfWork.SaveChangesAsync();
        return ServiceResult<ReusableAnswerResponse>.Created(ReusableAnswerResponse.From(answer));
    }

    public async Task<ServiceResult<ReusableAnswerResponse>> UpdateAnswerAsync(Guid userId, Guid id, ReusableAnswerRequest request)
    {
        var answer = await answers.FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.UserId == userId);
        if (answer is null) return ServiceResult<ReusableAnswerResponse>.NotFound();

        answer.QuestionKey = request.QuestionKey;
        answer.QuestionText = request.QuestionText;
        answer.AnswerValue = request.AnswerValue;
        answer.Category = request.Category;
        answer.UpdatedAt = DateTimeOffset.UtcNow;
        await unitOfWork.SaveChangesAsync();
        return ServiceResult<ReusableAnswerResponse>.Ok(ReusableAnswerResponse.From(answer));
    }

    public async Task<ServiceResult<ReusableAnswerResponse>> DeleteAnswerAsync(Guid userId, Guid id)
    {
        var answer = await answers.FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.UserId == userId);
        if (answer is null) return ServiceResult<ReusableAnswerResponse>.NotFound();
        answers.Remove(answer);
        await unitOfWork.SaveChangesAsync();
        return ServiceResult<ReusableAnswerResponse>.NoContent();
    }

    public async Task<List<QuestionPatternResponse>> ListPatternsAsync(Guid userId) =>
        (await patterns.Query()
            .Where(pattern => pattern.UserId == userId)
            .OrderByDescending(pattern => pattern.UseCount)
            .ToListAsync())
        .Select(QuestionPatternResponse.From)
        .ToList();

    public async Task<ServiceResult<QuestionPatternResponse>> CreatePatternAsync(Guid userId, QuestionPatternRequest request)
    {
        var pattern = BuildPattern(userId, request);
        patterns.Add(pattern);
        await unitOfWork.SaveChangesAsync();
        return ServiceResult<QuestionPatternResponse>.Created(QuestionPatternResponse.From(pattern));
    }

    public async Task<ServiceResult<QuestionPatternResponse>> UpdatePatternAsync(Guid userId, Guid id, QuestionPatternRequest request)
    {
        var pattern = await patterns.FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.UserId == userId);
        if (pattern is null) return ServiceResult<QuestionPatternResponse>.NotFound();
        pattern.Apply(BuildPattern(userId, request));
        await unitOfWork.SaveChangesAsync();
        return ServiceResult<QuestionPatternResponse>.Ok(QuestionPatternResponse.From(pattern));
    }

    public async Task<ServiceResult<QuestionPatternResponse>> DeletePatternAsync(Guid userId, Guid id)
    {
        var pattern = await patterns.FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.UserId == userId);
        if (pattern is null) return ServiceResult<QuestionPatternResponse>.NotFound();
        patterns.Remove(pattern);
        await unitOfWork.SaveChangesAsync();
        return ServiceResult<QuestionPatternResponse>.NoContent();
    }

    public async Task<ResolvedAnswer> ResolvePatternAsync(Guid userId, AnswerDraftRequest request)
    {
        var profile = await profiles.FirstOrDefaultAsync(candidate => candidate.UserId == userId);
        return await resolver.ResolveAsync(userId, request.QuestionText, request.JobContext ?? "", profile);
    }

    private static QuestionPattern BuildPattern(Guid userId, QuestionPatternRequest request) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PatternKey = request.PatternKey,
            Category = request.Category,
            Tags = request.Tags,
            QuestionTemplates = request.QuestionTemplates,
            AnswerTemplate = request.AnswerTemplate,
            ValueSource = request.ValueSource,
            ProfileFieldMapping = request.ProfileFieldMapping,
            ConfidenceThreshold = request.ConfidenceThreshold
        };
}
