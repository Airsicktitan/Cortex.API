using Cortex.API.Models;

namespace Cortex.API.Services;

public interface IRealtimeAudienceResolver
{
    Task<int[]> GetAudienceUserIdsAsync(
        int createdBy,
        string? synitiOwner,
        string? businessOwner,
        CancellationToken cancellationToken = default);

    Task<int[]> GetAudienceUserIdsAsync(
        Ticket ticket,
        CancellationToken cancellationToken = default);

    Task<int[]> GetAudienceUserIdsAsync(
        ArchivedTicket ticket,
        CancellationToken cancellationToken = default);
}
