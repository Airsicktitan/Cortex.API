using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services;
using Microsoft.AspNetCore.Http;

namespace Cortex.API.Handlers;

public static class ScheduledJobHandlers
{
    public static async Task<IResult> GetScheduledJobs(
        IScheduledJobService service,
        IResponseMappingContextFactory mappingContextFactory,
        HttpContext httpContext)
    {
        var jobs = (await service.GetAllAsync()).ToList();
        var canViewSensitiveDetails =
            httpContext.User.IsInRole(Auth0Roles.Admin) ||
            httpContext.User.IsInRole(Auth0Roles.Developer);
        var mappingContext = await mappingContextFactory.CreateAsync(
            jobs.Select(job => job.RunAsUserId),
            jobs.Where(job => job.StoredProcedureDefinitionId.HasValue)
                .Select(job => job.StoredProcedureDefinitionId!.Value));
        return Results.Ok(jobs.Select(job => job.ToResponse(mappingContext, canViewSensitiveDetails)));
    }

    public static async Task<IResult> CreateScheduledJob(
        UpsertScheduledJobRequest request,
        IScheduledJobService service,
        IUserContextService userContextService,
        IResponseMappingContextFactory mappingContextFactory)
    {
        try
        {
            var currentUser = await userContextService.GetCurrentUserAsync();
            var job = await service.CreateAsync(ToModel(request), currentUser.Id);
            var mappingContext = await mappingContextFactory.CreateAsync(
                [job.RunAsUserId],
                job.StoredProcedureDefinitionId.HasValue
                    ? [job.StoredProcedureDefinitionId.Value]
                    : []);
            return Results.Created($"/api/jobs/{job.Id}", job.ToResponse(mappingContext));
        }
        catch (ArgumentException)
        {
            return SafeErrorResponses.BadRequest();
        }
    }

    public static async Task<IResult> UpdateScheduledJob(
        int id,
        UpsertScheduledJobRequest request,
        IScheduledJobService service,
        IUserContextService userContextService,
        IResponseMappingContextFactory mappingContextFactory)
    {
        try
        {
            var currentUser = await userContextService.GetCurrentUserAsync();
            var job = await service.UpdateAsync(id, ToModel(request), currentUser.Id);
            var mappingContext = await mappingContextFactory.CreateAsync(
                [job.RunAsUserId],
                job.StoredProcedureDefinitionId.HasValue
                    ? [job.StoredProcedureDefinitionId.Value]
                    : []);
            return Results.Ok(job.ToResponse(mappingContext));
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
        catch (ArgumentException)
        {
            return SafeErrorResponses.BadRequest();
        }
    }

    public static async Task<IResult> RunScheduledJobNow(
        int id,
        IScheduledJobService service,
        IResponseMappingContextFactory mappingContextFactory)
    {
        try
        {
            var job = await service.RunNowAsync(id);
            var mappingContext = await mappingContextFactory.CreateAsync(
                [job.RunAsUserId],
                job.StoredProcedureDefinitionId.HasValue
                    ? [job.StoredProcedureDefinitionId.Value]
                    : []);
            return Results.Ok(job.ToResponse(mappingContext));
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
        catch (ArgumentException)
        {
            return SafeErrorResponses.BadRequest();
        }
        catch (InvalidOperationException)
        {
            return SafeErrorResponses.BadRequest();
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
