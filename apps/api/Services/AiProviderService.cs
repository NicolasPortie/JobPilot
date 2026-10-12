using JobPilot.Api.Application;
using JobPilot.Api.Domain;
using JobPilot.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Services;

public sealed class AiProviderService(
    IRepository<AiProvider> providers,
    IUnitOfWork unitOfWork,
    IConfiguration configuration,
    AiCompletionService completion)
{
    public async Task<List<AiProviderResponse>> ListAsync(Guid userId)
    {
        var allProviders = await providers.Query()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Name)
            .ToListAsync();

        return allProviders.Select(ToResponse).ToList();
    }

    public async Task<ServiceResult<AiProviderResponse>> CreateAsync(Guid userId, AiProviderRequest request)
    {
        AiProviderEndpointPolicy.Validate(request, configuration);
        if (request.IsDefault)
        {
            await providers.Query()
                .Where(x => x.UserId == userId)
                .ExecuteUpdateAsync(x => x.SetProperty(p => p.IsDefault, false));
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
            EncryptedApiKey = SecretProtector.Protect(request.ApiKey, configuration),
            IsActive = request.IsActive,
            IsDefault = request.IsDefault
        };

        providers.Add(provider);
        await unitOfWork.SaveChangesAsync();
        return ServiceResult<AiProviderResponse>.Created(ToResponse(provider));
    }

    public async Task<ServiceResult<AiProviderResponse>> UpdateAsync(Guid userId, Guid id, AiProviderRequest request)
    {
        AiProviderEndpointPolicy.Validate(request, configuration);
        var provider = await providers.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (provider is null) return ServiceResult<AiProviderResponse>.NotFound();

        if (request.IsDefault)
        {
            await providers.Query()
                .Where(x => x.UserId == userId && x.Id != id)
                .ExecuteUpdateAsync(x => x.SetProperty(p => p.IsDefault, false));
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
            provider.EncryptedApiKey = SecretProtector.Protect(request.ApiKey, configuration);
        }

        await unitOfWork.SaveChangesAsync();
        return ServiceResult<AiProviderResponse>.Ok(ToResponse(provider));
    }

    public async Task<ServiceResult<AiProviderResponse>> DeleteAsync(Guid userId, Guid id)
    {
        var provider = await providers.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (provider is null) return ServiceResult<AiProviderResponse>.NotFound();

        providers.Remove(provider);
        await unitOfWork.SaveChangesAsync();
        return ServiceResult<AiProviderResponse>.NoContent();
    }

    public async Task<ServiceResult<AiProviderTestResponse>> TestAsync(Guid userId, Guid id)
    {
        var provider = await providers.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (provider is null) return ServiceResult<AiProviderTestResponse>.NotFound();

        var response = await completion.TestProviderConnectivityAsync(provider, userId);
        return ServiceResult<AiProviderTestResponse>.Ok(new(response.Ok, provider.Name, response.Model, response.Sample, response.Error));
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

public sealed record AiProviderTestResponse(bool Ok, string Provider, string Model, string Sample, string? Error);
