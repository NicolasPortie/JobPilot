using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Tests;

public class JobFilterServiceTests
{
    private static JobPilotDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<JobPilotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new JobPilotDbContext(options);
    }

    [Fact]
    public async Task GetOrCreateAsync_CreatesDefaultFilter_WhenNoneExists()
    {
        var db = CreateDb();
        var service = new JobFilterService(db);
        var userId = Guid.NewGuid();

        var filter = await service.GetOrCreateAsync(userId);

        Assert.NotNull(filter);
        Assert.Contains("junior", filter.AllowedSeniorities);
        Assert.Contains("senior", filter.BlockedSeniorities);
        Assert.Contains("qa", filter.AllowedRoleKeywords);
        Assert.Contains("remote", filter.AllowedWorkplaceTypes);
        Assert.Contains("hybrid", filter.BlockedWorkplaceTypes);
    }

    [Fact]
    public void ShouldIgnore_DoesNotBlockAllowedAspNetArchitectRole()
    {
        var db = CreateDb();
        var service = new JobFilterService(db);
        var filter = new JobFilter
        {
            BlockedSeniorities = ["senior", "lead", "manager"],
            AllowedRoleKeywords = ["arquiteto asp.net", "asp.net architect"],
            BlockedRoleKeywords = ["suporte"],
            BlockedWorkplaceTypes = ["hybrid"],
            AllowedWorkplaceTypes = ["remote"],
            MinScore = 30,
            AutoIgnoreLowScore = true
        };

        var lead = new JobLead
        {
            Title = "Arquiteto ASP.NET",
            Company = "TechCorp",
            Location = "Brasil",
            Score = 70
        };

        var ignored = service.ShouldIgnore(lead, filter, out var reason);

        Assert.False(ignored);
        Assert.Equal("", reason);
    }

    [Fact]
    public void ShouldIgnore_BlocksConfiguredRoleKeyword()
    {
        var db = CreateDb();
        var service = new JobFilterService(db);
        var filter = new JobFilter
        {
            BlockedSeniorities = ["senior"],
            AllowedRoleKeywords = ["asp.net"],
            BlockedRoleKeywords = ["suporte"],
            BlockedWorkplaceTypes = ["hybrid"],
            AllowedWorkplaceTypes = ["remote"],
            MinScore = 30,
            AutoIgnoreLowScore = true
        };

        var lead = new JobLead
        {
            Title = "Analista de Suporte",
            Company = "TechCorp",
            Location = "Remote",
            Score = 80
        };

        var ignored = service.ShouldIgnore(lead, filter, out var reason);

        Assert.True(ignored);
        Assert.Contains("suporte", reason);
    }

    [Fact]
    public void ShouldIgnore_BlocksSeniorInTitle()
    {
        var db = CreateDb();
        var service = new JobFilterService(db);
        var filter = new JobFilter
        {
            BlockedSeniorities = ["senior", "sr.", "staff"],
            BlockedWorkplaceTypes = ["hybrid"],
            AllowedWorkplaceTypes = ["remote"],
            MinScore = 30,
            AutoIgnoreLowScore = true
        };

        var lead = new JobLead
        {
            Title = "Desenvolvedor Senior .NET",
            Company = "TechCorp",
            Location = "Remote"
        };

        var ignored = service.ShouldIgnore(lead, filter, out var reason);

        Assert.True(ignored);
        Assert.Contains("senior", reason);
    }

    [Fact]
    public void ShouldIgnore_BlocksHybridInLocation()
    {
        var db = CreateDb();
        var service = new JobFilterService(db);
        var filter = new JobFilter
        {
            BlockedSeniorities = ["senior"],
            BlockedWorkplaceTypes = ["hybrid", "hibrido", "híbrido", "onsite"],
            AllowedWorkplaceTypes = ["remote"],
            MinScore = 30,
            AutoIgnoreLowScore = true
        };

        var lead = new JobLead
        {
            Title = "Desenvolvedor Junior .NET",
            Company = "TechCorp",
            Location = "Sao Paulo - Hibrido"
        };

        var ignored = service.ShouldIgnore(lead, filter, out var reason);

        Assert.True(ignored);
        Assert.Contains("hibrido", reason.ToLowerInvariant());
    }

    [Fact]
    public void ShouldIgnore_AllowsJuniorRemote()
    {
        var db = CreateDb();
        var service = new JobFilterService(db);
        var filter = new JobFilter
        {
            BlockedSeniorities = ["senior", "sr.", "staff"],
            BlockedWorkplaceTypes = ["hybrid"],
            AllowedWorkplaceTypes = ["remote"],
            MinScore = 30,
            AutoIgnoreLowScore = true
        };

        var lead = new JobLead
        {
            Title = "Desenvolvedor Junior .NET",
            Company = "TechCorp",
            Location = "Remoto",
            Score = 50
        };

        var ignored = service.ShouldIgnore(lead, filter, out _);

        Assert.False(ignored);
    }

    [Fact]
    public void ShouldIgnore_TreatsEmptyLocationAsUnknownForRemoteSearch()
    {
        var db = CreateDb();
        var service = new JobFilterService(db);
        var filter = new JobFilter
        {
            BlockedSeniorities = ["senior"],
            BlockedWorkplaceTypes = ["hybrid", "onsite"],
            AllowedWorkplaceTypes = ["remote"],
            MinScore = 30,
            AutoIgnoreLowScore = true
        };

        var lead = new JobLead
        {
            Title = "Desenvolvedor Junior .NET",
            Company = "TechCorp",
            Location = "",
            Score = 50
        };

        var ignored = service.ShouldIgnore(lead, filter, out _);

        Assert.False(ignored);
        Assert.False(lead.IsRemote);
        Assert.Contains("sem sinal remoto", lead.ScoreReason);
    }

    [Fact]
    public void ShouldIgnore_TreatsBrazilLocationAsUnknownForRemoteSearch()
    {
        var db = CreateDb();
        var service = new JobFilterService(db);
        var filter = new JobFilter
        {
            BlockedSeniorities = ["senior"],
            BlockedWorkplaceTypes = ["hybrid", "onsite"],
            AllowedWorkplaceTypes = ["remote"],
            MinScore = 30,
            AutoIgnoreLowScore = true
        };

        var lead = new JobLead
        {
            Title = "Desenvolvedor Junior .NET",
            Company = "TechCorp",
            Location = "Brasil",
            Score = 50
        };

        var ignored = service.ShouldIgnore(lead, filter, out _);

        Assert.False(ignored);
        Assert.False(lead.IsRemote);
        Assert.Contains("sem sinal remoto", lead.ScoreReason);
    }

    [Fact]
    public void ShouldIgnore_DoesNotBlockMissingRemoteBadge()
    {
        var db = CreateDb();
        var service = new JobFilterService(db);
        var filter = new JobFilter
        {
            BlockedSeniorities = ["senior"],
            BlockedWorkplaceTypes = ["hybrid", "onsite"],
            AllowedWorkplaceTypes = ["remote"],
            MinScore = 30,
            AutoIgnoreLowScore = true
        };

        var lead = new JobLead
        {
            Title = "Engenheiro de Software .NET",
            Company = "TechCorp",
            Location = "Sao Paulo, Brasil",
            Score = 55,
            ScoreReason = "cargo tecnico compativel"
        };

        var ignored = service.ShouldIgnore(lead, filter, out var reason);

        Assert.False(ignored);
        Assert.Equal("", reason);
        Assert.Contains("sem sinal remoto", lead.ScoreReason);
    }

    [Fact]
    public void BuildSearchUrl_KeepsLinkedInDiscoveryBroad()
    {
        var db = CreateDb();
        var service = new JobFilterService(db);
        var filter = new JobFilter
        {
            AllowedSeniorities = ["junior", "entry"],
            AllowedWorkplaceTypes = ["remote"],
            AllowedApplyTypes = ["easy_apply", "external_form"]
        };

        var url = service.BuildSearchUrl(filter, "ASP.NET", "Brazil", 25);

        Assert.DoesNotContain("f_E=", url);
        Assert.DoesNotContain("f_WT=", url);
    }

    [Fact]
    public void BuildSearchUrl_RemovesEasyApplyFilter_WhenExternalAllowed()
    {
        var db = CreateDb();
        var service = new JobFilterService(db);
        var filter = new JobFilter
        {
            AllowedSeniorities = ["junior"],
            AllowedWorkplaceTypes = ["remote"],
            AllowedApplyTypes = ["easy_apply", "external_form"]
        };

        var url = service.BuildSearchUrl(filter, "ASP.NET", "Brazil", 25);

        Assert.DoesNotContain("f_AL=true", url);
    }
}
