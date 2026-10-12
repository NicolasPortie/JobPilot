using JobPilot.Api.Domain;

namespace JobPilot.Api.Application;

public sealed record ProfileValidationResponse(bool Valid, bool Critical, IReadOnlyList<ProfileValidationIssue> Issues);
