namespace JobPilot.Api.Application.Contracts;

public record ResumeSectionDiff(string SectionType, string Title, string Status, string BeforePreview, string AfterPreview, Guid? AfterSectionId = null);
