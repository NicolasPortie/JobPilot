using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Services;

public sealed class PlatformCredentialService
{
    private readonly JobPilotDbContext _db;
    private readonly IConfiguration _configuration;

    public PlatformCredentialService(JobPilotDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public async Task<List<PlatformCredentialResponse>> ListAsync(Guid userId)
    {
        var credentials = await _db.PlatformCredentials
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.Platform)
            .ToListAsync();

        return credentials.Select(ToResponse).ToList();
    }

    public async Task<IResult> CreateAsync(Guid userId, PlatformCredentialRequest request)
    {
        var credential = new PlatformCredential
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Platform = request.Platform,
            AccountEmail = request.AccountEmail,
            EncryptedPassword = SecretProtector.Protect(request.Password, _configuration),
            IsActive = request.IsActive
        };

        _db.PlatformCredentials.Add(credential);
        await _db.SaveChangesAsync();
        return Results.Created($"/api/platform-credentials/{credential.Id}", ToResponse(credential));
    }

    public async Task<IResult> UpdateAsync(Guid userId, Guid id, PlatformCredentialRequest request)
    {
        var credential = await _db.PlatformCredentials.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (credential is null) return Results.NotFound();

        credential.Platform = request.Platform;
        credential.AccountEmail = request.AccountEmail;
        credential.IsActive = request.IsActive;
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            credential.EncryptedPassword = SecretProtector.Protect(request.Password, _configuration);
        }
        credential.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return Results.Ok(ToResponse(credential));
    }

    public async Task<IResult> DeleteAsync(Guid userId, Guid id)
    {
        var credential = await _db.PlatformCredentials.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (credential is null) return Results.NotFound();

        _db.PlatformCredentials.Remove(credential);
        await _db.SaveChangesAsync();
        return Results.NoContent();
    }

    public async Task<List<AgentPlatformCredentialResponse>> ListForAgentAsync(Guid userId)
    {
        var credentials = await _db.PlatformCredentials
            .Where(x => x.UserId == userId && x.IsActive)
            .OrderBy(x => x.Platform)
            .ToListAsync();

        return credentials
            .Select(x => new AgentPlatformCredentialResponse(x.Platform, x.AccountEmail, SecretProtector.Unprotect(x.EncryptedPassword, _configuration)))
            .ToList();
    }

    private static PlatformCredentialResponse ToResponse(PlatformCredential credential) =>
        new(
            credential.Id,
            credential.Platform,
            credential.AccountEmail,
            !string.IsNullOrWhiteSpace(credential.EncryptedPassword),
            credential.IsActive,
            credential.UpdatedAt);
}
