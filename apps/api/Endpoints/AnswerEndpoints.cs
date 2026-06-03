using System.Security.Claims;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Endpoints;

public static class AnswerEndpoints
{
    public static RouteGroupBuilder MapAnswerEndpoints(this RouteGroupBuilder authed)
    {
        authed.MapGet("/answers", async (JobPilotDbContext db, ClaimsPrincipal principal) =>
            await db.ReusableAnswers.Where(x => x.UserId == principal.UserId()).OrderBy(x => x.QuestionKey).ToListAsync());

        authed.MapPost("/answers", async (ReusableAnswer input, JobPilotDbContext db, ClaimsPrincipal principal) =>
        {
            input.Id = Guid.NewGuid();
            input.UserId = principal.UserId();
            db.ReusableAnswers.Add(input);
            await db.SaveChangesAsync();
            return Results.Created($"/api/answers/{input.Id}", input);
        });

        authed.MapPut("/answers/{id:guid}", async (Guid id, ReusableAnswer input, JobPilotDbContext db, ClaimsPrincipal principal) =>
        {
            var answer = await db.ReusableAnswers.FirstOrDefaultAsync(x => x.Id == id && x.UserId == principal.UserId());
            if (answer is null) return Results.NotFound();
            answer.QuestionKey = input.QuestionKey;
            answer.QuestionText = input.QuestionText;
            answer.AnswerValue = input.AnswerValue;
            answer.Category = input.Category;
            answer.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(answer);
        });

        authed.MapDelete("/answers/{id:guid}", async (Guid id, JobPilotDbContext db, ClaimsPrincipal principal) =>
        {
            var answer = await db.ReusableAnswers.FirstOrDefaultAsync(x => x.Id == id && x.UserId == principal.UserId());
            if (answer is null) return Results.NotFound();
            db.ReusableAnswers.Remove(answer);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        authed.MapGet("/question-patterns", async (JobPilotDbContext db, ClaimsPrincipal principal) =>
            await db.QuestionPatterns.Where(x => x.UserId == principal.UserId()).OrderByDescending(x => x.UseCount).ToListAsync());

        authed.MapPost("/question-patterns", async (QuestionPatternRequest request, JobPilotDbContext db, ClaimsPrincipal principal) =>
        {
            var pattern = new QuestionPattern
            {
                Id = Guid.NewGuid(),
                UserId = principal.UserId(),
                PatternKey = request.PatternKey,
                Category = request.Category,
                Tags = request.Tags,
                QuestionTemplates = request.QuestionTemplates,
                AnswerTemplate = request.AnswerTemplate,
                ValueSource = request.ValueSource,
                ProfileFieldMapping = request.ProfileFieldMapping,
                ConfidenceThreshold = request.ConfidenceThreshold
            };
            db.QuestionPatterns.Add(pattern);
            await db.SaveChangesAsync();
            return Results.Created($"/api/question-patterns/{pattern.Id}", pattern);
        });

        authed.MapPut("/question-patterns/{id:guid}", async (Guid id, QuestionPatternRequest request, JobPilotDbContext db, ClaimsPrincipal principal) =>
        {
            var pattern = await db.QuestionPatterns.FirstOrDefaultAsync(x => x.Id == id && x.UserId == principal.UserId());
            if (pattern is null) return Results.NotFound();
            pattern.Apply(new QuestionPattern
            {
                PatternKey = request.PatternKey,
                Category = request.Category,
                Tags = request.Tags,
                QuestionTemplates = request.QuestionTemplates,
                AnswerTemplate = request.AnswerTemplate,
                ValueSource = request.ValueSource,
                ProfileFieldMapping = request.ProfileFieldMapping,
                ConfidenceThreshold = request.ConfidenceThreshold
            });
            await db.SaveChangesAsync();
            return Results.Ok(pattern);
        });

        authed.MapDelete("/question-patterns/{id:guid}", async (Guid id, JobPilotDbContext db, ClaimsPrincipal principal) =>
        {
            var pattern = await db.QuestionPatterns.FirstOrDefaultAsync(x => x.Id == id && x.UserId == principal.UserId());
            if (pattern is null) return Results.NotFound();
            db.QuestionPatterns.Remove(pattern);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        authed.MapPost("/question-patterns/resolve", async (AnswerDraftRequest request, AnswerResolver resolver, JobPilotDbContext db, ClaimsPrincipal principal) =>
        {
            var profile = await db.CandidateProfiles.FirstOrDefaultAsync(x => x.UserId == principal.UserId());
            var result = await resolver.ResolveAsync(principal.UserId(), request.QuestionText, request.JobContext ?? "", profile);
            return Results.Ok(result);
        });

        return authed;
    }
}
