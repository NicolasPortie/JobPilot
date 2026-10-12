using System.Diagnostics;
using JobPilot.Api.Domain;

namespace JobPilot.Api.Services;

internal static class AutomationRuntimeLauncher
{
    public static string ResolveExecutionTarget(AgentPolicy? policy)
    {
        var value = Environment.GetEnvironmentVariable("JOBPILOT_EXECUTION_TARGET")
                    ?? Environment.GetEnvironmentVariable("JOBPILOT_AUTOMATION_BROWSER_DRIVER")
                    ?? Environment.GetEnvironmentVariable("JOBPILOT_BROWSER_DRIVER")
                    ?? policy?.ExecutionTarget
                    ?? ExecutionTargets.Extension;
        return ExecutionTargets.Normalize(value);
    }

    public static string ResolveBrowserDriver(string executionTarget) =>
        executionTarget == ExecutionTargets.LocalDedicatedBrowser ? "playwright" : "extension";

    public static string ResolveBrowserProfilePath(Guid userId)
    {
        var configured = Environment.GetEnvironmentVariable("JOBPILOT_BROWSER_PROFILE");
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var root = string.IsNullOrWhiteSpace(localAppData)
            ? ResolveWorkspaceRoot()
            : localAppData;

        return Path.Combine(root, "JobPilot", "browser-profiles", userId.ToString("N"));
    }

    public static AgentProcessInfo StartAgentProcess(string root, string arguments)
    {
        var logsDir = Path.Combine(root, "logs");
        Directory.CreateDirectory(logsDir);
        var logPath = Path.Combine(logsDir, $"agent-{DateTimeOffset.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.log");

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project {Quote(Path.Combine(root, "apps", "agent", "JobPilot.Agent.csproj"))} -- {arguments}",
            WorkingDirectory = root,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => AppendAgentLog(logPath, e.Data);
        process.ErrorDataReceived += (_, e) => AppendAgentLog(logPath, e.Data);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        AppendAgentLog(logPath, $"[start] dotnet {RedactToken(startInfo.Arguments)}");
        return new AgentProcessInfo(logPath, process.Id);
    }

    public static string ResolveWorkspaceRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "apps", "agent")))
                return current.FullName;
            current = current.Parent;
        }
        return Directory.GetCurrentDirectory();
    }

    private static void AppendAgentLog(string logPath, string? line)
    {
        if (line is null) return;
        try
        {
            File.AppendAllText(logPath, $"[{DateTimeOffset.Now:O}] {line}{Environment.NewLine}");
        }
        catch
        {
            // Logging must never prevent automation from starting.
        }
    }

    private static string RedactToken(string value) =>
        System.Text.RegularExpressions.Regex.Replace(value, @"--token\s+""[^""]+""", "--token \"[redacted]\"");

    public static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
}

