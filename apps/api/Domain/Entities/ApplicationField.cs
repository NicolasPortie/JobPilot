namespace JobPilot.Api.Domain;

public class ApplicationField
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ApplicationJobId { get; set; }
    public string FieldKey { get; set; } = "";
    public string Label { get; set; } = "";
    public string FieldType { get; set; } = "";
    public string ValueSource { get; set; } = "";
    public decimal Confidence { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
