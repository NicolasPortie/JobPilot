namespace JobPilot.Api.Application.Contracts;

public record ResumeOcrCommandStatus(string Command, bool Available, string Path);
