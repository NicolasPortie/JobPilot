namespace JobPilot.Agent.Harness.Observation;

public sealed record ActiveSurfaceDefinition(
    string? ModalOutletSelector,
    string[] ModalSelectors,
    string[] DialogSelectors,
    string[] ModalScrollSelectors,
    string[] FieldContextSelectors)
{
    public static ActiveSurfaceDefinition Generic { get; } = new(
        null,
        [],
        ["[role=\"dialog\"]", "[role=\"alertdialog\"]", ".modal", ".modal-dialog", ".overlay__content"],
        [],
        ["[role=\"group\"]", "fieldset", "form"]);
}
