using System.Security.Claims;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Endpoints;

public static class AgentManagementEndpoints
{
    public static RouteGroupBuilder MapAgentManagementEndpoints(this RouteGroupBuilder authed)
    {
        authed.MapGet("/agent/policy", async (JobPilotDbContext db, ClaimsPrincipal principal) =>
        {
            var userId = principal.UserId();
            var policy = await db.AgentPolicies.FirstOrDefaultAsync(x => x.UserId == userId);
            if (policy is not null) return Results.Ok(policy);

            policy = new AgentPolicy { Id = Guid.NewGuid(), UserId = userId };
            db.AgentPolicies.Add(policy);
            await db.SaveChangesAsync();
            return Results.Ok(policy);
        });

        authed.MapPut("/agent/policy", async (AgentPolicy input, JobPilotDbContext db, ClaimsPrincipal principal) =>
        {
            var userId = principal.UserId();
            var policy = await db.AgentPolicies.FirstOrDefaultAsync(x => x.UserId == userId);
            if (policy is null)
            {
                policy = input;
                policy.Id = Guid.NewGuid();
                policy.UserId = userId;
                db.AgentPolicies.Add(policy);
            }
            else
            {
                policy.Apply(input);
            }
            await db.SaveChangesAsync();
            return Results.Ok(policy);
        });

        authed.MapGet("/agent/history", async (JobPilotDbContext db, ClaimsPrincipal principal) =>
        {
            var userId = principal.UserId();
            return Results.Ok(new
            {
                runs = await db.AgentRuns.Where(x => x.UserId == userId).OrderByDescending(x => x.StartedAt).Take(20).ToListAsync(),
                steps = await db.AgentSteps.Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAt).Take(40).ToListAsync(),
                fields = await db.ApplicationFields.Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAt).Take(40).ToListAsync(),
                answers = await db.ApplicationAnswers.Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAt).Take(40).ToListAsync(),
                memories = await db.SiteWorkflowMemories.Where(x => x.UserId == userId).OrderByDescending(x => x.UpdatedAt).Take(40).ToListAsync()
            });
        });

        authed.MapPost("/ai/answer-draft", async (AnswerDraftRequest request, IAiChatService ai, JobPilotDbContext db, ClaimsPrincipal principal) =>
        {
            var profile = await db.CandidateProfiles.FirstOrDefaultAsync(x => x.UserId == principal.UserId());
            return Results.Ok(await ai.DraftAnswerAsync(principal.UserId(), request.QuestionText, request.JobContext, profile));
        });

        authed.MapPost("/agent/pair", async (PairDeviceRequest request, JobPilotDbContext db, JwtTokenService tokens, ClaimsPrincipal principal) =>
        {
            var device = new AgentDevice
            {
                Id = Guid.NewGuid(),
                UserId = principal.UserId(),
                Name = string.IsNullOrWhiteSpace(request.Name) ? Environment.MachineName : request.Name,
                Status = "active"
            };
            db.AgentDevices.Add(device);
            await db.SaveChangesAsync();
            return Results.Ok(new PairDeviceResponse(device.Id, tokens.CreateToken(device.Id, device.Name, "agent", principal.UserId())));
        });

        return authed;
    }
}
