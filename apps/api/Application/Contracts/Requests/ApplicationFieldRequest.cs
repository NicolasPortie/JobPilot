namespace JobPilot.Api.Application.Contracts;

public record ApplicationFieldRequest(Guid ApplicationJobId, string FieldKey, string Label, string FieldType, string ValueSource, decimal Confidence);
