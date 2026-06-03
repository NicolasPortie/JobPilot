using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Data;

public class JobPilotDbContext(DbContextOptions<JobPilotDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<CandidateProfile> CandidateProfiles => Set<CandidateProfile>();
    public DbSet<ResumeImport> ResumeImports => Set<ResumeImport>();
    public DbSet<ResumeDocument> ResumeDocuments => Set<ResumeDocument>();
    public DbSet<ResumeVersion> ResumeVersions => Set<ResumeVersion>();
    public DbSet<ResumeSection> ResumeSections => Set<ResumeSection>();
    public DbSet<ResumeExport> ResumeExports => Set<ResumeExport>();
    public DbSet<ResumeTailoringRun> ResumeTailoringRuns => Set<ResumeTailoringRun>();
    public DbSet<ResumeReviewAnswer> ResumeReviewAnswers => Set<ResumeReviewAnswer>();
    public DbSet<JobTarget> JobTargets => Set<JobTarget>();
    public DbSet<JobRequirement> JobRequirements => Set<JobRequirement>();
    public DbSet<ResumeEvidenceMapRun> ResumeEvidenceMapRuns => Set<ResumeEvidenceMapRun>();
    public DbSet<ResumeRequirementEvidence> ResumeRequirementEvidences => Set<ResumeRequirementEvidence>();
    public DbSet<SavedSearch> SavedSearches => Set<SavedSearch>();
    public DbSet<JobLead> JobLeads => Set<JobLead>();
    public DbSet<ReusableAnswer> ReusableAnswers => Set<ReusableAnswer>();
    public DbSet<AiProvider> AiProviders => Set<AiProvider>();
    public DbSet<AiUsageLog> AiUsageLogs => Set<AiUsageLog>();
    public DbSet<AgentRun> AgentRuns => Set<AgentRun>();
    public DbSet<AgentStep> AgentSteps => Set<AgentStep>();
    public DbSet<AgentTraceStep> AgentTraceSteps => Set<AgentTraceStep>();
    public DbSet<ApplicationField> ApplicationFields => Set<ApplicationField>();
    public DbSet<ApplicationAnswer> ApplicationAnswers => Set<ApplicationAnswer>();
    public DbSet<SiteWorkflowMemory> SiteWorkflowMemories => Set<SiteWorkflowMemory>();
    public DbSet<AgentLearning> AgentLearnings => Set<AgentLearning>();
    public DbSet<AgentPolicy> AgentPolicies => Set<AgentPolicy>();
    public DbSet<AgentDevice> AgentDevices => Set<AgentDevice>();
    public DbSet<BrowserExtensionSession> BrowserExtensionSessions => Set<BrowserExtensionSession>();
    public DbSet<BrowserExtensionCommandJob> BrowserExtensionCommandJobs => Set<BrowserExtensionCommandJob>();
    public DbSet<ApplicationRun> ApplicationRuns => Set<ApplicationRun>();
    public DbSet<ApplicationJob> ApplicationJobs => Set<ApplicationJob>();
    public DbSet<ExecutionEvent> ExecutionEvents => Set<ExecutionEvent>();
    public DbSet<Checkpoint> Checkpoints => Set<Checkpoint>();
    public DbSet<QuestionPattern> QuestionPatterns => Set<QuestionPattern>();
    public DbSet<JobFilter> JobFilters => Set<JobFilter>();
    public DbSet<ProfileValidationRule> ProfileValidationRules => Set<ProfileValidationRule>();
    public DbSet<PlatformCredential> PlatformCredentials => Set<PlatformCredential>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>().HasIndex(x => x.Email).IsUnique();
        modelBuilder.Entity<CandidateProfile>().HasIndex(x => x.UserId).IsUnique();
        modelBuilder.Entity<ResumeImport>().HasIndex(x => new { x.UserId, x.CreatedAt });
        modelBuilder.Entity<ResumeDocument>().HasIndex(x => new { x.UserId, x.UpdatedAt });
        modelBuilder.Entity<ResumeVersion>().HasIndex(x => new { x.UserId, x.ResumeDocumentId, x.UpdatedAt });
        modelBuilder.Entity<ResumeSection>().HasIndex(x => new { x.ResumeVersionId, x.SortOrder });
        modelBuilder.Entity<ResumeExport>().HasIndex(x => new { x.UserId, x.ResumeVersionId, x.CreatedAt });
        modelBuilder.Entity<ResumeTailoringRun>().HasIndex(x => new { x.UserId, x.ResumeVersionId, x.CreatedAt });
        modelBuilder.Entity<JobTarget>().HasIndex(x => new { x.UserId, x.UpdatedAt });
        modelBuilder.Entity<JobTarget>().HasIndex(x => new { x.UserId, x.JobLeadId });
        modelBuilder.Entity<JobRequirement>().HasIndex(x => new { x.JobTargetId, x.SortOrder });
        modelBuilder.Entity<ResumeEvidenceMapRun>().HasIndex(x => new { x.UserId, x.ResumeVersionId, x.JobTargetId, x.CreatedAt });
        modelBuilder.Entity<ResumeRequirementEvidence>().HasIndex(x => new { x.ResumeEvidenceMapRunId, x.SortOrder });
        modelBuilder.Entity<ResumeDocument>()
            .HasMany(x => x.Versions)
            .WithOne(x => x.Document)
            .HasForeignKey(x => x.ResumeDocumentId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ResumeVersion>()
            .HasMany(x => x.Sections)
            .WithOne(x => x.Version)
            .HasForeignKey(x => x.ResumeVersionId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<JobTarget>()
            .HasMany(x => x.Requirements)
            .WithOne(x => x.JobTarget)
            .HasForeignKey(x => x.JobTargetId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ResumeEvidenceMapRun>()
            .HasMany(x => x.Items)
            .WithOne(x => x.Run)
            .HasForeignKey(x => x.ResumeEvidenceMapRunId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<JobLead>().HasIndex(x => new { x.UserId, x.Url }).IsUnique();
        modelBuilder.Entity<ReusableAnswer>().HasIndex(x => new { x.UserId, x.QuestionKey }).IsUnique();
        modelBuilder.Entity<AiProvider>().HasIndex(x => new { x.UserId, x.Name }).IsUnique();
        modelBuilder.Entity<AiUsageLog>().HasIndex(x => new { x.UserId, x.CreatedAt });
        modelBuilder.Entity<AiUsageLog>().HasIndex(x => new { x.UserId, x.Operation, x.CreatedAt });
        modelBuilder.Entity<AgentPolicy>().HasIndex(x => x.UserId).IsUnique();
        modelBuilder.Entity<BrowserExtensionSession>().HasIndex(x => new { x.UserId, x.CreatedAt });
        modelBuilder.Entity<BrowserExtensionSession>().HasIndex(x => new { x.UserId, x.InstallationId });
        modelBuilder.Entity<BrowserExtensionSession>().HasIndex(x => x.TokenHash).IsUnique();
        modelBuilder.Entity<BrowserExtensionCommandJob>().HasIndex(x => new { x.BrowserExtensionSessionId, x.Status, x.CreatedAt });
        modelBuilder.Entity<SiteWorkflowMemory>().HasIndex(x => new { x.UserId, x.Domain, x.PatternKey }).IsUnique();
        modelBuilder.Entity<AgentLearning>().HasIndex(x => new { x.UserId, x.Domain, x.PatternKey }).IsUnique();
        modelBuilder.Entity<AgentTraceStep>().HasIndex(x => new { x.UserId, x.ApplicationJobId, x.StepNumber });
        modelBuilder.Entity<AgentTraceStep>().HasIndex(x => new { x.UserId, x.AgentRunId, x.StepNumber });
        modelBuilder.Entity<Checkpoint>().HasIndex(x => new { x.UserId, x.TraceStepId });
        modelBuilder.Entity<QuestionPattern>().HasIndex(x => new { x.UserId, x.PatternKey }).IsUnique();
        modelBuilder.Entity<JobFilter>().HasIndex(x => x.UserId).IsUnique();
        modelBuilder.Entity<ProfileValidationRule>().HasIndex(x => new { x.UserId, x.FieldName }).IsUnique();
        modelBuilder.Entity<ApplicationRun>()
            .HasMany(x => x.Jobs)
            .WithOne(x => x.ApplicationRun)
            .HasForeignKey(x => x.ApplicationRunId);
        modelBuilder.Entity<ApplicationJob>()
            .HasOne(x => x.JobLead)
            .WithMany()
            .HasForeignKey(x => x.JobLeadId);
        modelBuilder.Entity<ApplicationJob>()
            .HasOne(x => x.ResumeVersion)
            .WithMany()
            .HasForeignKey(x => x.ResumeVersionId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<ApplicationJob>()
            .HasOne(x => x.ResumeExport)
            .WithMany()
            .HasForeignKey(x => x.ResumeExportId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<ApplicationJob>().HasIndex(x => new { x.UserId, x.ResumeVersionId });
        modelBuilder.Entity<ApplicationJob>().HasIndex(x => new { x.UserId, x.ResumeExportId });
        modelBuilder.Entity<ResumeReviewAnswer>().HasIndex(x => new { x.UserId, x.JobRequirementId });
        modelBuilder.Entity<ResumeReviewAnswer>().HasIndex(x => new { x.UserId, x.ResumeEvidenceMapRunId });
    }
}

public static class DbSeeder
{
    public static async Task SeedAsync(JobPilotDbContext db, IConfiguration configuration)
    {
        var email = configuration["Auth:SeedEmail"] ?? "admin@jobpilot.local";
        var password = configuration["Auth:SeedPassword"] ?? "admin123";

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
                FullName = "Nicolas Portie Sussai Silva",
                Cpf = "",
                Email = email,
                Country = "Brazil",
                City = "Brazil",
                TargetRole = "ASP.NET Developer",
                Seniority = "junior",
                Skills = ["C#", "ASP.NET Core", "React", "SQL", "Entity Framework"],
                ResumePath = configuration["Candidate:DefaultResumePath"] ?? ""
            });
            db.SavedSearches.Add(new SavedSearch
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Name = "ASP.NET Junior Remote",
                Keywords = ".NET",
                Location = "Brazil",
                Workplace = "remote",
                Seniority = "junior",
                LimitPerRun = 25
            });
            db.ReusableAnswers.AddRange(
                new ReusableAnswer
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    QuestionKey = "salary",
                    QuestionText = "Pretensao salarial",
                    AnswerValue = "Minha pretensao e negociavel conforme escopo da vaga, beneficios e formato de contrato.",
                    Category = "screening"
                },
                new ReusableAnswer
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    QuestionKey = "experience_aspnet",
                    QuestionText = "Experiencia com ASP.NET",
                    AnswerValue = "Tenho experiencia com ASP.NET Core, C#, APIs REST, Entity Framework e bancos relacionais.",
                    Category = "screening"
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
            db.AgentPolicies.Add(new AgentPolicy
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                AutoSubmitMode = "conservative",
                ExecutionTarget = ExecutionTargets.Extension,
                MaxApplicationsPerRun = 10,
                MaxApplicationsPerDay = 50
            });
            db.JobFilters.Add(new JobFilter
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                AllowedSeniorities = ["junior"],
                BlockedSeniorities = ["senior", "sr.", "staff", "principal", "lead", "manager"],
                AllowedRoleKeywords = ["asp.net", ".net", "c#", "full stack", "fullstack", "qa", "tester", "quality assurance", "arquiteto asp.net", "asp.net architect"],
                BlockedRoleKeywords = ["suporte", "support", "infra", "sre", "devops", "security", "seguranca", "data engineer", "dados"],
                AllowedWorkplaceTypes = ["remote"],
                BlockedWorkplaceTypes = ["hybrid", "hibrido", "híbrido", "onsite", "presencial"],
                AllowedApplyTypes = ["linkedin_easy_apply", "inhire"],
                MinScore = 30,
                AutoIgnoreLowScore = true
            });
            SeedQuestionPatterns(db, user.Id);
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

            if (!await db.AgentPolicies.AnyAsync(x => x.UserId == user.Id))
            {
                db.AgentPolicies.Add(new AgentPolicy
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    AutoSubmitMode = "conservative",
                    ExecutionTarget = ExecutionTargets.Extension,
                    MaxApplicationsPerRun = 10,
                    MaxApplicationsPerDay = 50
                });
            }

            if (!await db.JobFilters.AnyAsync(x => x.UserId == user.Id))
            {
                db.JobFilters.Add(new JobFilter
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    AllowedSeniorities = ["junior"],
                    BlockedSeniorities = ["senior", "sr.", "staff", "principal", "lead", "manager"],
                    AllowedRoleKeywords = ["asp.net", ".net", "c#", "full stack", "fullstack", "qa", "tester", "quality assurance", "arquiteto asp.net", "asp.net architect"],
                    BlockedRoleKeywords = ["suporte", "support", "infra", "sre", "devops", "security", "seguranca", "data engineer", "dados"],
                    AllowedWorkplaceTypes = ["remote"],
                    BlockedWorkplaceTypes = ["hybrid", "onsite"],
                    AllowedApplyTypes = ["linkedin_easy_apply", "inhire"],
                    MinScore = 30,
                    AutoIgnoreLowScore = true
                });
            }

            SeedQuestionPatterns(db, user.Id);
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

    private static void SeedQuestionPatterns(JobPilotDbContext db, Guid userId)
    {
        var patterns = new[]
        {
            new QuestionPattern
            {
                Id = Guid.NewGuid(), UserId = userId, PatternKey = "salary_expectation",
                Category = "screening", Tags = ["salario", "pretensao", "clt", "remuneracao"],
                QuestionTemplates = ["Pretensao salarial", "Qual sua pretensao salarial?", "Remuneracao", "Salario esperado", "Expectativa salarial"],
                AnswerTemplate = "Minha pretensao e de R$ {{profile.SalaryExpectation}} mensal, negociavel conforme beneficios e formato de contrato.",
                ValueSource = "profile_field", ProfileFieldMapping = "SalaryExpectation", ConfidenceThreshold = 0.90m
            },
            new QuestionPattern
            {
                Id = Guid.NewGuid(), UserId = userId, PatternKey = "years_experience",
                Category = "screening", Tags = ["experiencia", "anos", "years", "xp"],
                QuestionTemplates = ["Anos de experiencia", "Tempo de experiencia", "Anos de experiencia", "Ha quanto tempo trabalha com", "Quanto tempo de experiencia"],
                AnswerTemplate = "Tenho experiencia pratica com {{profile.Skills}} em projetos pessoais e profissionais. Meu foco e em entrega e aprendizado continuo.",
                ValueSource = "ai_draft", ProfileFieldMapping = "ProfessionalSummary", ConfidenceThreshold = 0.70m
            },
            new QuestionPattern
            {
                Id = Guid.NewGuid(), UserId = userId, PatternKey = "work_model",
                Category = "screening", Tags = ["remoto", "home office", "hibrido", "presencial"],
                QuestionTemplates = ["Modelo de trabalho", "Disponibilidade para trabalho remoto", "Prefere remoto ou hibrido?", "Modelo de trabalho"],
                AnswerTemplate = "Prefiro trabalho remoto. Tenho ambiente adequado e experiencia colaborando com times distribuidos.",
                ValueSource = "reusable", ProfileFieldMapping = "", ConfidenceThreshold = 0.90m
            },
            new QuestionPattern
            {
                Id = Guid.NewGuid(), UserId = userId, PatternKey = "english_level",
                Category = "screening", Tags = ["ingles", "english", "idioma"],
                QuestionTemplates = ["Nivel de ingles", "Nivel de ingles", "Fala ingles?", "Como esta seu ingles?"],
                AnswerTemplate = "Tenho ingles tecnico para leitura de documentacao e comunicacao escrita. Estou em constante evolucao.",
                ValueSource = "reusable", ProfileFieldMapping = "", ConfidenceThreshold = 0.85m
            },
            new QuestionPattern
            {
                Id = Guid.NewGuid(), UserId = userId, PatternKey = "availability_start",
                Category = "screening", Tags = ["disponibilidade", "inicio", "start", "quando"],
                QuestionTemplates = ["Disponibilidade para inicio", "Quando pode comecar?", "Disponibilidade", "Previsao de inicio"],
                AnswerTemplate = "Tenho disponibilidade imediata para inicio.",
                ValueSource = "reusable", ProfileFieldMapping = "", ConfidenceThreshold = 0.90m
            },
            new QuestionPattern
            {
                Id = Guid.NewGuid(), UserId = userId, PatternKey = "why_company",
                Category = "screening", Tags = ["motivacao", "por que", "why", "interesse"],
                QuestionTemplates = ["Por que quer trabalhar aqui?", "Por que se interessou pela vaga?", "Por que quer trabalhar aqui?", "O que te atraiu nesta posicao?"],
                AnswerTemplate = "Me interessei pela oportunidade de atuar com {{profile.TargetRole}}. Acredito que minha experiencia com {{profile.Skills}} pode agregar valor ao time.",
                ValueSource = "profile_field", ProfileFieldMapping = "TargetRole,Skills", ConfidenceThreshold = 0.75m
            },
            new QuestionPattern
            {
                Id = Guid.NewGuid(), UserId = userId, PatternKey = "education",
                Category = "screening", Tags = ["formacao", "graduacao", "ensino", "education"],
                QuestionTemplates = ["Formacao academica", "Grau de instrucao", "Formacao academica", "Qual sua formacao?"],
                AnswerTemplate = "Tenho formacao compativel com a area de tecnologia e estudo continuo em {{profile.Skills}}.",
                ValueSource = "profile_field", ProfileFieldMapping = "Skills", ConfidenceThreshold = 0.70m
            },
            new QuestionPattern
            {
                Id = Guid.NewGuid(), UserId = userId, PatternKey = "phone_contact",
                Category = "screening", Tags = ["telefone", "celular", "whatsapp", "contato"],
                QuestionTemplates = ["Numero de telefone", "Celular para contato", "Numero de telefone", "Telefone de contato"],
                AnswerTemplate = "{{profile.Phone}}",
                ValueSource = "profile_field", ProfileFieldMapping = "Phone", ConfidenceThreshold = 0.95m
            },
            new QuestionPattern
            {
                Id = Guid.NewGuid(), UserId = userId, PatternKey = "city_location",
                Category = "screening", Tags = ["cidade", "local", "onde mora", "location"],
                QuestionTemplates = ["Cidade", "Onde mora?", "Localizacao", "Cidade/Estado"],
                AnswerTemplate = "{{profile.City}} - {{profile.Country}}",
                ValueSource = "profile_field", ProfileFieldMapping = "City,Country", ConfidenceThreshold = 0.95m
            },
            new QuestionPattern
            {
                Id = Guid.NewGuid(), UserId = userId, PatternKey = "linkedin_profile",
                Category = "screening", Tags = ["linkedin", "perfil"],
                QuestionTemplates = ["LinkedIn", "Perfil do LinkedIn", "URL do LinkedIn"],
                AnswerTemplate = "{{profile.LinkedinUrl}}",
                ValueSource = "profile_field", ProfileFieldMapping = "LinkedinUrl", ConfidenceThreshold = 0.95m
            },
            new QuestionPattern
            {
                Id = Guid.NewGuid(), UserId = userId, PatternKey = "portfolio_github",
                Category = "screening", Tags = ["github", "portfolio", "projetos"],
                QuestionTemplates = ["GitHub", "Portfolio", "Link para projetos", "GitHub/Portfolio"],
                AnswerTemplate = "{{profile.GithubUrl}}",
                ValueSource = "profile_field", ProfileFieldMapping = "GithubUrl", ConfidenceThreshold = 0.90m
            },
            new QuestionPattern
            {
                Id = Guid.NewGuid(), UserId = userId, PatternKey = "challenge_learning",
                Category = "screening", Tags = ["desafio", "aprender", "growth"],
                QuestionTemplates = ["O que voce espera aprender?", "Qual seu maior desafio?", "Como voce lida com desafios?"],
                AnswerTemplate = "Gosto de aprender na pratica. Meus projetos com {{profile.Skills}} me ensinaram a resolver problemas de forma autonoma.",
                ValueSource = "profile_field", ProfileFieldMapping = "Skills", ConfidenceThreshold = 0.70m
            },
            new QuestionPattern
            {
                Id = Guid.NewGuid(), UserId = userId, PatternKey = "salary_current",
                Category = "screening", Tags = ["salario atual", "quanto ganha", "current salary"],
                QuestionTemplates = ["Salario atual", "Quanto voce ganha hoje?", "Salario atual", "Remuneracao atual"],
                AnswerTemplate = "Prefiro focar na pretensao para a posicao, que e de R$ {{profile.SalaryExpectation}}.",
                ValueSource = "profile_field", ProfileFieldMapping = "SalaryExpectation", ConfidenceThreshold = 0.80m
            },
            new QuestionPattern
            {
                Id = Guid.NewGuid(), UserId = userId, PatternKey = "team_work",
                Category = "screening", Tags = ["trabalho em equipe", "colaboracao", "team"],
                QuestionTemplates = ["Trabalho em equipe", "Como voce trabalha em equipe?", "Trabalho em equipe", "Colaboracao"],
                AnswerTemplate = "Gosto de colaborar, compartilhar ideias e aprender com o time. Ja trabalhei com metodologias ageis e integracao continua.",
                ValueSource = "reusable", ProfileFieldMapping = "", ConfidenceThreshold = 0.75m
            },
            new QuestionPattern
            {
                Id = Guid.NewGuid(), UserId = userId, PatternKey = "disability_pcd",
                Category = "screening", Tags = ["deficiencia", "pcd", "disability"],
                QuestionTemplates = ["Voce e PCD?", "Possui alguma deficiencia?", "Deficiencia", "PCD"],
                AnswerTemplate = "",
                ValueSource = "checkpoint", ProfileFieldMapping = "", ConfidenceThreshold = 0.95m
            }
        };

        foreach (var p in patterns)
        {
            if (!db.QuestionPatterns.Any(x => x.UserId == userId && x.PatternKey == p.PatternKey))
            {
                db.QuestionPatterns.Add(p);
            }
        }
    }

    private static void SeedProfileValidationRules(JobPilotDbContext db, Guid userId)
    {
        var rules = new[]
        {
            new ProfileValidationRule { Id = Guid.NewGuid(), UserId = userId, FieldName = "Phone", ValidationType = "regex", ValidationValue = @"^(\(?\d{2}\)?\s?\d{4,5}-?\d{4})$", ErrorMessage = "Telefone invalido. Use formato (11) 99999-9999.", IsCritical = true },
            new ProfileValidationRule { Id = Guid.NewGuid(), UserId = userId, FieldName = "Email", ValidationType = "regex", ValidationValue = @"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Email invalido.", IsCritical = true },
            new ProfileValidationRule { Id = Guid.NewGuid(), UserId = userId, FieldName = "ResumePath", ValidationType = "file_exists", ValidationValue = "", ErrorMessage = "Arquivo de curriculo nao encontrado no caminho informado.", IsCritical = true },
            new ProfileValidationRule { Id = Guid.NewGuid(), UserId = userId, FieldName = "SalaryExpectation", ValidationType = "not_empty", ValidationValue = "", ErrorMessage = "Pretensao salarial nao informada. O agente pode preencher errado.", IsCritical = true },
            new ProfileValidationRule { Id = Guid.NewGuid(), UserId = userId, FieldName = "Skills", ValidationType = "length", ValidationValue = "3", ErrorMessage = "Adicione pelo menos 3 skills ao seu perfil.", IsCritical = false },
            new ProfileValidationRule { Id = Guid.NewGuid(), UserId = userId, FieldName = "FullName", ValidationType = "regex", ValidationValue = @"^\S+\s+\S+$", ErrorMessage = "Informe nome completo (nome e sobrenome).", IsCritical = true },
            new ProfileValidationRule { Id = Guid.NewGuid(), UserId = userId, FieldName = "City", ValidationType = "not_empty", ValidationValue = "", ErrorMessage = "Cidade nao informada.", IsCritical = false },
            new ProfileValidationRule { Id = Guid.NewGuid(), UserId = userId, FieldName = "ProfessionalSummary", ValidationType = "not_empty", ValidationValue = "", ErrorMessage = "Resumo profissional vazio. Ajuda o agente a montar respostas personalizadas.", IsCritical = false }
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
