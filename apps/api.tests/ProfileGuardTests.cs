using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Tests;

public class ProfileGuardTests
{
    private static JobPilotDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<JobPilotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new JobPilotDbContext(options);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsWarning_WhenPhoneInvalid()
    {
        var db = CreateDb();
        var userId = Guid.NewGuid();
        db.CandidateProfiles.Add(new CandidateProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FullName = "Nicolas Teste",
            Phone = "123",
            Email = "test@email.com",
            SalaryExpectation = "3800.00",
            Skills = ["C#", "ASP.NET", "React"]
        });
        await db.SaveChangesAsync();

        var guard = new ProfileGuard(db);
        var issues = await guard.ValidateAsync(userId);

        Assert.Contains(issues, i => i.FieldName == "Phone" && !i.IsCritical);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsCritical_WhenEmailInvalid()
    {
        var db = CreateDb();
        var userId = Guid.NewGuid();
        db.CandidateProfiles.Add(new CandidateProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FullName = "Nicolas Teste",
            Phone = "(11) 99999-9999",
            Email = "emailinvalido",
            SalaryExpectation = "3800.00",
            Skills = ["C#", "ASP.NET", "React"]
        });
        await db.SaveChangesAsync();

        var guard = new ProfileGuard(db);
        var issues = await guard.ValidateAsync(userId);

        Assert.Contains(issues, i => i.FieldName == "Email" && i.IsCritical);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsEmpty_WhenProfileValid()
    {
        var db = CreateDb();
        var userId = Guid.NewGuid();
        var resumePath = Path.GetTempFileName();
        File.WriteAllText(resumePath, "resume");
        db.CandidateProfiles.Add(new CandidateProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FullName = "Nicolas Teste",
            Phone = "(11) 99999-9999",
            Email = "test@email.com",
            City = "Sao Paulo",
            SalaryExpectation = "3800.00",
            Skills = ["C#", "ASP.NET", "React"],
            ProfessionalSummary = "Desenvolvedor full stack com foco em .NET",
            ResumePath = resumePath
        });
        await db.SaveChangesAsync();

        var guard = new ProfileGuard(db);
        var issues = await guard.ValidateAsync(userId);

        try { File.Delete(resumePath); } catch { }
        Assert.Empty(issues);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsWarning_WhenSkillsLessThan3()
    {
        var db = CreateDb();
        var userId = Guid.NewGuid();
        db.CandidateProfiles.Add(new CandidateProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FullName = "Nicolas Teste",
            Phone = "(11) 99999-9999",
            Email = "test@email.com",
            SalaryExpectation = "3800.00",
            Skills = ["C#"]
        });
        await db.SaveChangesAsync();

        var guard = new ProfileGuard(db);
        var issues = await guard.ValidateAsync(userId);

        Assert.Contains(issues, i => i.FieldName == "Skills" && !i.IsCritical);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsCritical_WhenSalaryEmpty()
    {
        var db = CreateDb();
        var userId = Guid.NewGuid();
        db.CandidateProfiles.Add(new CandidateProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FullName = "Nicolas Teste",
            Phone = "(11) 99999-9999",
            Email = "test@email.com",
            SalaryExpectation = "",
            Skills = ["C#", "ASP.NET", "React"]
        });
        await db.SaveChangesAsync();

        var guard = new ProfileGuard(db);
        var issues = await guard.ValidateAsync(userId);

        Assert.Contains(issues, i => i.FieldName == "SalaryExpectation" && i.IsCritical);
    }
}
