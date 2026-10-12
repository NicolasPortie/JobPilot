namespace JobPilot.Api.Application.Contracts;

public record CreateResumeExportRequest(string Format = "pdf", string Layout = "ats_standard");
