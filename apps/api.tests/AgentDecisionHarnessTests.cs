using System.Text.Json;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;

namespace JobPilot.Api.Tests;

public class AgentDecisionHarnessTests
{
    [Fact]
    public void ValidateBrowserActionPlan_BloqueiaReviewComComboboxObrigatorioNoPlaceholder()
    {
        var observationJson = JsonSerializer.Serialize(new
        {
            elements = new object[]
            {
                new
                {
                    elementId = "combo-1",
                    tag = "button",
                    type = "",
                    role = "combobox",
                    label = "Voce possui conhecimento em Java?",
                    text = "Selecionar opcao",
                    value = "Selecionar opcao",
                    required = true,
                    disabled = false,
                    @checked = false
                },
                new
                {
                    elementId = "review",
                    tag = "button",
                    type = "",
                    role = "",
                    label = "Revisar",
                    text = "Revisar",
                    value = "",
                    required = false,
                    disabled = false,
                    @checked = false
                }
            }
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var plan = new BrowserActionPlanResponse("click", "review", "", "Review application.", 0.9m, false);

        var result = AgentDecisionHarness.ValidateBrowserActionPlan(plan, observationJson);

        Assert.Equal("checkpoint", result.Plan.ActionType);
        Assert.Equal("llm.blocked_by_required_fields", result.EventType);
        Assert.Contains("empty select", result.Plan.Reason);
    }

    [Fact]
    public void ValidateBrowserActionPlan_RemoveResumePathInventadoPelaLlm()
    {
        var observationJson = JsonSerializer.Serialize(new
        {
            elements = new[]
            {
                new
                {
                    elementId = "resume",
                    tag = "input",
                    type = "file",
                    role = "",
                    label = "Curriculo",
                    text = "",
                    value = "",
                    required = false,
                    disabled = false,
                    @checked = false
                }
            }
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var plan = new BrowserActionPlanResponse("upload_file", "resume", "C:\\fake\\cv.pdf", "Upload resume.", 0.9m, false);

        var result = AgentDecisionHarness.ValidateBrowserActionPlan(plan, observationJson);

        Assert.Equal("upload_file", result.Plan.ActionType);
        Assert.Equal("", result.Plan.Value);
    }

    [Fact]
    public void ValidateBrowserActionPlan_BloqueiaUploadEmBotao()
    {
        var observationJson = JsonSerializer.Serialize(new
        {
            elements = new[]
            {
                new
                {
                    elementId = "upload-button",
                    tag = "button",
                    type = "",
                    role = "",
                    label = "Carregue o curriculo",
                    text = "Carregue o curriculo",
                    value = "",
                    required = false,
                    disabled = false,
                    @checked = false
                }
            }
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var plan = new BrowserActionPlanResponse("upload_file", "upload-button", "", "Upload resume.", 0.9m, false);

        var result = AgentDecisionHarness.ValidateBrowserActionPlan(plan, observationJson);

        Assert.Equal("checkpoint", result.Plan.ActionType);
        Assert.Equal("llm.invalid_target_type", result.EventType);
    }

    [Fact]
    public void ValidateBrowserActionPlan_BloqueiaReviewComErroVisivel()
    {
        var observationJson = JsonSerializer.Serialize(new
        {
            visibleText = "Insira uma resposta valida",
            elements = new[]
            {
                new
                {
                    elementId = "review",
                    tag = "button",
                    type = "",
                    role = "",
                    label = "Revisar",
                    text = "Revisar",
                    value = "",
                    required = false,
                    disabled = false,
                    @checked = false
                }
            }
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var plan = new BrowserActionPlanResponse("click", "review", "", "Review application.", 0.9m, false);

        var result = AgentDecisionHarness.ValidateBrowserActionPlan(plan, observationJson);

        Assert.Equal("checkpoint", result.Plan.ActionType);
        Assert.Equal("llm.blocked_by_required_fields", result.EventType);
        Assert.Contains("visible validation", result.Plan.Reason);
    }

    [Fact]
    public void ValidateBrowserActionPlan_NaoBloqueiaDropdownDePaisComoErroVisivel()
    {
        var observationJson = JsonSerializer.Serialize(new
        {
            visibleText = "Codigo do pais Selecionar opcao Brasil (+55)",
            elements = new[]
            {
                new
                {
                    elementId = "country",
                    tag = "button",
                    type = "",
                    role = "combobox",
                    label = "Codigo do pais",
                    text = "Brasil (+55)",
                    value = "Brasil (+55)",
                    required = true,
                    disabled = false,
                    @checked = false
                },
                new
                {
                    elementId = "review",
                    tag = "button",
                    type = "",
                    role = "",
                    label = "Revisar",
                    text = "Revisar",
                    value = "",
                    required = false,
                    disabled = false,
                    @checked = false
                }
            }
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var plan = new BrowserActionPlanResponse("click", "review", "", "Review application.", 0.9m, false);

        var result = AgentDecisionHarness.ValidateBrowserActionPlan(plan, observationJson);

        Assert.Equal("click", result.Plan.ActionType);
        Assert.Null(result.EventType);
    }

    [Fact]
    public void ValidateBrowserActionPlan_NaoBloqueiaReviewComCurriculoJaSelecionado()
    {
        var observationJson = JsonSerializer.Serialize(new
        {
            visibleText = "Curriculo Nicolas Portie.pdf Desmarcar selecao",
            elements = new object[]
            {
                new
                {
                    elementId = "resume",
                    tag = "input",
                    type = "file",
                    role = "",
                    label = "Curriculo",
                    text = "",
                    value = "",
                    required = true,
                    disabled = false,
                    @checked = false
                },
                new
                {
                    elementId = "review",
                    tag = "button",
                    type = "",
                    role = "",
                    label = "Revisar",
                    text = "Revisar",
                    value = "",
                    required = false,
                    disabled = false,
                    @checked = false
                }
            }
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var plan = new BrowserActionPlanResponse("click", "review", "", "Review application.", 0.9m, false);

        var result = AgentDecisionHarness.ValidateBrowserActionPlan(plan, observationJson);

        Assert.Equal("click", result.Plan.ActionType);
        Assert.Null(result.EventType);
    }

    [Fact]
    public void ValidateBrowserActionPlan_BloqueiaActionTypeDesconhecido()
    {
        var observationJson = JsonSerializer.Serialize(new { elements = Array.Empty<object>() }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var plan = new BrowserActionPlanResponse("double_click_everything", "", "", "Do magic.", 0.9m, false);

        var result = AgentDecisionHarness.ValidateBrowserActionPlan(plan, observationJson);

        Assert.Equal("checkpoint", result.Plan.ActionType);
        Assert.Equal("llm.invalid_action_type", result.EventType);
    }
}
