using System.Security.Claims;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;

namespace JobPilot.Api.Endpoints;

public static class JobTargetEndpoints
{
    public static RouteGroupBuilder MapJobTargetEndpoints(this RouteGroupBuilder authed)
    {
        authed.MapGet("/job-targets", async (JobTargetService service, ClaimsPrincipal principal) =>
            Results.Ok(await service.ListAsync(principal.UserId())));

        authed.MapGet("/job-targets/{id:guid}", async (Guid id, JobTargetService service, ClaimsPrincipal principal) =>
        {
            var target = await service.GetAsync(principal.UserId(), id);
            return target is null ? Results.NotFound() : Results.Ok(target);
        });

        authed.MapPost("/job-targets/organize", async (OrganizeJobTargetRequest request, JobTargetService service, ClaimsPrincipal principal) =>
        {
            try
            {
                return Results.Ok(await service.OrganizeAsync(principal.UserId(), request));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        authed.MapPost("/job-targets/organize-screenshot", async (IFormFile file, bool? useAi, JobTargetSourceFileService files, JobTargetService service, ClaimsPrincipal principal) =>
        {
            try
            {
                var saved = await files.SaveAsync(principal.UserId(), file);
                return Results.Ok(await service.OrganizeAsync(principal.UserId(), new OrganizeJobTargetRequest(null, "image_ocr", null, null, saved.Path, useAi ?? true)));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).DisableAntiforgery();

        return authed;
    }
}
