namespace JobPilot.Api.Domain;

public static class JobStatuses
{
    public const string Found = "found";
    public const string Scored = "scored";
    public const string Shortlisted = "shortlisted";
    public const string QueuedToApply = "queued_to_apply";
    public const string Opening = "opening";
    public const string Filling = "filling";
    public const string NeedsReview = "needs_review";
    public const string ReadyForSubmit = "ready_for_submit";
    public const string Applied = "applied";
    public const string Failed = "failed";
    public const string Ignored = "ignored";
    public const string ManualOnly = "manual_only";
}
