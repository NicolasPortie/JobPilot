using JobPilot.Agent.Application;
using JobPilot.Agent.Domain;

namespace JobPilot.Agent.Tests;

public class AtsDetectorTests
{
    private readonly AtsDetector _detector = new();

    [Fact]
    public void Detect_GreenhouseUrl_Detecta()
    {
        var obs = new PageObservation("https://boards.greenhouse.io/company/jobs/123", "", "", []);
        var platform = _detector.Detect(obs);
        Assert.Equal(AtsPlatform.Greenhouse, platform);
    }

    [Fact]
    public void Detect_LeverUrl_Detecta()
    {
        var obs = new PageObservation("https://jobs.lever.co/company/123", "", "", []);
        var platform = _detector.Detect(obs);
        Assert.Equal(AtsPlatform.Lever, platform);
    }

    [Fact]
    public void Detect_AshbyUrl_Detecta()
    {
        var obs = new PageObservation("https://jobs.ashbyhq.com/company", "", "", []);
        var platform = _detector.Detect(obs);
        Assert.Equal(AtsPlatform.Ashby, platform);
    }

    [Fact]
    public void Detect_WorkableUrl_Detecta()
    {
        var obs = new PageObservation("https://apply.workable.com/company/j/123", "", "", []);
        var platform = _detector.Detect(obs);
        Assert.Equal(AtsPlatform.Workable, platform);
    }

    [Fact]
    public void Detect_GupyUrl_Detecta()
    {
        var obs = new PageObservation("https://company.gupy.io/jobs/123", "", "", []);
        var platform = _detector.Detect(obs);
        Assert.Equal(AtsPlatform.Gupy, platform);
    }

    [Fact]
    public void Detect_WorkdayUrl_Detecta()
    {
        var obs = new PageObservation("https://company.wd101.myworkdayjobs.com/", "", "", []);
        var platform = _detector.Detect(obs);
        Assert.Equal(AtsPlatform.Workday, platform);
    }

    [Fact]
    public void Detect_LinkedinUrl_Linkedin()
    {
        var obs = new PageObservation("https://www.linkedin.com/jobs/view/123", "", "", []);
        var platform = _detector.Detect(obs);
        Assert.Equal(AtsPlatform.LinkedInEasyApply, platform);
    }

    [Fact]
    public void Detect_IndeedUrl_Indeed()
    {
        var obs = new PageObservation("https://br.indeed.com/viewjob?jk=123", "", "", []);
        var platform = _detector.Detect(obs);
        Assert.Equal(AtsPlatform.IndeedApply, platform);
    }

    [Fact]
    public void Detect_UrlDesconhecida_Unknown()
    {
        var obs = new PageObservation("https://example.com/careers", "", "", []);
        var platform = _detector.Detect(obs);
        Assert.Equal(AtsPlatform.Unknown, platform);
    }

    [Fact]
    public void Detect_SmartRecruitersUrl_Detecta()
    {
        var obs = new PageObservation("https://jobs.smartrecruiters.com/Company/123", "", "", []);
        var platform = _detector.Detect(obs);
        Assert.Equal(AtsPlatform.SmartRecruiters, platform);
    }

    [Fact]
    public void Detect_PersonioUrl_Detecta()
    {
        var obs = new PageObservation("https://company.jobs.personio.de/123", "", "", []);
        var platform = _detector.Detect(obs);
        Assert.Equal(AtsPlatform.Personio, platform);
    }
}

public class FormStateMachineTests
{
    private readonly FormStateMachine _fsm = new();
    private readonly AtsDetector _detector = new();

    [Fact]
    public void Transition_PrimeiraObservacao_JobPage()
    {
        var obs = new PageObservation("https://linkedin.com/jobs/view/123", "Job Title", "Apply now", []);
        _fsm.Transition(obs, null);

        Assert.Equal(FormPhase.JobPage, _fsm.CurrentPhase);
        Assert.False(_fsm.IsStuck);
    }

    [Fact]
    public void Transition_BotaoApply_AplicaModal()
    {
        var obs = new PageObservation("https://linkedin.com/jobs/123", "Job", "", [
            new ElementCandidate("btn1", "button", "submit", "", "Apply", "Apply", "", false, false, false, "",
                [], new ElementBounds(0, 0, 0, 0)),
            new ElementCandidate("inp1", "input", "text", "", "Nome", "", "", true, false, false, "",
                [], new ElementBounds(0, 0, 0, 0))
        ], HasModal: true);
        _fsm.Transition(obs, new BrowserAction("click", "btn1", "", "Apply", "", 0.9m));

        Assert.Equal(FormPhase.ApplyModal, _fsm.CurrentPhase);
    }

    [Fact]
    public void Transition_CampoInput_FormStep()
    {
        var obs = new PageObservation("https://greenhouse.io/apply", "Apply", "", [
            new ElementCandidate("inp1", "input", "text", "", "Nome", "", "", true, false, false, "",
                [], new ElementBounds(0, 0, 0, 0))
        ]);
        _fsm.Transition(obs, new BrowserAction("fill", "inp1", "Nicolas", "Nome", "", 0.9m));

        Assert.Equal(FormPhase.FormStep, _fsm.CurrentPhase);
    }

    [Fact]
    public void Transition_Step1De2_FormStep()
    {
        var obs = new PageObservation("https://boards.greenhouse.io/company/jobs/123", "Job", "Step 1 of 3", [
            new ElementCandidate("inp1", "input", "text", "", "Nome", "", "", true, false, false, "",
                [], new ElementBounds(0, 0, 0, 0))
        ]);
        _fsm.Transition(obs, null);

        Assert.Equal(FormPhase.FormStep, _fsm.CurrentPhase);
        Assert.Equal(1, _fsm.CurrentStep);
    }

    [Fact]
    public void Transition_Step3De3_ReviewStep()
    {
        var obs1 = new PageObservation("https://boards.greenhouse.io/company/jobs/123", "Job", "Step 3 of 3", [
            new ElementCandidate("inp1", "input", "text", "", "Nome", "", "", true, false, false, "",
                [], new ElementBounds(0, 0, 0, 0)),
            new ElementCandidate("btn1", "button", "submit", "", "Review", "Review", "", false, false, false, "",
                [], new ElementBounds(0, 0, 0, 0))
        ]);
        _fsm.Transition(obs1, null);

        Assert.Equal(FormPhase.ReviewStep, _fsm.CurrentPhase);
    }

    [Fact]
    public void Transition_Confirmation_Continue()
    {
        var obs = new PageObservation("https://boards.greenhouse.io/company/jobs/123", "Job", "Application submitted!", [
            new ElementCandidate("btn1", "button", "submit", "", "Continue", "Continue", "", false, false, false, "",
                [], new ElementBounds(0, 0, 0, 0))
        ]);
        _fsm.Transition(obs, null);

        Assert.Equal(FormPhase.Confirmation, _fsm.CurrentPhase);
    }

    [Fact]
    public void Transition_MesmaFingerprint_4x_Stuck()
    {
        var obs = new PageObservation("https://example.com/form", "Form", "same text", [
            new ElementCandidate("inp1", "input", "text", "", "Nome", "", "same", true, false, false, "",
                [], new ElementBounds(0, 0, 0, 0))
        ]);

        _fsm.Transition(obs, null);
        _fsm.Transition(obs, null);
        _fsm.Transition(obs, null);
        _fsm.Transition(obs, null);

        Assert.True(_fsm.IsStuck);
    }

    [Fact]
    public void Transition_FingerprintDiferente_NaoStuck()
    {
        var obs1 = new PageObservation("https://example.com/form", "Form", "text", [
            new ElementCandidate("inp1", "input", "text", "", "Nome", "", "", true, false, false, "",
                [], new ElementBounds(0, 0, 0, 0))
        ]);
        var obs2 = new PageObservation("https://example.com/form", "Form", "other", [
            new ElementCandidate("inp1", "input", "text", "", "Nome", "", "", true, false, false, "",
                [], new ElementBounds(0, 0, 0, 0))
        ]);

        _fsm.Transition(obs1, null);
        _fsm.Transition(obs2, null);
        _fsm.Transition(obs2, null);

        Assert.False(_fsm.IsStuck);
    }

    [Fact]
    public void Transition_Progresso_StepIncrementa()
    {
        var obs1 = new PageObservation("https://example.com/form", "Form", "Step 1 of 3", [
            new ElementCandidate("inp1", "input", "text", "", "Nome", "", "", true, false, false, "",
                [], new ElementBounds(0, 0, 0, 0))
        ]);
        var obs2 = new PageObservation("https://example.com/form", "Form", "Step 2 of 3", [
            new ElementCandidate("inp2", "input", "text", "", "Email", "", "", true, false, false, "",
                [], new ElementBounds(0, 0, 0, 0))
        ]);

        _fsm.Transition(obs1, null);
        _fsm.Transition(obs2, null);

        Assert.Equal(2, _fsm.CurrentStep);
        Assert.True(_fsm.IsProgressing);
    }

    [Fact]
    public void DetectedAts_Gupy_Detecta()
    {
        var obs = new PageObservation("https://company.gupy.io/jobs/123", "Vaga", "", []);
        _fsm.Transition(obs, null);

        Assert.Equal(AtsPlatform.Gupy, _fsm.DetectedAts);
    }

    [Fact]
    public void IsInForm_FormStep_True()
    {
        var obs = new PageObservation("https://example.com/form", "Form", "", [
            new ElementCandidate("inp1", "input", "text", "", "Nome", "", "", true, false, false, "",
                [], new ElementBounds(0, 0, 0, 0))
        ]);
        _fsm.Transition(obs, new BrowserAction("fill", "inp1", "Nicolas", "Nome", "", 0.9m));

        Assert.True(_fsm.IsInForm);
    }

    [Fact]
    public void IsInForm_JobPage_False()
    {
        var obs = new PageObservation("https://linkedin.com/jobs/view/123", "Job", "Apply now", []);
        _fsm.Transition(obs, null);

        Assert.False(_fsm.IsInForm);
    }
}

