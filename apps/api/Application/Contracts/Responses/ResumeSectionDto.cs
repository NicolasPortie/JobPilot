namespace JobPilot.Api.Application.Contracts;

public record ResumeSectionDto(Guid Id, string SectionType, string Title, int SortOrder, string ContentJson);
