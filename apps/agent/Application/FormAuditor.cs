using JobPilot.Agent.Domain;

namespace JobPilot.Agent.Application;

public sealed class FormAuditor
{
    private readonly PageStateClassifier _pageStateClassifier;
    private readonly FieldTypeClassifier _fieldTypeClassifier = new();
    private readonly FieldValuePolicy _valuePolicy;

    public FormAuditor(PageStateClassifier pageStateClassifier, FieldValuePolicy valuePolicy)
    {
        _pageStateClassifier = pageStateClassifier;
        _valuePolicy = valuePolicy;
    }

    public FormAuditResult Audit(PageObservation observation, AgentTaskResponse task)
    {
        var issues = new List<FieldIssue>();
        var fields = observation.Elements.Where(e => e.IsField && !e.IsDisabledState).ToList();
        var buttons = observation.Elements.Where(e => e.IsButtonLike && !e.IsDisabledState).ToList();

        foreach (var field in fields)
        {
            var type = _fieldTypeClassifier.Infer(field);

            if (field.IsChoice && field.Required && !field.Checked)
            {
                issues.Add(new FieldIssue(field, ReasonCodes.MissingRequiredField, type, "Required radio/checkbox question is unanswered."));
                continue;
            }

            if (field.IsSelect && FieldValuePolicy.IsEmptySelectValue(field))
            {
                issues.Add(new FieldIssue(field, ReasonCodes.MissingRequiredField, type, "Select is empty or still on placeholder."));
                continue;
            }

            var correction = _valuePolicy.ResolveExistingCorrection(field, task.Profile);
            if (correction is not null)
            {
                issues.Add(new FieldIssue(field, ReasonCodes.InvalidExistingValue, correction.InferredType, correction.Reason));
                continue;
            }

            if (field.IsTextEntry && !field.IsFile && field.Required && string.IsNullOrWhiteSpace(field.Value))
            {
                issues.Add(new FieldIssue(field, ReasonCodes.MissingRequiredField, type, "Text entry field is empty."));
                continue;
            }

            if (field.IsFile && (field.Required || AgentText.ContainsAny(field.SearchTextWithHints, ["curriculo", "currículo", "resume", "cv"])))
            {
                issues.Add(new FieldIssue(field, ReasonCodes.MissingRequiredField, type, "File field is available for resume upload."));
            }
        }

        return new FormAuditResult(_pageStateClassifier.Classify(observation), issues, fields, buttons);
    }
}
