using Cortex.API.Handlers;
using Cortex.API.Models;

namespace Cortex.API.Extensions;

public static class WorkloadEndpoints
{
    public static void MapWorkloadEndpoints(this WebApplication app)
    {
        var workload = app.MapGroup("/api/workload")
            .RequireAuthorization()
            .WithTags("Workload");

        workload.MapGet("/snapshot", WorkloadHandlers.GetSnapshot)
            .WithName("GetWorkloadSnapshot")
            .Produces<IReadOnlyList<WorkloadSnapshot>>(StatusCodes.Status200OK);
    }
}
