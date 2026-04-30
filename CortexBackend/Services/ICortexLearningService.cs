using Cortex.API.DTO;

namespace Cortex.API.Services;

/// <summary>
/// Aggregates persisted ticket outcomes into advisory learning signals.
/// Read-only and observational — never mutates routing or assignments.
/// </summary>
public interface ICortexLearningService
{
    Task<OwnerSuccessStats> GetOwnerSuccessStatsAsync(
        string owner,
        int? boardId = null,
        CancellationToken cancellationToken = default);

    Task<SemanticClusterStats> GetSemanticClusterStatsAsync(
        string ticketId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cluster stats seeded with the same similar tickets the insight panel surfaced
    /// so the learning layer never falls below the user-visible evidence set.
    /// </summary>
    Task<SemanticClusterStats> GetSemanticClusterStatsAsync(
        string ticketId,
        IReadOnlyCollection<string> displayedSimilarTicketIds,
        CancellationToken cancellationToken = default);

    Task<RoutingRuleEffectiveness> GetRoutingRuleEffectivenessAsync(
        int ruleId,
        CancellationToken cancellationToken = default,
        bool bypassCache = false);

    Task<List<CortexLearningSignalDto>> GetLearningSignalsAsync(
        string ticketId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Composes signals using the displayed similar tickets as evidence so the
    /// learning panel stays aligned with what the user is already seeing.
    /// </summary>
    Task<List<CortexLearningSignalDto>> GetLearningSignalsAsync(
        string ticketId,
        IReadOnlyCollection<string> displayedSimilarTicketIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tier 6 — converts learning stats into bounded ±10 score adjustments
    /// that the decision service can apply to candidate scores or final
    /// confidence. Empty when no meaningful evidence exists.
    /// </summary>
    Task<IReadOnlyList<CortexLearningScoreAdjustment>> GetScoreAdjustmentsAsync(
        string ticketId,
        IReadOnlyCollection<string> displayedSimilarTicketIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CortexSystemRecommendation>> GetSystemRecommendationsAsync(
        CancellationToken cancellationToken = default);
}

public sealed class OwnerSuccessStats
{
    public string Owner { get; set; } = string.Empty;
    public int? BoardId { get; set; }
    public int TotalCompleted { get; set; }
    public int SlaBreachedCount { get; set; }
    public double SlaSuccessPercent { get; set; }
    public int OverrideCount { get; set; }
    public double OverridePercent { get; set; }
    public double AverageCommentCount { get; set; }
    public int ReassignedCount { get; set; }
    public double ReassignmentPercent { get; set; }
    public int ReopenedCount { get; set; }
    public double ReopenPercent { get; set; }
}

public sealed class SemanticClusterStats
{
    public string TicketId { get; set; } = string.Empty;
    public int SimilarTicketCount { get; set; }
    public int OutcomeMatchedCount { get; set; }
    public string? MostCommonSuccessfulOwner { get; set; }
    public int MostCommonSuccessfulOwnerCount { get; set; }
    public string? CommonOverrideTarget { get; set; }
    public int CommonOverrideTargetCount { get; set; }
    public double SlaSuccessPercent { get; set; }
    public double AverageCommentCount { get; set; }
    public int ReassignmentCount { get; set; }
    public double TopSimilarity { get; set; }
}

public sealed class RoutingRuleEffectiveness
{
    public int RuleId { get; set; }
    public int TotalDecisions { get; set; }
    public int FollowedCount { get; set; }
    public int OverrideCount { get; set; }
    public double FollowedPercent { get; set; }
    public double OverridePercent { get; set; }
    public int OutcomeSampleCount { get; set; }
    public int SlaBreachedCount { get; set; }
    public double SlaSuccessPercent { get; set; }
    public double AverageCommentCount { get; set; }
    public int ReassignmentCount { get; set; }
    public double ReassignmentPercent { get; set; }
}
