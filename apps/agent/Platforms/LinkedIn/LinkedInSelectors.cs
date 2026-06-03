namespace JobPilot.Agent.Platforms.LinkedIn;

public static class LinkedInSelectors
{
    public const string JobDetailPanel =
        ".jobs-details__main-content, .jobs-unified-top-card, [class*='job-details-jobs-unified-top-card']";

    public const string EasyApplyButton =
        "button:visible:not([role='radio']):not([id^='searchFilter']):has-text('Candidatura simplificada'), button:visible:not([role='radio']):not([id^='searchFilter']):has-text('Easy Apply')";

    public const string ExternalApplyButton =
        "button:visible:not([role='radio']):not([id^='searchFilter']):has-text('Candidatar-se'), button:visible:not([role='radio']):not([id^='searchFilter']):has-text('Aplicar'), button:visible:not([role='radio']):not([id^='searchFilter']):has-text('Apply')";

    public static readonly string[] EasyApplyModal =
    [
        "#artdeco-modal-outlet [class*='jobs-easy-apply-content']",
        "#artdeco-modal-outlet .jobs-easy-apply-form",
        "#artdeco-modal-outlet [role='dialog']",
        "div[role='dialog']:has-text('Candidatura')",
        "div[role='dialog']:has-text('Easy Apply')",
        "div[role='dialog']:has-text('Informações de contato')",
        "div[role='dialog']:has-text('Contact info')"
    ];
}
