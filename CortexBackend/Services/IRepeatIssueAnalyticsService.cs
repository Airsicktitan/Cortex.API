using Cortex.API.DTO;

namespace Cortex.API.Services;

/// <summary>
/// Read-only analytics service that groups live + archived tickets into recurring issue
/// buckets using a keyword-signature heuristic and produces honest effort metrics
/// (resolution time proxies, touch counts — not human work time).
/// </summary>
public interface IRepeatIssueAnalyticsService
{
    /// <summary>
    /// Returns the top recurring issue groups and aggregate headline metrics.
    /// </summary>
    /// <param name="topN">Maximum number of groups to return (ranked by repeat count, then recency).</param>
    Task<RepeatIssueOverviewResponse> GetOverviewAsync(
        int topN,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns detail for a single recurring issue group (tickets, boards, owners).
    /// </summary>
    /// <returns><c>null</c> when the group key is not found in the current snapshot.</returns>
    Task<RepeatIssueGroupDetailResponse?> GetGroupDetailAsync(
        string groupKey,
        CancellationToken cancellationToken = default);
}
