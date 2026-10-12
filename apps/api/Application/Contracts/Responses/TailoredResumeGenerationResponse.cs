namespace JobPilot.Api.Application.Contracts;

public record TailoredResumeGenerationResponse(string PlainText, string[] Warnings);
