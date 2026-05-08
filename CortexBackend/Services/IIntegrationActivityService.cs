using Cortex.API.DTO;

namespace Cortex.API.Services;

public interface IIntegrationActivityService
{
    Task RecordAsync(IntegrationActivityLogRecordRequest request, CancellationToken cancellationToken = default);

    /// <summary>Returns newest-first activity for the source, or null if the source does not exist.</summary>
    Task<IReadOnlyList<IntegrationActivityLogResponse>?> GetSourceActivityAsync(
        int sourceId,
        int take = 20,
        string? activityType = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns newest-first activity for the connection (any source under it plus connection-only rows), or null if the connection does not exist.</summary>
    Task<IReadOnlyList<IntegrationActivityLogResponse>?> GetConnectionActivityAsync(
        int connectionId,
        int take = 20,
        string? activityType = null,
        CancellationToken cancellationToken = default);
}
