namespace JobPilot.Api.Domain;

public class QuestionPattern
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string PatternKey { get; set; } = "";
    public string Category { get; set; } = "general";
    public string[] Tags { get; set; } = [];
    public string[] QuestionTemplates { get; set; } = [];
    public string AnswerTemplate { get; set; } = "";
    public string ValueSource { get; set; } = "profile_field";
    public string ProfileFieldMapping { get; set; } = "";
    public decimal ConfidenceThreshold { get; set; } = 0.75m;
    public int UseCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public void Apply(QuestionPattern input)
    {
        PatternKey = input.PatternKey;
        Category = input.Category;
        Tags = input.Tags;
        QuestionTemplates = input.QuestionTemplates;
        AnswerTemplate = input.AnswerTemplate;
        ValueSource = input.ValueSource;
        ProfileFieldMapping = input.ProfileFieldMapping;
        ConfidenceThreshold = input.ConfidenceThreshold;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
