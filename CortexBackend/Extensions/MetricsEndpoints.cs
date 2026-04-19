using Cortex.API.Authorization;
using Cortex.API.DTO;
using Cortex.API.Handlers;

namespace Cortex.API.Extensions;

public static class MetricsEndpoints
{
    public static void MapMetricsEndpoints(this WebApplication app)
    {
        var metrics = app.MapGroup("/api/metrics")
            .RequireAuthorization(CortexAuthorizationExtensions.BusinessAccess)
            .WithTags("Metrics");

        metrics.MapGet("/snapshot", MetricsHandlers.GetWorkflowMetricsSnapshot)
            .WithName("GetWorkflowMetricsSnapshot")
            .Produces<WorkflowMetricsSnapshotResponse>(StatusCodes.Status200OK);
    }
}
