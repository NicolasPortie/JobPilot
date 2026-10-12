using System.Globalization;
using System.Text.RegularExpressions;
using JobPilot.Api.Domain;
using JobPilot.Api.Infrastructure.Persistence;

namespace JobPilot.Api.Services;

public sealed class ReusableAnswerService(IRepository<ReusableAnswer> answers)
{
    public async Task UpsertScreeningAnswerAsync(Guid userId, string questionText, string answerValue)
    {
        var key = StableQuestionKey(questionText);
        var existing = await answers.FirstOrDefaultAsync(x =>
            x.UserId == userId &&
            (x.QuestionKey == key || x.QuestionText == questionText));

        if (existing is null)
        {
            answers.Add(new ReusableAnswer
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                QuestionKey = key,
                QuestionText = questionText,
                AnswerValue = answerValue.Trim(),
                Category = "screening"
            });
            return;
        }

        existing.AnswerValue = answerValue.Trim();
        existing.UpdatedAt = DateTimeOffset.UtcNow;
    }

    public static string StableQuestionKey(string text)
    {
        var normalized = text.Normalize(System.Text.NormalizationForm.FormD);
        var chars = normalized
            .Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            .Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-')
            .ToArray();

        var compact = Regex.Replace(new string(chars), "-+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(compact) ? "answer" : compact[..Math.Min(compact.Length, 80)];
    }
}
