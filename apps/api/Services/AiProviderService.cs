using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Services;

public sealed class AiProviderService
{
    private readonly JobPilotDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly IAiChatService _ai;

    public AiProviderService(JobPilotDbContext db, IConfiguration configuration, IAiChatService ai)
    {
        _db = db;
        _configuration = configuration;
        _ai = ai;
    }

    public async Task<List<AiProviderResponse>> ListAsync(Guid userId)
    {
        var providers = await _db.AiProviders
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Name)
            .ToListAsync();

        return providers.Select(ToResponse).ToList();
    }

    public async Task<IResult> CreateAsync(Guid userId, AiProviderRequest request)
    {
        if (request.IsDefault)
        {
            await _db.AiProviders.Where(x => x.UserId == userId).ExecuteUpdateAsync(x => x.SetProperty(p => p.IsDefault, false));
        }

        var provider = new AiProvider
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name,
            ProviderType = request.ProviderType,
            BaseUrl = request.BaseUrl,
            Model = request.Model,
            ApiFormat = request.ApiFormat,
            EncryptedApiKey = SecretProtector.Protect(request.ApiKey, _configuration),
            IsActive = request.IsActive,
            IsDefault = request.IsDefault
        };

        _db.AiProviders.Add(provider);
        await _db.SaveChangesAsync();
        return Results.Created($"/api/ai/providers/{provider.Id}", ToResponse(provider));
    }

    public async Task<IResult> UpdateAsync(Guid userId, Guid id, AiProviderRequest request)
    {
        var provider = await _db.AiProviders.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (provider is null) return Results.NotFound();

        if (request.IsDefault)
        {
            await _db.AiProviders.Where(x => x.UserId == userId && x.Id != id).ExecuteUpdateAsync(x => x.SetProperty(p => p.IsDefault, false));
        }

        provider.Apply(new AiProvider
        {
            Name = request.Name,
            ProviderType = request.ProviderType,
            BaseUrl = request.BaseUrl,
            Model = request.Model,
            ApiFormat = request.ApiFormat,
            IsActive = request.IsActive,
            IsDefault = request.IsDefault
        });
        if (!string.IsNullOrWhiteSpace(request.ApiKey))
        {
            provider.EncryptedApiKey = SecretProtector.Protect(request.ApiKey, _configuration);
        }

        await _db.SaveChangesAsync();
        return Results.Ok(ToResponse(provider));
    }

    public async Task<IResult> DeleteAsync(Guid userId, Guid id)
    {
        var provider = await _db.AiProviders.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (provider is null) return Results.NotFound();

        _db.AiProviders.Remove(provider);
        await _db.SaveChangesAsync();
        return Results.NoContent();
    }

    public async Task<IResult> TestAsync(Guid userId, Guid id)
    {
        var provider = await _db.AiProviders.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (provider is null) return Results.NotFound();

        var response = await _ai.TestProviderAsync(userId, id);
        return Results.Ok(new { ok = response.UsedKimi, provider = provider.Name, model = response.Model, sample = response.Text });
    }

    private static AiProviderResponse ToResponse(AiProvider provider) =>
        new(
            provider.Id,
            provider.Name,
            provider.ProviderType,
            provider.BaseUrl,
            provider.Model,
            provider.ApiFormat,
            !string.IsNullOrWhiteSpace(provider.EncryptedApiKey),
            provider.IsActive,
            provider.IsDefault,
            provider.UpdatedAt);
}
