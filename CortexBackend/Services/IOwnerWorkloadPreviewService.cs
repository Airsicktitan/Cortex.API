using Cortex.API.DTO;

namespace Cortex.API.Services;

public interface IOwnerWorkloadPreviewService
{
    Task<OwnerWorkloadPreviewResponse> GetSummariesAsync(
        OwnerWorkloadPreviewRequest request,
        CancellationToken cancellationToken = default);
}
