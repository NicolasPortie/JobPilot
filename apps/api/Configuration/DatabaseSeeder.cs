using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Configuration;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(JobPilotDbContext db, IConfiguration configuration)
    {
        var email = configuration["Auth:SeedEmail"]
            ?? throw new InvalidOperationException("Auth:SeedEmail is required when database seed is enabled.");
        var password = configuration["Auth:SeedPassword"]
            ?? throw new InvalidOperationException("Auth:SeedPassword is required when database seed is enabled.");

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Auth seed credentials cannot be empty.");

        if (!await db.Users.AnyAsync(x => x.Email == email))
        {
            var user = new AppUser
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password)
            };
            db.Users.Add(user);
            db.CandidateProfiles.Add(new CandidateProfile
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                FullName = configuration["Bootstrap:FullName"] ?? "",
                Cpf = "",
                Email = email,
                Country = configuration["Bootstrap:Country"] ?? "",
                City = configuration["Bootstrap:City"] ?? "",
                TargetRole = configuration["Bootstrap:TargetRole"] ?? "",
                Seniority = configuration["Bootstrap:Seniority"] ?? "unknown",
                Skills = configuration.GetSection("Bootstrap:Skills").Get<string[]>() ?? [],
                ResumePath = ""
            });
            var kimiApiKey = configuration["Kimi:ApiKey"]
                ?? Environment.GetEnvironmentVariable("KIMI_API_KEY")
                ?? Environment.GetEnvironmentVariable("KIMI_API_KEY", EnvironmentVariableTarget.User)
                ?? "";
            db.AiProviders.Add(CreateLocalProvider(user.Id, configuration, isDefault: true));
            db.AiProviders.Add(new AiProvider
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Name = "Kimi Code",
                ProviderType = "kimi",
                BaseUrl = configuration["Kimi:BaseUrl"] ?? "https://api.kimi.com/coding",
                Model = configuration["Kimi:Model"] ?? "kimi-for-coding",
                ApiFormat = configuration["Kimi:Api"] ?? "anthropic-messages",
                EncryptedApiKey = SecretProtector.Protect(kimiApiKey, configuration),
                IsActive = true,
                IsDefault = false
            });
            SeedProfileValidationRules(db, user.Id);
            await db.SaveChangesAsync();
        }

        foreach (var user in await db.Users.ToListAsync())
        {
            if (!await db.AiProviders.AnyAsync(x => x.UserId == user.Id && x.ProviderType == "ollama"))
            {
                await db.AiProviders.Where(x => x.UserId == user.Id).ExecuteUpdateAsync(x => x.SetProperty(p => p.IsDefault, false));
                db.AiProviders.Add(CreateLocalProvider(user.Id, configuration, isDefault: true));
            }

            if (!await db.AiProviders.AnyAsync(x => x.UserId == user.Id))
            {
                var kimiApiKey = configuration["Kimi:ApiKey"]
                    ?? Environment.GetEnvironmentVariable("KIMI_API_KEY")
                    ?? Environment.GetEnvironmentVariable("KIMI_API_KEY", EnvironmentVariableTarget.User)
                    ?? "";
                db.AiProviders.Add(new AiProvider
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    Name = "Kimi Code",
                    ProviderType = "kimi",
                    BaseUrl = configuration["Kimi:BaseUrl"] ?? "https://api.kimi.com/coding",
                    Model = configuration["Kimi:Model"] ?? "kimi-for-coding",
                    ApiFormat = configuration["Kimi:Api"] ?? "anthropic-messages",
                    EncryptedApiKey = SecretProtector.Protect(kimiApiKey, configuration),
                    IsActive = true,
                    IsDefault = true
                });
            }


            SeedProfileValidationRules(db, user.Id);
        }

        await db.SaveChangesAsync();
    }

    private static AiProvider CreateLocalProvider(Guid userId, IConfiguration configuration, bool isDefault) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "Ollama Local",
            ProviderType = "ollama",
            BaseUrl = configuration["LocalLlm:BaseUrl"] ?? "http://localhost:11434",
            Model = configuration["LocalLlm:Model"] ?? "qwen3:4b-instruct",
            ApiFormat = "ollama-chat",
            EncryptedApiKey = "",
            IsActive = configuration.GetValue("LocalLlm:Enabled", true),
            IsDefault = isDefault
        };

    private static void SeedProfileValidationRules(JobPilotDbContext db, Guid userId)
    {
        var rules = new[]
        {
            new ProfileValidationRule { Id = Guid.NewGuid(), UserId = userId, FieldName = "Phone", ValidationType = "regex", ValidationValue = @"^(\(?\d{2}\)?\s?\d{4,5}-?\d{4})$", ErrorMessage = "Telefone invalido. Use formato (11) 99999-9999.", IsCritical = true },
            new ProfileValidationRule { Id = Guid.NewGuid(), UserId = userId, FieldName = "Email", ValidationType = "regex", ValidationValue = @"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Email invalido.", IsCritical = true },
            new ProfileValidationRule { Id = Guid.NewGuid(), UserId = userId, FieldName = "SalaryExpectation", ValidationType = "not_empty", ValidationValue = "", ErrorMessage = "Pretensao salarial ainda nao informada.", IsCritical = false },
            new ProfileValidationRule { Id = Guid.NewGuid(), UserId = userId, FieldName = "Skills", ValidationType = "length", ValidationValue = "3", ErrorMessage = "Adicione pelo menos 3 skills ao seu perfil.", IsCritical = false },
            new ProfileValidationRule { Id = Guid.NewGuid(), UserId = userId, FieldName = "FullName", ValidationType = "regex", ValidationValue = @"^\S+\s+\S+$", ErrorMessage = "Informe nome completo (nome e sobrenome).", IsCritical = true },
            new ProfileValidationRule { Id = Guid.NewGuid(), UserId = userId, FieldName = "City", ValidationType = "not_empty", ValidationValue = "", ErrorMessage = "Cidade nao informada.", IsCritical = false },
            new ProfileValidationRule { Id = Guid.NewGuid(), UserId = userId, FieldName = "ProfessionalSummary", ValidationType = "not_empty", ValidationValue = "", ErrorMessage = "Resumo profissional vazio. Ajuda a explicar seu valor para a vaga.", IsCritical = false }
        };

        foreach (var r in rules)
        {
            if (!db.ProfileValidationRules.Any(x => x.UserId == userId && x.FieldName == r.FieldName))
            {
                db.ProfileValidationRules.Add(r);
            }
        }
    }
}

