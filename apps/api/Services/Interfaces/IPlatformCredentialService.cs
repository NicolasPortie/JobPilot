using JobPilot.Api.Application;

namespace JobPilot.Api.Services;

public interface IPlatformCredentialService
{
    Task<List<PlatformCredentialResponse>> ListAsync(Guid userId);
    Task<ServiceResult<PlatformCredentialResponse>> CreateAsync(Guid userId, PlatformCredentialRequest request);
    Task<ServiceResult<PlatformCredentialResponse>> UpdateAsync(Guid userId, Guid id, PlatformCredentialRequest request);
    Task<ServiceResult<PlatformCredentialResponse>> DeleteAsync(Guid userId, Guid id);
    Task<List<AgentPlatformCredentialResponse>> ListForAgentAsync(Guid userId);
}
