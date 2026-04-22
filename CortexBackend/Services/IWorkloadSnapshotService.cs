using Cortex.API.Models;

namespace Cortex.API.Services;

public interface IWorkloadSnapshotService
{
    Task<IReadOnlyList<WorkloadSnapshot>> GetSnapshotsAsync(CancellationToken cancellationToken = default);
    Task<WorkloadSnapshot?> GetSnapshotAsync(string userId, CancellationToken cancellationToken = default);
}
