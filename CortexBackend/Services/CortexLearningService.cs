using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cortex.API.Database;
using Cortex.API.DTO;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Cortex.API.Services;

/// <summary>
/// Tier 5 advisory learning aggregations over persisted ticket outcomes.
/// Read-only. Never mutates routing decisions, scoring, or owner assignments.
/// </summary>
public sealed class CortexLearningService : ICortexLearningService
{
    // Lower-bound to reject true noise. Still well above random cosine; aligns with
    // tickets the insight panel surfaces via its blended score (>= 25/100).
    private const double MinimumLearningSimilarity = 0.30;
    private const int MaxSemanticCluster = 25;
    private const int MaxLearningSignals = 5;
    private const int MinOwnerSignalSamples = 2;
    private const int MinSemanticOwnerSamples = 1;
    private const int MinSemanticFollowupSamples = 3;
    private const double MinSemanticFollowupAvgComments = 3.0;
    private const int MinRuleSampleForSignal = 3;
    private const int MinRuleOutcomeSampleForGoodSignal = 2;
    private const int MaxSystemRecommendations = 3;

    // Tier 6 score adjustment bounds. Each adjustment is hard-clamped to [-10, +10];
    // the decision service additionally clamps the per-target net delta.
    public const int MaxLearningBoost = 10;
    public const int MaxLearningPenalty = -10;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private readonly CortexDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CortexLearningService> _logger;

    public CortexLearningService(
        CortexDbContext db,
        IMemoryCache cache,
        ILogger<CortexLearningService> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task<OwnerSuccessStats> GetOwnerSuccessStatsAsync(
        string owner,
        int? boardId = null,
        CancellationToken cancellationToken = default)
    {
        var empty = new OwnerSuccessStats { Owner = owner ?? string.Empty, BoardId = boardId };
        if (string.IsNullOrWhiteSpace(owner))
        {
            return empty;
        }

        var key = $"learning:owner:{owner.Trim().ToLowerInvariant()}:{boardId?.ToString() ?? "any"}";
        if (_cache.TryGetValue<OwnerSuccessStats>(key, out var cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            var trimmed = owner.Trim();
            var query = _db.TicketOutcomes
                .AsNoTracking()
                .Where(o => o.ReachedTerminalStatus
                    && (o.FinalSynitiOwner == trimmed || o.FinalBusinessOwner == trimmed));

            if (boardId.HasValue)
            {
                query = query.Where(o => o.BoardId == boardId.Value);
            }

            var totals = await query
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    SlaBreached = g.Count(o => o.SlaBreached),
                    Overridden = g.Count(o => o.WasOverridden),
                    Reassigned = g.Count(o => o.WasReassigned),
                    Reopened = g.Count(o => o.WasReopened),
                    AvgComments = g.Average(o => (double?)o.CommentCount) ?? 0.0,
                })
                .FirstOrDefaultAsync(cancellationToken);

            var stats = totals is null
                ? empty
                : new OwnerSuccessStats
                {
                    Owner = trimmed,
                    BoardId = boardId,
                    TotalCompleted = totals.Total,
                    SlaBreachedCount = totals.SlaBreached,
                    SlaSuccessPercent = SafePercent(totals.Total - totals.SlaBreached, totals.Total),
                    OverrideCount = totals.Overridden,
                    OverridePercent = SafePercent(totals.Overridden, totals.Total),
                    ReassignedCount = totals.Reassigned,
                    ReassignmentPercent = SafePercent(totals.Reassigned, totals.Total),
                    ReopenedCount = totals.Reopened,
                    ReopenPercent = SafePercent(totals.Reopened, totals.Total),
                    AverageCommentCount = Math.Round(totals.AvgComments, 2),
                };

            _cache.Set(key, stats, CacheDuration);
            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Owner success stats failed for owner {Owner}.", owner);
            return empty;
        }
    }

    public Task<SemanticClusterStats> GetSemanticClusterStatsAsync(
        string ticketId,
        CancellationToken cancellationToken = default) =>
        GetSemanticClusterStatsAsync(ticketId, Array.Empty<string>(), cancellationToken);

    public async Task<SemanticClusterStats> GetSemanticClusterStatsAsync(
        string ticketId,
        IReadOnlyCollection<string> displayedSimilarTicketIds,
        CancellationToken cancellationToken = default)
    {
        var empty = new SemanticClusterStats { TicketId = ticketId ?? string.Empty };
        if (string.IsNullOrWhiteSpace(ticketId))
        {
            return empty;
        }

        var displayedSet = (displayedSimilarTicketIds ?? Array.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .ToHashSet(StringComparer.Ordinal);

        var key = $"learning:semantic:{ticketId}:{HashIds(displayedSet)}";
        if (_cache.TryGetValue<SemanticClusterStats>(key, out var cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            var similarities = await ComputeSemanticSimilaritiesAsync(ticketId, cancellationToken);
            var topSimilarity = similarities.Count == 0 ? 0.0 : similarities.Values.Max();

            // Start with insight-panel displayed IDs (already vetted by the blended insight score),
            // then layer in any additional cosine matches above the learning floor.
            var clusterIds = new HashSet<string>(displayedSet, StringComparer.Ordinal);
            foreach (var pair in similarities
                .Where(p => p.Value >= MinimumLearningSimilarity)
                .OrderByDescending(p => p.Value)
                .Take(MaxSemanticCluster))
            {
                clusterIds.Add(pair.Key);
                if (clusterIds.Count >= MaxSemanticCluster)
                {
                    break;
                }
            }

            if (clusterIds.Count == 0)
            {
                _logger.LogInformation(
                    "Cortex learning: no semantic cluster for ticket {TicketId} (similarities={SimCount}, topSim={TopSim:F2}, displayedIds=0).",
                    ticketId,
                    similarities.Count,
                    topSimilarity);
                _cache.Set(key, empty, CacheDuration);
                return empty;
            }

            var qualifyingIds = clusterIds.ToList();

            var outcomes = await _db.TicketOutcomes
                .AsNoTracking()
                .Where(o => qualifyingIds.Contains(o.TicketId))
                .ToListAsync(cancellationToken);

            if (outcomes.Count == 0)
            {
                _logger.LogInformation(
                    "Cortex learning: semantic cluster for ticket {TicketId} has no TicketOutcome rows yet (clusterSize={ClusterSize}).",
                    ticketId,
                    qualifyingIds.Count);
            }

            var terminal = outcomes.Where(o => o.ReachedTerminalStatus).ToList();
            var topOwner = terminal
                .Where(o => !o.SlaBreached && !string.IsNullOrWhiteSpace(o.FinalSynitiOwner))
                .GroupBy(o => o.FinalSynitiOwner!.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => new { Owner = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .FirstOrDefault();

            // Owners that emerged via override on similar tickets — still useful when the
            // base owner sample is sparse.
            if (topOwner is null)
            {
                topOwner = terminal
                    .Where(o => !string.IsNullOrWhiteSpace(o.FinalSynitiOwner))
                    .GroupBy(o => o.FinalSynitiOwner!.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Select(g => new { Owner = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .FirstOrDefault();
            }

            var topOverrideTarget = outcomes
                .Where(o => o.WasOverridden && !string.IsNullOrWhiteSpace(o.FinalSynitiOwner))
                .GroupBy(o => o.FinalSynitiOwner!.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => new { Owner = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .FirstOrDefault();

            var slaSuccess = terminal.Count == 0
                ? 0.0
                : SafePercent(terminal.Count(o => !o.SlaBreached), terminal.Count);
            var avgComments = terminal.Count == 0
                ? 0.0
                : Math.Round(terminal.Average(o => (double)o.CommentCount), 2);
            var reassignmentCount = outcomes.Count(o => o.WasReassigned);

            var stats = new SemanticClusterStats
            {
                TicketId = ticketId,
                SimilarTicketCount = qualifyingIds.Count,
                OutcomeMatchedCount = outcomes.Count,
                MostCommonSuccessfulOwner = topOwner?.Owner,
                MostCommonSuccessfulOwnerCount = topOwner?.Count ?? 0,
                CommonOverrideTarget = topOverrideTarget?.Owner,
                CommonOverrideTargetCount = topOverrideTarget?.Count ?? 0,
                SlaSuccessPercent = slaSuccess,
                AverageCommentCount = avgComments,
                ReassignmentCount = reassignmentCount,
                TopSimilarity = topSimilarity,
            };

            _cache.Set(key, stats, CacheDuration);
            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Semantic cluster stats failed for ticket {TicketId}.", ticketId);
            return empty;
        }
    }

    public async Task<RoutingRuleEffectiveness> GetRoutingRuleEffectivenessAsync(
        int ruleId,
        CancellationToken cancellationToken = default)
    {
        var empty = new RoutingRuleEffectiveness { RuleId = ruleId };
        if (ruleId <= 0)
        {
            return empty;
        }

        var key = $"learning:rule:{ruleId}";
        if (_cache.TryGetValue<RoutingRuleEffectiveness>(key, out var cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            var ticketIds = await _db.TicketRoutingDecisions
                .AsNoTracking()
                .Where(d => d.MatchedRuleId == ruleId)
                .Select(d => d.TicketId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (ticketIds.Count == 0)
            {
                _cache.Set(key, empty, CacheDuration);
                return empty;
            }

            var overrideTicketIds = await _db.TicketRoutingOverrides
                .AsNoTracking()
                .Where(o => ticketIds.Contains(o.TicketId))
                .Select(o => o.TicketId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var outcomes = await _db.TicketOutcomes
                .AsNoTracking()
                .Where(o => ticketIds.Contains(o.TicketId))
                .ToListAsync(cancellationToken);

            var terminal = outcomes.Where(o => o.ReachedTerminalStatus).ToList();
            var slaBreached = terminal.Count(o => o.SlaBreached);
            var reassigned = outcomes.Count(o => o.WasReassigned);

            var stats = new RoutingRuleEffectiveness
            {
                RuleId = ruleId,
                TotalDecisions = ticketIds.Count,
                OverrideCount = overrideTicketIds.Count,
                FollowedCount = Math.Max(0, ticketIds.Count - overrideTicketIds.Count),
                OverridePercent = SafePercent(overrideTicketIds.Count, ticketIds.Count),
                FollowedPercent = SafePercent(ticketIds.Count - overrideTicketIds.Count, ticketIds.Count),
                OutcomeSampleCount = terminal.Count,
                SlaBreachedCount = slaBreached,
                SlaSuccessPercent = terminal.Count == 0
                    ? 0.0
                    : SafePercent(terminal.Count - slaBreached, terminal.Count),
                AverageCommentCount = terminal.Count == 0
                    ? 0.0
                    : Math.Round(terminal.Average(o => (double)o.CommentCount), 2),
                ReassignmentCount = reassigned,
                ReassignmentPercent = SafePercent(reassigned, ticketIds.Count),
            };

            _cache.Set(key, stats, CacheDuration);
            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Routing rule effectiveness failed for rule {RuleId}.", ruleId);
            return empty;
        }
    }

    public Task<List<CortexLearningSignalDto>> GetLearningSignalsAsync(
        string ticketId,
        CancellationToken cancellationToken = default) =>
        GetLearningSignalsAsync(ticketId, Array.Empty<string>(), cancellationToken);

    public async Task<List<CortexLearningSignalDto>> GetLearningSignalsAsync(
        string ticketId,
        IReadOnlyCollection<string> displayedSimilarTicketIds,
        CancellationToken cancellationToken = default)
    {
        var signals = new List<CortexLearningSignalDto>();
        if (string.IsNullOrWhiteSpace(ticketId))
        {
            return signals;
        }

        try
        {
            var ticket = await _db.Tickets
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);

            if (ticket is null)
            {
                return signals;
            }

            var displayedIds = (displayedSimilarTicketIds ?? Array.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id) && !string.Equals(id, ticketId, StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var semanticTask = GetSemanticClusterStatsAsync(ticketId, displayedIds, cancellationToken);
            var ruleEffectivenessTask = GetLatestRuleEffectivenessAsync(ticketId, cancellationToken);
            var assignedSynitiOwnerStatsTask = string.IsNullOrWhiteSpace(ticket.SynitiOwner)
                ? Task.FromResult<OwnerSuccessStats?>(null)
                : GetOwnerStatsAsNullable(ticket.SynitiOwner!, ticket.BoardId, cancellationToken);

            await Task.WhenAll(semanticTask, ruleEffectivenessTask, assignedSynitiOwnerStatsTask);

            var semantic = semanticTask.Result;
            var ruleEffectiveness = ruleEffectivenessTask.Result;
            var ownerStats = assignedSynitiOwnerStatsTask.Result;

            if (semantic.SimilarTicketCount > 0 && semantic.OutcomeMatchedCount == 0)
            {
                _logger.LogInformation(
                    "Cortex learning: ticket {TicketId} has {ClusterSize} similar tickets but none have TicketOutcome rows; semantic signals skipped.",
                    ticketId,
                    semantic.SimilarTicketCount);
            }

            // Signal: Semantic cluster — most common successful owner.
            if (semantic.OutcomeMatchedCount > 0
                && !string.IsNullOrWhiteSpace(semantic.MostCommonSuccessfulOwner)
                && semantic.MostCommonSuccessfulOwnerCount >= MinSemanticOwnerSamples)
            {
                var supporting = new List<string>
                {
                    $"{semantic.SimilarTicketCount} similar tickets analyzed",
                    $"{semantic.OutcomeMatchedCount} with recorded outcomes",
                    $"{Math.Round(semantic.SlaSuccessPercent)}% resolved within SLA",
                    $"Average comments: {semantic.AverageCommentCount:0.0}",
                };
                if (semantic.CommonOverrideTargetCount > 0
                    && !string.Equals(semantic.CommonOverrideTarget, semantic.MostCommonSuccessfulOwner, StringComparison.OrdinalIgnoreCase))
                {
                    supporting.Add($"Common override target: {semantic.CommonOverrideTarget}");
                }

                signals.Add(new CortexLearningSignalDto
                {
                    SignalType = "Semantic",
                    Title = "Historically successful owner found",
                    Description = $"Similar tickets were most often resolved successfully when assigned to {semantic.MostCommonSuccessfulOwner}.",
                    Confidence = ConfidenceFromSample(
                        semantic.MostCommonSuccessfulOwnerCount,
                        semantic.SlaSuccessPercent,
                        strongSampleFloor: 10,
                        mediumSampleFloor: 4,
                        strongSuccessFloor: 75,
                        mediumSuccessFloor: 55),
                    SupportingFacts = supporting,
                });
            }

            // Signal: Semantic cluster — high follow-up activity.
            if (semantic.OutcomeMatchedCount >= MinSemanticFollowupSamples
                && semantic.AverageCommentCount >= MinSemanticFollowupAvgComments)
            {
                signals.Add(new CortexLearningSignalDto
                {
                    SignalType = "Semantic",
                    Title = "Similar tickets often needed follow-up",
                    Description = "Past tickets similar to this one had higher-than-average comment activity before resolution.",
                    Confidence = ConfidenceFromSample(
                        semantic.OutcomeMatchedCount,
                        semantic.AverageCommentCount * 10,
                        strongSampleFloor: 10,
                        mediumSampleFloor: 5,
                        strongSuccessFloor: 60,
                        mediumSuccessFloor: 35),
                    SupportingFacts =
                    [
                        $"{semantic.SimilarTicketCount} similar tickets analyzed",
                        $"Average comments: {semantic.AverageCommentCount:0.0}",
                        $"{semantic.ReassignmentCount} required reassignment or clarification",
                    ],
                });
            }

            // Signal: Routing rule effectiveness.
            if (ruleEffectiveness is not null
                && ruleEffectiveness.TotalDecisions >= MinRuleSampleForSignal)
            {
                if (ruleEffectiveness.OverridePercent <= 25
                    && ruleEffectiveness.OutcomeSampleCount >= MinRuleOutcomeSampleForGoodSignal)
                {
                    signals.Add(new CortexLearningSignalDto
                    {
                        SignalType = "Rule",
                        Title = "Routing rule has strong historical performance",
                        Description = "This routing rule has usually produced stable assignments with low override activity.",
                        Confidence = ConfidenceFromSample(
                            ruleEffectiveness.TotalDecisions,
                            100 - ruleEffectiveness.OverridePercent,
                            strongSampleFloor: 12,
                            mediumSampleFloor: 5,
                            strongSuccessFloor: 80,
                            mediumSuccessFloor: 60),
                        SupportingFacts =
                        [
                            $"{ruleEffectiveness.TotalDecisions} prior tickets matched this rule",
                            $"{Math.Round(ruleEffectiveness.OverridePercent)}% override rate",
                            $"{Math.Round(ruleEffectiveness.SlaSuccessPercent)}% resolved within SLA",
                        ],
                    });
                }
                else if (ruleEffectiveness.OverridePercent >= 50)
                {
                    signals.Add(new CortexLearningSignalDto
                    {
                        SignalType = "Rule",
                        Title = "Routing rule frequently overridden",
                        Description = "Reviewers have often replaced this rule's recommended owner before completion.",
                        Confidence = ConfidenceFromSample(
                            ruleEffectiveness.TotalDecisions,
                            ruleEffectiveness.OverridePercent,
                            strongSampleFloor: 12,
                            mediumSampleFloor: 5,
                            strongSuccessFloor: 70,
                            mediumSuccessFloor: 50),
                        SupportingFacts =
                        [
                            $"{ruleEffectiveness.TotalDecisions} prior tickets matched this rule",
                            $"{Math.Round(ruleEffectiveness.OverridePercent)}% override rate",
                            $"{ruleEffectiveness.ReassignmentCount} reassignments observed",
                        ],
                    });
                }
            }
            else if (ruleEffectiveness is null)
            {
                _logger.LogInformation(
                    "Cortex learning: ticket {TicketId} has no matched routing rule history; rule signal skipped.",
                    ticketId);
            }

            // Signal: Owner stats for the currently assigned Syniti owner.
            if (ownerStats is not null && ownerStats.TotalCompleted >= MinOwnerSignalSamples)
            {
                if (ownerStats.SlaSuccessPercent >= 70)
                {
                    signals.Add(new CortexLearningSignalDto
                    {
                        SignalType = "Owner",
                        Title = "Assigned owner has strong delivery history",
                        Description = $"{ownerStats.Owner} has a consistent record of resolving tickets on this board within SLA.",
                        Confidence = ConfidenceFromSample(
                            ownerStats.TotalCompleted,
                            ownerStats.SlaSuccessPercent,
                            strongSampleFloor: 15,
                            mediumSampleFloor: 5,
                            strongSuccessFloor: 80,
                            mediumSuccessFloor: 60),
                        SupportingFacts =
                        [
                            $"{ownerStats.TotalCompleted} completed tickets analyzed",
                            $"{Math.Round(ownerStats.SlaSuccessPercent)}% resolved within SLA",
                            $"{Math.Round(ownerStats.OverridePercent)}% override rate",
                            $"Average comments: {ownerStats.AverageCommentCount:0.0}",
                        ],
                    });
                }
                else if (ownerStats.ReopenPercent >= 25 || ownerStats.ReassignmentPercent >= 30)
                {
                    signals.Add(new CortexLearningSignalDto
                    {
                        SignalType = "Risk",
                        Title = "Assigned owner has elevated rework rate",
                        Description = $"Tickets resolved by {ownerStats.Owner} have historically been reopened or reassigned more often than average.",
                        Confidence = ConfidenceFromSample(
                            ownerStats.TotalCompleted,
                            Math.Max(ownerStats.ReopenPercent, ownerStats.ReassignmentPercent),
                            strongSampleFloor: 12,
                            mediumSampleFloor: 5,
                            strongSuccessFloor: 35,
                            mediumSuccessFloor: 20),
                        SupportingFacts =
                        [
                            $"{ownerStats.TotalCompleted} completed tickets analyzed",
                            $"{Math.Round(ownerStats.ReopenPercent)}% reopened",
                            $"{Math.Round(ownerStats.ReassignmentPercent)}% reassigned",
                        ],
                    });
                }
            }

            if (signals.Count == 0)
            {
                _logger.LogInformation(
                    "Cortex learning: no signals produced for ticket {TicketId}. clusterSize={ClusterSize} outcomeMatched={OutcomeMatched} ruleSamples={RuleSamples} ownerSamples={OwnerSamples}.",
                    ticketId,
                    semantic.SimilarTicketCount,
                    semantic.OutcomeMatchedCount,
                    ruleEffectiveness?.TotalDecisions ?? 0,
                    ownerStats?.TotalCompleted ?? 0);
            }

            return signals.Take(MaxLearningSignals).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Learning signals generation failed for ticket {TicketId}.", ticketId);
            return signals;
        }
    }

    public async Task<IReadOnlyList<CortexLearningScoreAdjustment>> GetScoreAdjustmentsAsync(
        string ticketId,
        IReadOnlyCollection<string> displayedSimilarTicketIds,
        CancellationToken cancellationToken = default)
    {
        var adjustments = new List<CortexLearningScoreAdjustment>();
        if (string.IsNullOrWhiteSpace(ticketId))
        {
            return adjustments;
        }

        try
        {
            var ticket = await _db.Tickets
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);
            if (ticket is null)
            {
                return adjustments;
            }

            var displayedIds = (displayedSimilarTicketIds ?? Array.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id) && !string.Equals(id, ticketId, StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var semanticTask = GetSemanticClusterStatsAsync(ticketId, displayedIds, cancellationToken);
            var ruleTask = GetLatestRuleEffectivenessAsync(ticketId, cancellationToken);
            await Task.WhenAll(semanticTask, ruleTask);
            var semantic = semanticTask.Result;
            var rule = ruleTask.Result;

            // -- Owner: success boost from semantic cluster --
            if (semantic.OutcomeMatchedCount > 0
                && !string.IsNullOrWhiteSpace(semantic.MostCommonSuccessfulOwner)
                && semantic.MostCommonSuccessfulOwnerCount >= MinSemanticOwnerSamples)
            {
                var (delta, confidence) = OwnerBoost(
                    semantic.MostCommonSuccessfulOwnerCount,
                    semantic.SlaSuccessPercent);
                if (delta > 0)
                {
                    adjustments.Add(new CortexLearningScoreAdjustment
                    {
                        TargetType = "Owner",
                        TargetValue = semantic.MostCommonSuccessfulOwner,
                        ScoreDelta = ClampDelta(delta),
                        Confidence = confidence,
                        Reason = $"Similar tickets were historically resolved successfully by {semantic.MostCommonSuccessfulOwner}.",
                        SupportingFacts =
                        [
                            $"{semantic.SimilarTicketCount} similar tickets analyzed",
                            $"{semantic.MostCommonSuccessfulOwnerCount} resolved by this owner",
                            $"{Math.Round(semantic.SlaSuccessPercent)}% within SLA",
                        ],
                    });
                }
            }

            // -- Rule: override-rate penalty --
            if (rule is not null && rule.TotalDecisions >= MinRuleSampleForSignal)
            {
                var (delta, confidence) = RulePenalty(rule.TotalDecisions, rule.OverridePercent);
                if (delta < 0)
                {
                    adjustments.Add(new CortexLearningScoreAdjustment
                    {
                        TargetType = "Rule",
                        TargetValue = rule.RuleId.ToString(),
                        ScoreDelta = ClampDelta(delta),
                        Confidence = confidence,
                        Reason = "Matched routing rule has historically been overridden frequently.",
                        SupportingFacts =
                        [
                            $"{rule.TotalDecisions} prior tickets matched this rule",
                            $"{Math.Round(rule.OverridePercent)}% override rate",
                            $"{Math.Round(rule.SlaSuccessPercent)}% resolved within SLA",
                        ],
                    });
                }
            }

            // -- Decision: reassignment / follow-up penalty --
            if (semantic.OutcomeMatchedCount >= MinRuleOutcomeSampleForGoodSignal)
            {
                var reassignRate = semantic.SimilarTicketCount > 0
                    ? (double)semantic.ReassignmentCount / semantic.SimilarTicketCount
                    : 0.0;
                var (delta, confidence) = DecisionFollowupPenalty(
                    semantic.OutcomeMatchedCount,
                    semantic.AverageCommentCount,
                    reassignRate);
                if (delta < 0)
                {
                    adjustments.Add(new CortexLearningScoreAdjustment
                    {
                        TargetType = "Decision",
                        TargetValue = null,
                        ScoreDelta = ClampDelta(delta),
                        Confidence = confidence,
                        Reason = "Similar tickets often required reassignment or follow-up before completion.",
                        SupportingFacts =
                        [
                            $"{semantic.OutcomeMatchedCount} similar tickets had recorded outcomes",
                            $"Average comments: {semantic.AverageCommentCount:0.0}",
                            $"{semantic.ReassignmentCount} required reassignment",
                        ],
                    });
                }
            }

            // -- Risk: SLA-breach pattern in similar tickets --
            if (semantic.OutcomeMatchedCount >= MinRuleOutcomeSampleForGoodSignal
                && semantic.SlaSuccessPercent <= 50)
            {
                var (delta, confidence) = RiskSlaPenalty(
                    semantic.OutcomeMatchedCount,
                    semantic.SlaSuccessPercent);
                if (delta < 0)
                {
                    adjustments.Add(new CortexLearningScoreAdjustment
                    {
                        TargetType = "Risk",
                        TargetValue = null,
                        ScoreDelta = ClampDelta(delta),
                        Confidence = confidence,
                        Reason = "Similar tickets have historically shown elevated SLA risk.",
                        SupportingFacts =
                        [
                            $"{semantic.OutcomeMatchedCount} similar tickets with recorded outcomes",
                            $"{Math.Round(semantic.SlaSuccessPercent)}% resolved within SLA",
                        ],
                    });
                }
            }

            if (adjustments.Count == 0)
            {
                _logger.LogInformation(
                    "Cortex learning: no score adjustments produced for ticket {TicketId}. clusterSize={ClusterSize} outcomeMatched={OutcomeMatched} ruleSamples={RuleSamples}.",
                    ticketId,
                    semantic.SimilarTicketCount,
                    semantic.OutcomeMatchedCount,
                    rule?.TotalDecisions ?? 0);
            }
            else
            {
                _logger.LogInformation(
                    "Cortex learning: produced {Count} score adjustments for ticket {TicketId}.",
                    adjustments.Count,
                    ticketId);
            }

            return adjustments;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Score adjustment generation failed for ticket {TicketId}.", ticketId);
            return adjustments;
        }
    }

    public async Task<IReadOnlyList<CortexSystemRecommendation>> GetSystemRecommendationsAsync(
        CancellationToken cancellationToken = default)
    {
        var recommendations = new List<CortexSystemRecommendation>();
        try
        {
            var ruleDecisionCounts = await _db.TicketRoutingDecisions
                .AsNoTracking()
                .Where(d => d.MatchedRuleId != null)
                .GroupBy(d => d.MatchedRuleId!.Value)
                .Select(g => new
                {
                    RuleId = g.Key,
                    TotalDecisions = g.Count()
                })
                .ToListAsync(cancellationToken);

            if (ruleDecisionCounts.Count == 0)
            {
                return recommendations;
            }

            foreach (var candidate in ruleDecisionCounts.OrderByDescending(x => x.TotalDecisions))
            {
                var effectiveness = await GetRoutingRuleEffectivenessAsync(candidate.RuleId, cancellationToken);
                if (effectiveness.TotalDecisions < 5 || effectiveness.OverridePercent < 70)
                {
                    continue;
                }

                var bestOwner = await ResolveBestOwnerForRuleAsync(candidate.RuleId, cancellationToken);
                recommendations.Add(new CortexSystemRecommendation
                {
                    Id = $"routing-rule:{candidate.RuleId}:override-rate",
                    Type = "RoutingRule",
                    SourceType = "RoutingRule",
                    SourceId = candidate.RuleId.ToString(),
                    Title = $"Routing rule ineffective ({Math.Round(effectiveness.OverridePercent)}% overridden)",
                    Description = "This routing rule is frequently overridden and does not reflect actual usage.",
                    Recommendation = string.IsNullOrWhiteSpace(bestOwner)
                        ? "Consider updating this routing rule to align with historical final ownership outcomes."
                        : $"Consider routing similar tickets to {bestOwner} instead.",
                    Confidence = effectiveness.TotalDecisions >= 8 ? "High" : "Medium",
                    Severity = effectiveness.OverridePercent >= 85 ? "High" : "Medium",
                    Status = "Open",
                    ActionLabel = "Review routing rule",
                    ActionPreview = string.IsNullOrWhiteSpace(bestOwner)
                        ? "Suggested configuration change: update this routing rule to align with historical final ownership outcomes."
                        : $"Suggested configuration change: route similar tickets to {bestOwner} by default.",
                    GeneratedAtUtc = DateTime.UtcNow,
                    SupportingFacts =
                    [
                        $"{effectiveness.TotalDecisions} tickets matched this rule",
                        $"{Math.Round(effectiveness.OverridePercent)}% override rate",
                        $"{Math.Round(effectiveness.SlaSuccessPercent)}% resolved within SLA",
                    ]
                });

                if (recommendations.Count >= MaxSystemRecommendations)
                {
                    break;
                }
            }

            if (recommendations.Count == 0)
            {
                return recommendations;
            }

            var ids = recommendations.Select(r => r.Id).ToList();
            var states = await _db.CortexSystemRecommendationStates
                .AsNoTracking()
                .Where(state => ids.Contains(state.RecommendationId))
                .ToListAsync(cancellationToken);

            var stateByRecommendationId = states.ToDictionary(
                state => state.RecommendationId,
                state => state,
                StringComparer.OrdinalIgnoreCase);
            foreach (var recommendation in recommendations)
            {
                if (!stateByRecommendationId.TryGetValue(recommendation.Id, out var state))
                {
                    continue;
                }

                recommendation.Status = string.IsNullOrWhiteSpace(state.Status)
                    ? "Open"
                    : state.Status;
                recommendation.DismissedReason = state.DismissedReason;
            }

            return recommendations;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "System recommendation generation failed.");
            return recommendations;
        }
    }

    private async Task<string?> ResolveBestOwnerForRuleAsync(
        int ruleId,
        CancellationToken cancellationToken)
    {
        var matchedTicketIds = await _db.TicketRoutingDecisions
            .AsNoTracking()
            .Where(d => d.MatchedRuleId == ruleId)
            .Select(d => d.TicketId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (matchedTicketIds.Count == 0)
        {
            return null;
        }

        var outcomes = await _db.TicketOutcomes
            .AsNoTracking()
            .Where(o => matchedTicketIds.Contains(o.TicketId))
            .ToListAsync(cancellationToken);

        var successfulOwner = outcomes
            .Where(o => o.ReachedTerminalStatus && !o.SlaBreached && !string.IsNullOrWhiteSpace(o.FinalSynitiOwner))
            .GroupBy(o => o.FinalSynitiOwner!.Trim(), StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(successfulOwner))
        {
            return successfulOwner;
        }

        return outcomes
            .Where(o => !string.IsNullOrWhiteSpace(o.FinalSynitiOwner))
            .GroupBy(o => o.FinalSynitiOwner!.Trim(), StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();
    }

    private static int ClampDelta(int delta) =>
        Math.Max(MaxLearningPenalty, Math.Min(MaxLearningBoost, delta));

    private static (int Delta, string Confidence) OwnerBoost(int sampleCount, double slaSuccessPercent)
    {
        if (sampleCount >= 8 && slaSuccessPercent >= 75)
        {
            return (8, "High");
        }
        if (sampleCount >= 4 && slaSuccessPercent >= 60)
        {
            return (4, "Medium");
        }
        if (sampleCount >= 1)
        {
            return (2, "Low");
        }
        return (0, "Low");
    }

    private static (int Delta, string Confidence) RulePenalty(int totalDecisions, double overridePercent)
    {
        if (totalDecisions >= 8 && overridePercent >= 70)
        {
            return (-8, "High");
        }
        if (totalDecisions >= 5 && overridePercent >= 50)
        {
            return (-5, "Medium");
        }
        if (overridePercent >= 35)
        {
            return (-2, "Low");
        }
        return (0, "Low");
    }

    private static (int Delta, string Confidence) DecisionFollowupPenalty(
        int outcomeCount,
        double averageComments,
        double reassignmentRate)
    {
        if (outcomeCount >= 6 && (averageComments >= 6.0 || reassignmentRate >= 0.5))
        {
            return (-6, "Medium");
        }
        if (outcomeCount >= 3 && (averageComments >= 4.0 || reassignmentRate >= 0.35))
        {
            return (-3, "Low");
        }
        return (0, "Low");
    }

    private static (int Delta, string Confidence) RiskSlaPenalty(int outcomeCount, double slaSuccessPercent)
    {
        if (outcomeCount >= 6 && slaSuccessPercent <= 35)
        {
            return (-6, "Medium");
        }
        if (outcomeCount >= 3 && slaSuccessPercent <= 50)
        {
            return (-4, "Low");
        }
        return (0, "Low");
    }

    private async Task<RoutingRuleEffectiveness?> GetLatestRuleEffectivenessAsync(
        string ticketId,
        CancellationToken cancellationToken)
    {
        var matchedRuleId = await _db.TicketRoutingDecisions
            .AsNoTracking()
            .Where(d => d.TicketId == ticketId && d.MatchedRuleId != null)
            .OrderByDescending(d => d.CreatedDateUtc)
            .Select(d => d.MatchedRuleId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!matchedRuleId.HasValue)
        {
            return null;
        }

        return await GetRoutingRuleEffectivenessAsync(matchedRuleId.Value, cancellationToken);
    }

    private async Task<OwnerSuccessStats?> GetOwnerStatsAsNullable(
        string owner,
        int boardId,
        CancellationToken cancellationToken)
    {
        var stats = await GetOwnerSuccessStatsAsync(owner, boardId, cancellationToken);
        return stats.TotalCompleted == 0 ? null : stats;
    }

    private async Task<Dictionary<string, double>> ComputeSemanticSimilaritiesAsync(
        string ticketId,
        CancellationToken cancellationToken)
    {
        var current = await _db.TicketEmbeddings
            .AsNoTracking()
            .Where(e => e.TicketId == ticketId)
            .OrderByDescending(e => e.UpdatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (current is null)
        {
            return [];
        }

        var currentVector = TryParseVector(current.VectorJson);
        if (currentVector is null)
        {
            return [];
        }

        var others = await _db.TicketEmbeddings
            .AsNoTracking()
            .Where(e => e.TicketId != ticketId)
            .ToListAsync(cancellationToken);

        var result = new Dictionary<string, double>(others.Count);
        foreach (var embedding in others)
        {
            var vector = TryParseVector(embedding.VectorJson);
            if (vector is null)
            {
                continue;
            }

            var sim = CosineSimilarity(currentVector, vector);
            if (!result.TryGetValue(embedding.TicketId, out var existing) || sim > existing)
            {
                result[embedding.TicketId] = sim;
            }
        }

        return result;
    }

    private static float[]? TryParseVector(string? vectorJson)
    {
        if (string.IsNullOrWhiteSpace(vectorJson))
        {
            return null;
        }

        var trimmed = vectorJson.Trim();
        if (string.Equals(trimmed, "[]", StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            var result = JsonSerializer.Deserialize<float[]>(trimmed);
            return result is { Length: > 0 } ? result : null;
        }
        catch
        {
            return null;
        }
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
        {
            return 0.0;
        }

        double dot = 0, magA = 0, magB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += (double)a[i] * b[i];
            magA += (double)a[i] * a[i];
            magB += (double)b[i] * b[i];
        }

        if (magA <= 0 || magB <= 0)
        {
            return 0.0;
        }

        return dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
    }

    private static double SafePercent(int numerator, int denominator)
    {
        if (denominator <= 0)
        {
            return 0.0;
        }

        return Math.Round(100.0 * numerator / denominator, 2);
    }

    private static string ConfidenceFromSample(
        int sampleSize,
        double consistencyPercent,
        int strongSampleFloor,
        int mediumSampleFloor,
        double strongSuccessFloor,
        double mediumSuccessFloor)
    {
        if (sampleSize >= strongSampleFloor && consistencyPercent >= strongSuccessFloor)
        {
            return "High";
        }

        if (sampleSize >= mediumSampleFloor && consistencyPercent >= mediumSuccessFloor)
        {
            return "Medium";
        }

        return "Low";
    }

    private static string HashIds(IEnumerable<string> ids)
    {
        var ordered = ids.OrderBy(id => id, StringComparer.Ordinal);
        var joined = string.Join(',', ordered);
        if (joined.Length == 0)
        {
            return "none";
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(joined));
        return Convert.ToHexString(bytes[..6]);
    }
}
