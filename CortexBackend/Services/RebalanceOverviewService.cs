using Cortex.API.Data;
using Cortex.API.Database;
using Cortex.API.DTO;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Services;

/// <summary>
/// Operational Rebalance v1 — aggregates SynitiOwner workload, identifies
/// overloaded owners, and surfaces prioritized tickets as rebalance
/// candidates. Insight-only: does not trigger any reassignment itself.
///
/// Scoring / risk / recommendation logic is fully delegated:
///   - OwnerWorkloadScoringService → numeric workload per owner
///   - OperationalRiskService     → per-ticket risk level
///   - ReassignmentRecommendationService → lower-risk alternative owners
///   - TicketSlaCalculator         → SLA status (at_risk / breached)
/// </summary>
public sealed class RebalanceOverviewService(
    CortexDbContext dbContext,
    ITicketVisibilityService ticketVisibilityService,
    ISlaConfigurationService slaConfigurationService,
    IOwnerWorkloadScoringService ownerWorkloadScoringService,
    IOperationalRiskService operationalRiskService,
    IReassignmentRecommendationService reassignmentRecommendationService,
    IUserRepository userRepository) : IRebalanceOverviewService
{
    /// <summary>Deterministic cap for v1; tune once we have usage signal.</summary>
    private const int MaxCandidates = 20;

    public async Task<RebalanceOverviewResponse> GetOverviewAsync(
        CancellationToken cancellationToken = default)
    {
        // 1. Pull the active, visible ticket universe. Mirrors the filter
        //    used by OwnerWorkloadScoringService so the rebalance view and
        //    the underlying scores are over the same ticket set.
        var visibility = await ticketVisibilityService.GetCurrentVisibilityAsync();
        var priorityMap = await slaConfigurationService.GetPriorityMapAsync();

        var rawTickets = await dbContext.Tickets
            .AsNoTracking()
            .Where(t => t.ApprovalStatus == ApprovalStatus.Approved)
            .Where(t => !dbContext.ArchivedTickets.Any(a => a.Id == t.Id))
            .ToListAsync(cancellationToken);

        var activeTickets = rawTickets
            .Where(t => !TicketSlaCalculator.IsResolvedStatus(t.Status))
            .Where(t => visibility.CanView(t))
            .ToList();
        var users = (await userRepository.GetAllUsersAsync()).ToList();
        var userByLookupKey = BuildUserLookup(users);

        // 2. Score every active eligible Syniti owner, including owners with
        //    zero assigned tickets, so capacity is represented consistently.
        var synitiOwnerKeys = users
            .Where(user => user.IsActive && user.IsSynitiOwnerEligible)
            .Select(OwnerFieldResolution.ToCanonicalOwnerKey)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (synitiOwnerKeys.Count == 0)
        {
            return new RebalanceOverviewResponse();
        }

        // 3. Delegate workload scoring — do not duplicate this logic.
        var ownerScores = await ownerWorkloadScoringService.GetScoresAsync(
            synitiOwnerKeys,
            excludeTicketId: null,
            respectCurrentVisibility: true,
            cancellationToken);
        var ownerScoreByKey = ownerScores.ToDictionary(
            score => score.OwnerKey,
            StringComparer.Ordinal);

        // 4. Identify overloaded owners — pressure high or critical only.
        var overloadedOwnerKeys = ownerScores
            .Where(score => IsOverloaded(ToPressureLevel(score.WorkloadScore)))
            .Select(score => score.OwnerKey)
            .ToHashSet(StringComparer.Ordinal);

        if (overloadedOwnerKeys.Count == 0)
        {
            return new RebalanceOverviewResponse();
        }

        // 5. Candidate pool = tickets whose SynitiOwner is overloaded.
        var candidatePoolTickets = activeTickets
            .Where(t => overloadedOwnerKeys.Contains(NormalizeOwner(t.SynitiOwner, userByLookupKey)))
            .ToList();

        // 6. Delegate per-ticket operational risk for the full candidate
        //    pool so we can compute both the HighRiskTicketCount rollup AND
        //    the rebalance filter in a single pass.
        var riskByTicket = await operationalRiskService.EvaluateBatchAsync(
            candidatePoolTickets,
            cancellationToken);

        // 7. Compute SLA level per candidate ticket (pure calculator — not
        //    scoring logic).
        var slaLevelByTicket = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var ticket in candidatePoolTickets)
        {
            priorityMap.TryGetValue(ticket.Priority ?? string.Empty, out var configuration);
            var snapshot = TicketSlaCalculator.Calculate(ticket, configuration);
            slaLevelByTicket[ticket.Id] = ToSlaLevel(snapshot.Status, snapshot.IsBreached);
        }

        // 8. Per-owner HighRiskTicketCount (independent of rebalance filter).
        var highRiskCountByOwner = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var ticket in candidatePoolTickets)
        {
            if (!riskByTicket.TryGetValue(ticket.Id, out var risk))
            {
                continue;
            }

            if (!IsHighOrCritical(risk.RiskLevel))
            {
                continue;
            }

            var ownerKey = NormalizeOwner(ticket.SynitiOwner, userByLookupKey);
            if (ownerKey.Length == 0)
            {
                continue;
            }

            highRiskCountByOwner[ownerKey] =
                highRiskCountByOwner.TryGetValue(ownerKey, out var existing)
                    ? existing + 1
                    : 1;
        }

        // 9. Filter to actionable candidates — high/critical op risk OR SLA risk.
        var filtered = new List<RankedCandidate>();
        foreach (var ticket in candidatePoolTickets)
        {
            if (!riskByTicket.TryGetValue(ticket.Id, out var risk))
            {
                continue;
            }

            var ownerKey = NormalizeOwner(ticket.SynitiOwner, userByLookupKey);
            if (!ownerScoreByKey.TryGetValue(ownerKey, out var ownerScore))
            {
                continue;
            }

            var slaLevel = slaLevelByTicket.GetValueOrDefault(ticket.Id, "safe");
            var opRiskLevel = risk.RiskLevel;

            var isActionable = IsHighOrCritical(opRiskLevel)
                || slaLevel is "at_risk" or "breached";
            if (!isActionable)
            {
                continue;
            }

            filtered.Add(new RankedCandidate(
                Ticket: ticket,
                OperationalRiskLevel: opRiskLevel,
                SlaRiskLevel: slaLevel,
                OwnerScore: ownerScore));
        }

        // 10. Deterministic ranking: opRisk desc → slaRisk desc → ownerWorkload
        //     desc → ticketId asc. Keep top N.
        var rankedTopN = filtered
            .OrderByDescending(c => RiskRank(c.OperationalRiskLevel))
            .ThenByDescending(c => SlaRank(c.SlaRiskLevel))
            .ThenByDescending(c => c.OwnerScore.WorkloadScore)
            .ThenBy(c => c.Ticket.Id, StringComparer.Ordinal)
            .Take(MaxCandidates)
            .ToList();

        // 11. Delegate reassignment target discovery for the final N only —
        //     avoids paying the routing-rule evaluation cost for filtered-out
        //     tickets.
        var recommendationsByTicket = rankedTopN.Count == 0
            ? new Dictionary<string, ReassignmentRecommendationResponse>(StringComparer.Ordinal)
            : (await reassignmentRecommendationService.EvaluateBatchAsync(
                    rankedTopN.Select(c => c.Ticket),
                    cancellationToken))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal);

        // 13. Build response.
        var overloadedOwners = ownerScores
            .Where(score => overloadedOwnerKeys.Contains(score.OwnerKey))
            .OrderByDescending(score => score.WorkloadScore)
            .ThenBy(score => score.OwnerKey, StringComparer.Ordinal)
            .Select(score => new OwnerWorkloadSummaryResponse
            {
                OwnerId = score.OwnerKey,
                OwnerName = ResolveDisplayName(score.OwnerKey, userByLookupKey),
                TotalOpenTickets = score.ActiveTicketCount,
                HighPriorityCount = score.HighPriorityTicketCount,
                OverdueTicketCount = score.OverdueTicketCount,
                SlaRiskCount = score.SlaRiskTicketCount,
                StaleTicketCount = score.StaleTicketCount,
                WorkloadScore = score.WorkloadScore,
                PressureLevel = ToPressureLevel(score.WorkloadScore),
                HighRiskTicketCount = highRiskCountByOwner.GetValueOrDefault(score.OwnerKey, 0),
            })
            .ToList();

        var candidates = rankedTopN
            .Select(ranked =>
            {
                var ownerKey = ranked.OwnerScore.OwnerKey;
                var ownerName = ResolveDisplayName(ownerKey, userByLookupKey);
                var ownerPressure = ToPressureLevel(ranked.OwnerScore.WorkloadScore);
                var recommendation = recommendationsByTicket.GetValueOrDefault(ranked.Ticket.Id);
                var betterTargets = recommendation?.SuggestedTargets
                    .Where(target => target.IsBetterThanCurrent)
                    .ToList() ?? [];
                var topTarget = betterTargets.FirstOrDefault();

                return new RebalanceCandidateResponse
                {
                    TicketId = ranked.Ticket.Id,
                    Title = ranked.Ticket.Title,
                    CurrentOwnerId = ownerKey,
                    CurrentOwnerName = ownerName,
                    CurrentOwnerWorkloadScore = ranked.OwnerScore.WorkloadScore,
                    CurrentOwnerPressureLevel = ownerPressure,
                    OperationalRiskLevel = ranked.OperationalRiskLevel,
                    SlaRiskLevel = ranked.SlaRiskLevel,
                    RecommendedTargetCount = betterTargets.Count,
                    TopSuggestedTarget = topTarget is null
                        ? null
                        : new RebalanceSuggestedTargetResponse
                        {
                            OwnerKey = topTarget.OwnerKey,
                            DisplayName = topTarget.DisplayName,
                            WorkloadScore = topTarget.WorkloadScore,
                            PressureLevel = topTarget.PressureLevel,
                        },
                    AlternativeTargets = betterTargets.Skip(1).Take(2)
                        .Select(target => new RebalanceSuggestedTargetResponse
                        {
                            OwnerKey = target.OwnerKey,
                            DisplayName = target.DisplayName,
                            WorkloadScore = target.WorkloadScore,
                            PressureLevel = target.PressureLevel,
                        })
                        .ToList(),
                    PotentialImpactSummary = BuildImpactSummary(topTarget, recommendation),
                };
            })
            .ToList();

        return new RebalanceOverviewResponse
        {
            OverloadedOwners = overloadedOwners,
            RebalanceCandidates = candidates,
        };
    }

    private sealed record RankedCandidate(
        Ticket Ticket,
        string OperationalRiskLevel,
        string SlaRiskLevel,
        OwnerWorkloadScoreSnapshot OwnerScore);

    private static string NormalizeOwner(
        string? value,
        IReadOnlyDictionary<string, User> userLookup)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = NormalizeForLookup(value);
        return normalized.Length > 0 && userLookup.TryGetValue(normalized, out var user)
            ? OwnerFieldResolution.ToCanonicalOwnerKey(user)
            : value.Trim();
    }

    private static string NormalizeForLookup(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

    private static bool IsOverloaded(string pressureLevel) =>
        pressureLevel is "high" or "critical";

    private static bool IsHighOrCritical(string riskLevel) =>
        riskLevel is "high" or "critical";

    /// <summary>
    /// Mirrors the pressure-level ladder used inside OperationalRiskService
    /// and ReassignmentRecommendationService. Intentionally a local helper —
    /// the threshold policy is the scoring pipeline's, not this service's.
    /// </summary>
    private static string ToPressureLevel(decimal workloadScore) =>
        WorkloadScoringPolicy.ToPressureLevel(workloadScore);

    private static string ToSlaLevel(string slaStatus, bool isBreached)
    {
        if (slaStatus == "Breached" || isBreached)
        {
            return "breached";
        }
        if (slaStatus == "At Risk")
        {
            return "at_risk";
        }
        return "safe";
    }

    private static int RiskRank(string level) => level switch
    {
        "critical" => 3,
        "high" => 2,
        "moderate" => 1,
        _ => 0,
    };

    private static int SlaRank(string level) => level switch
    {
        "breached" => 2,
        "at_risk" => 1,
        _ => 0,
    };

    private static Dictionary<string, User> BuildUserLookup(IEnumerable<User> users)
    {
        var lookup = new Dictionary<string, User>(StringComparer.OrdinalIgnoreCase);
        foreach (var user in users)
        {
            AddLookupIfMissing(lookup, user.DisplayName, user);
            AddLookupIfMissing(lookup, user.Email, user);
            AddLookupIfMissing(lookup, user.NickName, user);
            AddLookupIfMissing(lookup, OwnerFieldResolution.ToCanonicalOwnerKey(user), user);
        }
        return lookup;
    }

    private static void AddLookupIfMissing(
        Dictionary<string, User> lookup,
        string? key,
        User user)
    {
        var normalized = NormalizeForLookup(key);
        if (normalized.Length == 0 || lookup.ContainsKey(normalized))
        {
            return;
        }

        lookup[normalized] = user;
    }

    private static string ResolveDisplayName(
        string ownerKey,
        IReadOnlyDictionary<string, User> userLookup)
    {
        var normalized = NormalizeForLookup(ownerKey);
        if (normalized.Length > 0 && userLookup.TryGetValue(normalized, out var user))
        {
            if (!string.IsNullOrWhiteSpace(user.DisplayName))
            {
                return user.DisplayName.Trim();
            }
            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                return user.Email;
            }
        }

        return ownerKey;
    }

    private static string BuildImpactSummary(
        ReassignmentTargetResponse? topTarget,
        ReassignmentRecommendationResponse? recommendation)
    {
        if (topTarget is null)
        {
            if (recommendation is not null && !recommendation.ShouldSuggestReassignment)
            {
                return "No lower-risk alternative owners currently available.";
            }
            return "Review manually — no alternative owners surfaced.";
        }

        return string.IsNullOrWhiteSpace(topTarget.ImprovementReason)
            ? $"Better target available: {topTarget.DisplayName}."
            : topTarget.ImprovementReason;
    }
}
