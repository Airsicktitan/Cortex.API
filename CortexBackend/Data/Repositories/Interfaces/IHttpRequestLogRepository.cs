using Cortex.API.Models;

namespace Cortex.API.Data.Repositories;

public interface IHttpRequestLogRepository
{
    Task AddAsync(HttpRequestLogEntry entry, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HttpRequestLogEntry>> GetBetweenAsync(
        DateTime fromUtcInclusive,
        DateTime toUtcInclusive,
        int maxRows,
        CancellationToken cancellationToken = default);
}
