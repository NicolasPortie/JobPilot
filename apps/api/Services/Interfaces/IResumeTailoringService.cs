namespace JobPilot.Api.Services;

public interface IResumeTailoringService
{
    Task<TailoredResumeResponse> TailorAsync(Guid userId, TailorResumeRequest request);
    Task<TailoredResumeResponse> BuildGenericAtsAsync(Guid userId, BuildGenericAtsResumeRequest request);
}
