using Cortex.API.Services;

namespace Cortex.API.Handlers;

public static class WorkloadHandlers
{
    public static async Task<IResult> GetSnapshot(
        IWorkloadSnapshotService workloadSnapshotService,
        CancellationToken cancellationToken)
    {
        var snapshots = await workloadSnapshotService.GetSnapshotsAsync(cancellationToken);
        return Results.Ok(snapshots);
    }
}
