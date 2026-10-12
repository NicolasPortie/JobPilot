namespace JobPilot.Api.Application.Contracts;

public record JobRequirementExtractionInput(string SourceText, string SourceUrl = "", string Title = "", string Company = "", string Location = "", AiInputAttachment[]? Attachments = null);
