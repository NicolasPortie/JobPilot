using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Tests;

public class AnswerResolverTests
{
    private static JobPilotDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<JobPilotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new JobPilotDbContext(options);
    }

    [Fact]
    public async Task ResolveAsync_MatchesSalaryQuestion_WithProfileValue()
    {
        var db = CreateDb();
        var userId = Guid.NewGuid();
        db.QuestionPatterns.Add(new QuestionPattern
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PatternKey = "salary_expectation",
            Category = "screening",
            Tags = ["salario", "pretensao"],
            QuestionTemplates = ["Pretensao salarial", "Qual sua pretensao salarial?"],
            AnswerTemplate = "Minha pretensao e de R$ {{profile.SalaryExpectation}}",
            ValueSource = "profile_field",
            ProfileFieldMapping = "SalaryExpectation",
            ConfidenceThreshold = 0.90m
        });
        await db.SaveChangesAsync();

        var resolver = new AnswerResolver(db);
        var profile = new CandidateProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FullName = "Nicolas",
            SalaryExpectation = "3800.00"
        };

        var result = await resolver.ResolveAsync(userId, "Qual sua pretensao salarial?", "", profile);

        Assert.True(result.Confidence >= 0.75m);
        Assert.Contains("3800.00", result.AnswerValue);
        Assert.Equal("profile_template", result.Source);
    }

    [Fact]
    public async Task ResolveAsync_MatchesPhoneQuestion_WithProfileValue()
    {
        var db = CreateDb();
        var userId = Guid.NewGuid();
        db.QuestionPatterns.Add(new QuestionPattern
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PatternKey = "phone_contact",
            Category = "screening",
            Tags = ["telefone", "celular"],
            QuestionTemplates = ["Numero de telefone", "Celular para contato"],
            AnswerTemplate = "{{profile.Phone}}",
            ValueSource = "profile_field",
            ProfileFieldMapping = "Phone",
            ConfidenceThreshold = 0.95m
        });
        await db.SaveChangesAsync();

        var resolver = new AnswerResolver(db);
        var profile = new CandidateProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FullName = "Nicolas",
            Phone = "(11) 99999-9999"
        };

        var result = await resolver.ResolveAsync(userId, "Celular para contato", "", profile);

        Assert.Equal("(11) 99999-9999", result.AnswerValue);
        Assert.Equal("profile_template", result.Source);
    }

    [Fact]
    public async Task ResolveAsync_NoMatch_ReturnsCheckpoint()
    {
        var db = CreateDb();
        var userId = Guid.NewGuid();
        db.QuestionPatterns.Add(new QuestionPattern
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PatternKey = "salary",
            QuestionTemplates = ["Pretensao salarial"],
            AnswerTemplate = "",
            ValueSource = "checkpoint"
        });
        await db.SaveChangesAsync();

        var resolver = new AnswerResolver(db);
        var result = await resolver.ResolveAsync(userId, "Pergunta totalmente diferente e sem sentido", "", null);

        Assert.Equal("checkpoint", result.Source);
    }

    [Fact]
    public async Task ResolveAsync_FuzzyMatch_WorksWithTypos()
    {
        var db = CreateDb();
        var userId = Guid.NewGuid();
        db.QuestionPatterns.Add(new QuestionPattern
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PatternKey = "english_level",
            Category = "screening",
            Tags = ["ingles", "english"],
            QuestionTemplates = ["Nivel de ingles", "English level"],
            AnswerTemplate = "Tenho ingles tecnico para leitura e escrita.",
            ValueSource = "reusable"
        });
        await db.SaveChangesAsync();

        var resolver = new AnswerResolver(db);
        var result = await resolver.ResolveAsync(userId, "Como esta seu ingles?", "", null);

        Assert.True(result.Confidence > 0.5m);
        Assert.Contains("ingles", result.AnswerValue.ToLowerInvariant());
    }

    [Fact]
    public async Task ResolveAsync_CheckpointPattern_ReturnsCheckpoint()
    {
        var db = CreateDb();
        var userId = Guid.NewGuid();
        db.QuestionPatterns.Add(new QuestionPattern
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PatternKey = "disability_pcd",
            QuestionTemplates = ["Voce e PCD?"],
            AnswerTemplate = "",
            ValueSource = "checkpoint"
        });
        await db.SaveChangesAsync();

        var resolver = new AnswerResolver(db);
        var result = await resolver.ResolveAsync(userId, "Voce e PCD?", "", null);

        Assert.Equal("checkpoint", result.Source);
    }
}
