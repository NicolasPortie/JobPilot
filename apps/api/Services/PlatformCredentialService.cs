using System.Security.Cryptography;
using JobPilot.Api.Application;
using JobPilot.Api.Domain;
using JobPilot.Api.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace JobPilot.Api.Services;

public sealed class PlatformCredentialService : IPlatformCredentialService
{
    private readonly IRepository<PlatformCredential> _credentials;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PlatformCredentialService> _logger;

    public PlatformCredentialService(
        IRepository<PlatformCredential> credentials,
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        ILogger<PlatformCredentialService> logger)
    {
        _credentials = credentials;
        _unitOfWork = unitOfWork;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<List<PlatformCredentialResponse>> ListAsync(Guid userId)
    {
        var credentials = await _credentials.ListAsync(x => x.UserId == userId);
        return credentials.OrderBy(x => x.Platform).Select(ToResponse).ToList();
    }

    public async Task<ServiceResult<PlatformCredentialResponse>> CreateAsync(Guid userId, PlatformCredentialRequest request)
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

        _credentials.Add(credential);
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult<PlatformCredentialResponse>.Created(ToResponse(credential));
    }

    public async Task<ServiceResult<PlatformCredentialResponse>> UpdateAsync(Guid userId, Guid id, PlatformCredentialRequest request)
    {
        var credential = await _credentials.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (credential is null) return ServiceResult<PlatformCredentialResponse>.NotFound();

        credential.Platform = request.Platform;
        credential.AccountEmail = request.AccountEmail;
        credential.IsActive = request.IsActive;
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            credential.EncryptedPassword = SecretProtector.Protect(request.Password, _configuration);
        }
        credential.UpdatedAt = DateTimeOffset.UtcNow;

        await _unitOfWork.SaveChangesAsync();
        return ServiceResult<PlatformCredentialResponse>.Ok(ToResponse(credential));
    }

    public async Task<ServiceResult<PlatformCredentialResponse>> DeleteAsync(Guid userId, Guid id)
    {
        var credential = await _credentials.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (credential is null) return ServiceResult<PlatformCredentialResponse>.NotFound();

        _credentials.Remove(credential);
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult<PlatformCredentialResponse>.NoContent();
    }

    public async Task<List<AgentPlatformCredentialResponse>> ListForAgentAsync(Guid userId)
    {
        var credentials = await _credentials.ListAsync(x => x.UserId == userId && x.IsActive);
        var result = new List<AgentPlatformCredentialResponse>();
        var migrated = false;

        foreach (var credential in credentials.OrderBy(x => x.Platform))
        {
            if (!SecretProtector.TryUnprotect(credential.EncryptedPassword, _configuration, out var password, out var matchedKeyLabel))
            {
                _logger.LogWarning(
                    "Falha ao decriptar a credencial {CredentialId} ({Platform}) do usuário {UserId}. " +
                    "Nenhuma chave de proteção conhecida funcionou.",
                    credential.Id, credential.Platform, userId);
                continue;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                _logger.LogWarning(
                    "Credencial {CredentialId} ({Platform}) do usuário {UserId} sem senha utilizável; ignorada.",
                    credential.Id, credential.Platform, userId);
                continue;
            }

            if (matchedKeyLabel is "Auth:JwtKey")
            {
                credential.EncryptedPassword = SecretProtector.Protect(password, _configuration);
                credential.UpdatedAt = DateTimeOffset.UtcNow;
                migrated = true;
                _logger.LogInformation(
                    "Credencial {CredentialId} ({Platform}) re-cifrada com a chave atual após decriptação legada.",
                    credential.Id, credential.Platform);
            }

            result.Add(new AgentPlatformCredentialResponse(credential.Platform, credential.AccountEmail, password));
        }

        if (migrated)
            await _unitOfWork.SaveChangesAsync();

        return result;
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
