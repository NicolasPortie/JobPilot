namespace JobPilot.Api.Application.Contracts;

public record ProfileImportRequest(bool FetchPublicLinks = false, bool UseAi = true);
