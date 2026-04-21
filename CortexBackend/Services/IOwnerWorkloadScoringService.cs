namespace Cortex.API.Services;

public sealed record OwnerWorkloadScoreSnapshot(
    string OwnerKey,
    int ActiveTicketCount,
    int HighPriorityTicketCount,
    int AtRiskTicketCount,
    int OutsideSlaOpenCount,
    int SlaRiskTicketCount,
    int WorkloadScore);

public interface IOwnerWorkloadScoringService
{
    Task<IReadOnlyList<OwnerWorkloadScoreSnapshot>> GetScoresAsync(
        IEnumerable<string> ownerKeys,
        string? excludeTicketId = null,
        bool respectCurrentVisibility = true,
        CancellationToken cancellationToken = default);
}
