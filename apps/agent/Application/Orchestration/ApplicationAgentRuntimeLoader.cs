using JobPilot.Agent.Domain;
using JobPilot.Agent.Infrastructure;

namespace JobPilot.Agent.Application;

public sealed record ApplicationAgentRuntime(
    AgentPolicy? Policy,
    LlmDecisionEngine Decision,
    List<AgentLearning> ActiveLearnings,
    List<PlatformCredential> PlatformCredentials);

public sealed class ApplicationAgentRuntimeLoader(
    JobPilotApiClient client,
    LocalAnswerResolver resolver,
    LlmClient? llm)
{
    public async Task<ApplicationAgentRuntime?> LoadAsync()
    {
        var validation = await client.ValidateProfileAsync();
        if (validation is { Valid: false, Critical: true })
        {
            var detail = "Validacao do perfil falhou: " + string.Join("; ", validation.Issues.Select(issue => issue.ErrorMessage));
            Console.WriteLine(detail);
            await client.EventAsync(null, "run.aborted", detail);
            return null;
        }

        await resolver.LoadAsync();
        var policy = await client.GetPolicyAsync();
        var decision = new LlmDecisionEngine(
            llm ?? new LlmClient(client, false),
            resolver,
            policy ?? new AgentPolicy("conservative", false, 10, 50, [], []));
        var learnings = await client.GetActiveLearningsAsync();
        var credentials = await client.GetPlatformCredentialsAsync() ?? [];

        if (learnings.Count > 0)
            Console.WriteLine($"[Learning] Loaded {learnings.Count} approved learning(s).");

        foreach (var credential in credentials)
            Console.WriteLine($"[Credentials] {credential.Platform} credential loaded.");

        return new ApplicationAgentRuntime(policy, decision, learnings, credentials);
    }
}
