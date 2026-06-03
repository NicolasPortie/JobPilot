using JobPilot.Agent.Domain;
using Microsoft.Playwright;

namespace JobPilot.Agent.Application;

public interface IApplicationPlatformFlow
{
    bool CanHandle(JobLead lead);
    Task NavigateToLeadAsync(IPage page, AgentTaskResponse task);
    Task RecoverAfterLoginAsync(IPage page, AgentTaskResponse task);
    Task<ApplicationSurfaceOpenResult> OpenApplicationSurfaceAsync(IPage page, AgentTaskResponse task);
}

public sealed record ApplicationSurfaceOpenResult(
    bool Opened,
    string FailureCode = "",
    string FailureState = "needs_review",
    string FieldLabel = "",
    string Detail = "",
    string SurfaceKind = "",
    string FinalUrl = "",
    string SuggestedPlatform = "",
    IPage? ActivePage = null)
{
    public static ApplicationSurfaceOpenResult Success { get; } = new(
        true,
        SurfaceKind: "linkedin_easy_apply",
        SuggestedPlatform: "LinkedIn");

    public static ApplicationSurfaceOpenResult OpenedSurface(
        string surfaceKind,
        string suggestedPlatform,
        string finalUrl = "",
        string detail = "",
        IPage? activePage = null) => new(
            true,
            Detail: detail,
            SurfaceKind: surfaceKind,
            FinalUrl: finalUrl,
            SuggestedPlatform: suggestedPlatform,
            ActivePage: activePage);

    public static ApplicationSurfaceOpenResult External(IPage page, string surfaceKind, string detail) => new(
        true,
        Detail: detail,
        SurfaceKind: surfaceKind,
        FinalUrl: page.Url,
        SuggestedPlatform: "Generic",
        ActivePage: page);
}
