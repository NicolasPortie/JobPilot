using JobPilot.Api.Domain;

namespace JobPilot.Api.Application;

public sealed record DiscoveredJobsResult(int Saved, int Ignored, int Reviewed);

public sealed record AutoQueueResult(
    int Queued,
    int Total,
    bool Paused = false,
    bool DailyLimitReached = false,
    int? DailyLimit = null,
    int? AppliedToday = null,
    int? RemainingToday = null);
