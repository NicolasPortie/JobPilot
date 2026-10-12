namespace JobPilot.Api.Application.Contracts;

public record AiInputAttachment(string Type, string MediaType, string DataUrl = "", string FileName = "", string FileId = "");
