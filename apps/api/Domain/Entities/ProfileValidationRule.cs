namespace JobPilot.Api.Domain;

public class ProfileValidationRule
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string FieldName { get; set; } = "";
    public string ValidationType { get; set; } = "not_empty";
    public string ValidationValue { get; set; } = "";
    public string ErrorMessage { get; set; } = "";
    public bool IsCritical { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
