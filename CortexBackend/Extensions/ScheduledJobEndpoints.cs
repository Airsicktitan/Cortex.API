using Cortex.API.Authorization;
using Cortex.API.Handlers;

namespace Cortex.API.Extensions;

public static class ScheduledJobEndpoints
{
    public static void MapScheduledJobEndpoints(this WebApplication app)
    {
        var jobs = app.MapGroup("/api/jobs")
            .RequireAuthorization()
            .WithTags("Jobs");

        jobs.MapGet("/", ScheduledJobHandlers.GetScheduledJobs)
            .RequireAuthorization(CortexAuthorizationExtensions.BusinessAccess)
            .WithName("GetScheduledJobs")
            .Produces(StatusCodes.Status200OK);

        jobs.MapPost("/", ScheduledJobHandlers.CreateScheduledJob)
            .RequireAuthorization(CortexAuthorizationExtensions.ElevatedAccess)
            .WithName("CreateScheduledJob")
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        jobs.MapPut("/{id:int}", ScheduledJobHandlers.UpdateScheduledJob)
            .RequireAuthorization(CortexAuthorizationExtensions.ElevatedAccess)
            .WithName("UpdateScheduledJob")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        jobs.MapPost("/{id:int}/run", ScheduledJobHandlers.RunScheduledJobNow)
            .RequireAuthorization(CortexAuthorizationExtensions.ElevatedAccess)
            .WithName("RunScheduledJobNow")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);
    }
}
