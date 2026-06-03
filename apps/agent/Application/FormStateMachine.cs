using System.Text.RegularExpressions;
using JobPilot.Agent.Domain;

namespace JobPilot.Agent.Application;

/// <summary>
/// Máquina de estados que rastreia o progresso do agente em formulários multi-step.
/// Detecta: avanço, retrocesso, stuck, confirmação, bloqueio.
/// </summary>
public sealed class FormStateMachine
{
    private readonly AtsDetector _atsDetector = new();
    private readonly HashSet<string> _visitedFingerprints = new();
    private string _lastFingerprint = "";
    private int _streakCount = 0;

    public FormPhase CurrentPhase { get; private set; } = FormPhase.JobPage;
    public int CurrentStep { get; private set; } = 0;
    public int TotalSteps { get; private set; } = 0;
    public AtsPlatform DetectedAts { get; private set; } = AtsPlatform.Unknown;
    public bool IsFirstObservation { get; private set; } = true;

    /// <summary>
    /// Transiciona o estado baseado na observação atual e ação executada.
    /// </summary>
    public void Transition(PageObservation observation, BrowserAction? action)
    {
        DetectedAts = _atsDetector.Detect(observation);
        var newPhase = InferPhase(observation);
        var fingerprint = observation.Fingerprint();

        // Detecta step number
        var step = DetectStepNumber(observation);
        if (step.HasValue)
        {
            if (step.Value > CurrentStep)
            {
                CurrentStep = step.Value;
                _streakCount = 0; // Resetou stuck
            }
            else if (step.Value < CurrentStep && CurrentStep > 0)
            {
                // Retrocesso = possível erro de validação no step anterior
                CurrentStep = step.Value;
            }
        }

        // Detecta stuck
        if (fingerprint == _lastFingerprint)
        {
            _streakCount++;
        }
        else
        {
            _streakCount = 0;
            _lastFingerprint = fingerprint;
        }

        _visitedFingerprints.Add(fingerprint);
        CurrentPhase = newPhase;
        IsFirstObservation = false;
    }

    /// <summary>
    /// O agente ficou preso na mesma tela por múltiplos ciclos?
    /// </summary>
    public bool IsStuck => _streakCount >= 3;

    /// <summary>
    /// Estamos avançando no formulário?
    /// </summary>
    public bool IsProgressing => _streakCount == 0 && CurrentStep > 0;

    /// <summary>
    /// Estamos em um formulário ativo (não na página da vaga nem confirmado)?
    /// </summary>
    public bool IsInForm => CurrentPhase is FormPhase.FormStep or FormPhase.ReviewStep or FormPhase.ApplyModal;

    private FormPhase InferPhase(PageObservation observation)
    {
        if (DecisionEngine.IsBlockedState(observation)) return FormPhase.Blocked;
        if (DecisionEngine.IsConfirmation(observation)) return FormPhase.Confirmation;

        var url = observation.Url.ToLowerInvariant();

        // Página da vaga no LinkedIn
        if (url.Contains("/jobs/view/", StringComparison.OrdinalIgnoreCase))
        {
            if (observation.Elements.Any(e => e.IsButtonLike && e.SearchText.Contains("apply", StringComparison.OrdinalIgnoreCase)))
                return FormPhase.JobPage;
        }

        // Modal de aplicação
        if (url.Contains("linkedin.com/jobs") && observation.HasModal)
        {
            if (observation.Elements.Count(e => e.IsField) > 0 || observation.Elements.Any(e => e.IsButtonLike))
                return FormPhase.ApplyModal;
        }

        // Página externa de aplicação
        if (!url.Contains("linkedin.com"))
        {
            if (observation.Elements.Any(e => e.IsButtonLike && LooksLikeReview(e.SearchText)))
                return FormPhase.ReviewStep;

            if (observation.Elements.Any(e => e.IsField))
                return FormPhase.FormStep;

            if (observation.Elements.Any(e => e.IsButtonLike && LooksLikeSubmitSafe(e.SearchText)))
                return FormPhase.FormStep;
        }

        return CurrentPhase;
    }

    private static int? DetectStepNumber(PageObservation observation)
    {
        var text = observation.VisibleText;

        // Pattern: "Step 2 of 5", "Etapa 2 de 5", "2/5"
        var match = Regex.Match(text, @"(?:step|etapa|p[áa]gina|page)\s*(\d+)\s*(?:of|de|\/|sur)\s*(\d+)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var current = int.Parse(match.Groups[1].Value);
            var total = int.Parse(match.Groups[2].Value);
            return current;
        }

        // Pattern: "2 de 5"
        var match2 = Regex.Match(text, @"(\d+)\s*de\s*(\d+)", RegexOptions.IgnoreCase);
        if (match2.Success)
        {
            var current = int.Parse(match2.Groups[1].Value);
            var total = int.Parse(match2.Groups[2].Value);
            if (total >= 2 && total <= 20)
                return current;
        }

        // Progress bars / breadcrumbs
        var progressElements = observation.Elements.Where(e =>
            e.Role == "progressbar" ||
            e.Label.Contains("active", StringComparison.OrdinalIgnoreCase) ||
            e.Label.Contains("current", StringComparison.OrdinalIgnoreCase) ||
            e.Label.Contains("passo", StringComparison.OrdinalIgnoreCase));

        if (progressElements.Any())
        {
            var allSteps = observation.Elements.Where(e =>
                e.Role == "progressbar" ||
                e.Label.Contains("step", StringComparison.OrdinalIgnoreCase) ||
                e.Label.Contains("etapa", StringComparison.OrdinalIgnoreCase)).ToList();

            if (allSteps.Count >= 2)
            {
                var activeIndex = allSteps.FindIndex(e =>
                    e.Label.Contains("active", StringComparison.OrdinalIgnoreCase) ||
                    e.Label.Contains("current", StringComparison.OrdinalIgnoreCase));
                if (activeIndex >= 0)
                    return activeIndex + 1;
            }
        }

        return null;
    }

    private static bool LooksLikeReview(string text) =>
        AgentText.ContainsAny(text, ["review", "revisar", "revisão", "revisao"]);

    private static bool LooksLikeSubmitSafe(string text) =>
        AgentText.ContainsAny(text, ["submit application", "enviar candidatura", "send application", "finalizar candidatura", "submit"]);
}

public enum FormPhase
{
    JobPage,          // Página da vaga, procurando botão Apply
    ApplyModal,       // Modal de aplicação (LinkedIn Easy Apply)
    FormStep,         // Preenchendo formulário (step N)
    ReviewStep,       // Tela de revisão antes de submit
    Confirmation,     // Confirmação de envio
    ExternalRedirect, // Redirecionado para site externo
    Blocked           // Captcha, login, etc.
}
