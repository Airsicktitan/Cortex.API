using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services;

namespace Cortex.API.Handlers;

public static class ScheduledJobHandlers
{
    public static async Task<IResult> GetScheduledJobs(
        IScheduledJobService service)
    {
        var jobs = await service.GetAllAsync();
        return Results.Ok(jobs.Select(job => job.ToResponse()));
    }

    public static async Task<IResult> CreateScheduledJob(
        UpsertScheduledJobRequest request,
        IScheduledJobService service,
        IUserContextService userContextService)
    {
        try
        {
            var currentUser = await userContextService.GetCurrentUserAsync();
            var job = await service.CreateAsync(ToModel(request), currentUser.Id);
            return Results.Created($"/api/jobs/{job.Id}", job.ToResponse());
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }

    public static async Task<IResult> UpdateScheduledJob(
        int id,
        UpsertScheduledJobRequest request,
        IScheduledJobService service,
        IUserContextService userContextService)
    {
        try
        {
            var currentUser = await userContextService.GetCurrentUserAsync();
            var job = await service.UpdateAsync(id, ToModel(request), currentUser.Id);
            return Results.Ok(job.ToResponse());
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }

    public static async Task<IResult> RunScheduledJobNow(
        int id,
        IScheduledJobService service)
    {
        try
        {
            var job = await service.RunNowAsync(id);
            return Results.Ok(job.ToResponse());
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }

    private static ScheduledJob ToModel(UpsertScheduledJobRequest request)
    {
        if (!Enum.TryParse<ScheduledJobType>(request.JobType, ignoreCase: true, out var jobType))
        {
            throw new ArgumentException("Unsupported job type.");
        }

        return new ScheduledJob
        {
            Name = request.Name,
            Description = request.Description,
            JobType = jobType,
            IntervalMinutes = request.IntervalMinutes,
            IsEnabled = request.IsEnabled,
            StoredProcedureDefinitionId = request.StoredProcedureDefinitionId
        };
    }
}
