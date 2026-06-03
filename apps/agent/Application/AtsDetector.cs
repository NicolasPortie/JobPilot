using JobPilot.Agent.Domain;

namespace JobPilot.Agent.Application;

/// <summary>
/// Detecta a plataforma ATS (Applicant Tracking System) baseado em URL e DOM.
/// </summary>
public sealed class AtsDetector
{
    public AtsPlatform Detect(PageObservation observation)
    {
        var url = observation.Url.ToLowerInvariant();
        var text = observation.VisibleText.ToLowerInvariant();

        // Detecção por URL (mais confiável)
        if (url.Contains("greenhouse.io")) return AtsPlatform.Greenhouse;
        if (url.Contains("boards.greenhouse.io")) return AtsPlatform.Greenhouse;
        if (url.Contains("lever.co")) return AtsPlatform.Lever;
        if (url.Contains("jobs.lever.co")) return AtsPlatform.Lever;
        if (url.Contains("ashbyhq.com")) return AtsPlatform.Ashby;
        if (url.Contains("workable.com")) return AtsPlatform.Workable;
        if (url.Contains("apply.workable.com")) return AtsPlatform.Workable;
        if (url.Contains("comeet")) return AtsPlatform.Comeet;
        if (url.Contains("inhire.app")) return AtsPlatform.Inhire;
        if (url.Contains("gupy.io")) return AtsPlatform.Gupy;
        if (url.Contains("workday.com")) return AtsPlatform.Workday;
        if (url.Contains("myworkdayjobs.com")) return AtsPlatform.Workday;
        if (url.Contains("smartrecruiters.com")) return AtsPlatform.SmartRecruiters;
        if (url.Contains("recruitee.com")) return AtsPlatform.Recruitee;
        if (url.Contains("personio.de")) return AtsPlatform.Personio;
        if (url.Contains("linkedin.com/jobs")) return AtsPlatform.LinkedInEasyApply;
        if (url.Contains("indeed.com")) return AtsPlatform.IndeedApply;

        // Detecção por DOM heurística
        if (text.Contains("greenhouse")) return AtsPlatform.Greenhouse;
        if (text.Contains("lever")) return AtsPlatform.Lever;
        if (text.Contains("ashby")) return AtsPlatform.Ashby;
        if (text.Contains("workable")) return AtsPlatform.Workable;
        if (text.Contains("comeet")) return AtsPlatform.Comeet;
        if (text.Contains("workday")) return AtsPlatform.Workday;
        if (text.Contains("smartrecruiters")) return AtsPlatform.SmartRecruiters;

        return AtsPlatform.Unknown;
    }
}

public enum AtsPlatform
{
    Unknown,
    LinkedInEasyApply,
    Greenhouse,
    Lever,
    Ashby,
    Workable,
    Personio,
    Comeet,
    Recruitee,
    SmartRecruiters,
    Workday,
    IndeedApply,
    Gupy,
    Inhire
}
