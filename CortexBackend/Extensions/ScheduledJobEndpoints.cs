using Cortex.API.Handlers;

namespace Cortex.API.Extensions;

public static class ScheduledJobEndpoints
{
    public static void MapScheduledJobEndpoints(this WebApplication app)
    {
        var jobs = app.MapGroup("/api/jobs")
            .RequireAuthorization("SlaManage")
            .WithTags("Jobs");

        jobs.MapGet("/", ScheduledJobHandlers.GetScheduledJobs)
            .WithName("GetScheduledJobs")
            .Produces(StatusCodes.Status200OK);

        jobs.MapPost("/", ScheduledJobHandlers.CreateScheduledJob)
            .WithName("CreateScheduledJob")
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        jobs.MapPut("/{id:int}", ScheduledJobHandlers.UpdateScheduledJob)
            .WithName("UpdateScheduledJob")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        jobs.MapPost("/{id:int}/run", ScheduledJobHandlers.RunScheduledJobNow)
            .WithName("RunScheduledJobNow")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);
    }
}
