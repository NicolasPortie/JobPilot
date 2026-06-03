using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Endpoints;

public static class ExtensionEndpoints
{
    public static RouteGroupBuilder MapExtensionEndpoints(this RouteGroupBuilder api, RouteGroupBuilder authed)
    {
        authed.MapGet("/extension/status", async (JobPilotDbContext db, ClaimsPrincipal principal) =>
        {
            var userId = principal.AgentUserId();
            var liveCutoff = DateTimeOffset.UtcNow.AddSeconds(-90);
            var session = await db.BrowserExtensionSessions
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.LastHeartbeatAt != null && x.LastHeartbeatAt >= liveCutoff)
                .ThenByDescending(x => x.LastHeartbeatAt ?? x.CreatedAt)
                .FirstOrDefaultAsync();

            if (session is null)
                return Results.Ok(new ExtensionStatusResponse(null, "not_paired", "", false, "", "", "", null, null));

            var heartbeatAge = session.LastHeartbeatAt is null
                ? TimeSpan.MaxValue
                : DateTimeOffset.UtcNow - session.LastHeartbeatAt.Value;
            var status = heartbeatAge <= TimeSpan.FromSeconds(90) ? "connected" : "stale";

            return Results.Ok(ToStatus(session, status));
        });

        authed.MapPost("/extension/pair", async (PairExtensionRequest request, JobPilotDbContext db, ClaimsPrincipal principal) =>
        {
            var rawToken = GenerateToken();
            var session = new BrowserExtensionSession
            {
                Id = Guid.NewGuid(),
                UserId = principal.UserId(),
                Name = string.IsNullOrWhiteSpace(request.Name) ? "Chrome extension" : request.Name.Trim(),
                TokenHash = HashToken(rawToken),
                Status = "paired",
                Browser = "chrome",
                ExtensionVersion = "0.1.0"
            };

            db.BrowserExtensionSessions.Add(session);
            await db.SaveChangesAsync();

            return Results.Ok(new PairExtensionResponse(session.Id, rawToken));
        });

        api.MapPost("/extension/local-pair", async (LocalExtensionPairRequest request, JobPilotDbContext db) =>
        {
            var user = await db.Users.OrderBy(x => x.CreatedAt).FirstOrDefaultAsync();
            if (user is null)
                return Results.NotFound(new { message = "No local JobPilot user found." });

            var rawToken = GenerateToken();
            var installationId = string.IsNullOrWhiteSpace(request.InstallationId) ? "" : request.InstallationId.Trim();
            var session = string.IsNullOrWhiteSpace(installationId)
                ? null
                : await db.BrowserExtensionSessions
                    .Where(x => x.UserId == user.Id && x.InstallationId == installationId && x.Status != "revoked")
                    .OrderByDescending(x => x.CreatedAt)
                    .FirstOrDefaultAsync();

            if (session is null)
            {
                session = new BrowserExtensionSession
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    InstallationId = installationId
                };
                db.BrowserExtensionSessions.Add(session);
            }

            session.Name = string.IsNullOrWhiteSpace(request.Name) ? "Chrome extension" : request.Name.Trim();
            session.TokenHash = HashToken(rawToken);
            session.Status = "paired";
            session.Browser = string.IsNullOrWhiteSpace(request.Browser) ? "chrome" : request.Browser.Trim();
            session.ExtensionVersion = string.IsNullOrWhiteSpace(request.ExtensionVersion) ? "0.1.0" : request.ExtensionVersion.Trim();
            session.LastError = "";
            await db.SaveChangesAsync();
            return Results.Ok(new PairExtensionResponse(session.Id, rawToken));
        });

        api.MapPost("/extension/heartbeat", async (ExtensionHeartbeatRequest request, JobPilotDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(request.Token))
                return Results.Unauthorized();

            var tokenHash = HashToken(request.Token);
            var session = await db.BrowserExtensionSessions.FirstOrDefaultAsync(x => x.TokenHash == tokenHash);
            if (session is null || session.Status == "revoked")
                return Results.Unauthorized();

            session.Status = "connected";
            session.CurrentUrl = request.CurrentUrl ?? "";
            session.IsSupportedPage = request.IsSupportedPage;
            session.Browser = string.IsNullOrWhiteSpace(request.Browser) ? "chrome" : request.Browser.Trim();
            session.ExtensionVersion = string.IsNullOrWhiteSpace(request.ExtensionVersion) ? session.ExtensionVersion : request.ExtensionVersion.Trim();
            session.LastError = request.LastError ?? "";
            session.LastHeartbeatAt = DateTimeOffset.UtcNow;
            if (request.IsSupportedPage)
                session.LastObservedAt = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync();
            return Results.Ok(ToStatus(session, "connected"));
        });

        authed.MapPost("/extension/commands", async (QueueExtensionCommandRequest request, JobPilotDbContext db, ClaimsPrincipal principal) =>
        {
            var userId = principal.AgentUserId();
            var session = await db.BrowserExtensionSessions.FirstOrDefaultAsync(x => x.Id == request.SessionId && x.UserId == userId);
            if (session is null)
                return Results.NotFound(new { message = "Extension session not found." });

            var command = new BrowserExtensionCommandJob
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                BrowserExtensionSessionId = session.Id,
                Type = request.Type.Trim(),
                ElementId = request.ElementId ?? "",
                Value = request.Value ?? "",
                ReasonCode = request.ReasonCode ?? "",
                Status = "pending"
            };

            db.BrowserExtensionCommandJobs.Add(command);
            await db.SaveChangesAsync();
            return Results.Ok(ToCommand(command));
        });

        authed.MapGet("/extension/commands/{id:guid}", async (Guid id, JobPilotDbContext db, ClaimsPrincipal principal) =>
        {
            var userId = principal.AgentUserId();
            var command = await db.BrowserExtensionCommandJobs.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
            if (command is null)
                return Results.NotFound();

            return Results.Ok(new ExtensionCommandStatusResponse(command.Id, command.Status, command.ResultJson, command.Error, command.ClaimedAt, command.CompletedAt));
        });

        api.MapPost("/extension/commands/next", async (ExtensionCommandPollRequest request, JobPilotDbContext db) =>
        {
            var session = await ResolveSessionAsync(db, request.Token);
            if (session is null)
                return Results.Unauthorized();

            session.Status = "connected";
            session.CurrentUrl = request.CurrentUrl ?? "";
            session.IsSupportedPage = request.IsSupportedPage;
            session.Browser = string.IsNullOrWhiteSpace(request.Browser) ? session.Browser : request.Browser.Trim();
            session.ExtensionVersion = string.IsNullOrWhiteSpace(request.ExtensionVersion) ? session.ExtensionVersion : request.ExtensionVersion.Trim();
            session.LastHeartbeatAt = DateTimeOffset.UtcNow;

            var staleClaimCutoff = DateTimeOffset.UtcNow.AddSeconds(-45);
            var staleClaims = await db.BrowserExtensionCommandJobs
                .Where(x =>
                    x.BrowserExtensionSessionId == session.Id &&
                    x.Status == "claimed" &&
                    x.ClaimedAt != null &&
                    x.ClaimedAt < staleClaimCutoff)
                .ToListAsync();

            foreach (var stale in staleClaims)
            {
                stale.Status = "pending";
                stale.Error = "requeued_after_extension_timeout";
                stale.ClaimedAt = null;
            }

            var command = await db.BrowserExtensionCommandJobs
                .Where(x => x.BrowserExtensionSessionId == session.Id && x.Status == "pending")
                .OrderBy(x => x.CreatedAt)
                .FirstOrDefaultAsync();

            if (command is null)
            {
                await db.SaveChangesAsync();
                return Results.Ok(new { command = (ExtensionCommandResponse?)null });
            }

            command.Status = "claimed";
            command.ClaimedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { command = ToCommand(command) });
        });

        api.MapPost("/extension/commands/{id:guid}/result", async (Guid id, ExtensionCommandResultRequest request, JobPilotDbContext db) =>
        {
            var session = await ResolveSessionAsync(db, request.Token);
            if (session is null)
                return Results.Unauthorized();

            var command = await db.BrowserExtensionCommandJobs.FirstOrDefaultAsync(x => x.Id == id && x.BrowserExtensionSessionId == session.Id);
            if (command is null)
                return Results.NotFound();

            command.Status = request.Success ? "completed" : "failed";
            command.ResultJson = string.IsNullOrWhiteSpace(request.ResultJson) ? "{}" : request.ResultJson;
            command.Error = request.Error ?? "";
            command.CompletedAt = DateTimeOffset.UtcNow;
            session.LastHeartbeatAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { command.Id, command.Status });
        });

        return api;
    }

    private static ExtensionCommandResponse ToCommand(BrowserExtensionCommandJob command) =>
        new(command.Id, command.Type, command.ElementId, command.Value, command.ReasonCode);

    private static ExtensionStatusResponse ToStatus(BrowserExtensionSession session, string status) =>
        new(session.Id, status, session.CurrentUrl, session.IsSupportedPage, session.Browser, session.ExtensionVersion, session.LastError, session.LastHeartbeatAt, session.LastObservedAt);

    private static async Task<BrowserExtensionSession?> ResolveSessionAsync(JobPilotDbContext db, string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var tokenHash = HashToken(token);
        var session = await db.BrowserExtensionSessions.FirstOrDefaultAsync(x => x.TokenHash == tokenHash);
        return session is null || session.Status == "revoked" ? null : session;
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    private static string GenerateToken() =>
        $"jpx_{Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace("+", "-").Replace("/", "_").TrimEnd('=')}";
}
