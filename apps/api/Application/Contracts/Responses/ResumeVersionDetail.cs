namespace JobPilot.Api.Application.Contracts;

public record ResumeVersionDetail(Guid Id, Guid ResumeDocumentId, string Title, string Status, string TargetRole, string TargetCompany, int AtsScore, string PlainText, ResumeSectionDto[] Sections);
