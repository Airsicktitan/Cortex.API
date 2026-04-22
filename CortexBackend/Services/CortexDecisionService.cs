using Cortex.API.Database;
using Cortex.API.Data;
using Cortex.API.DTO;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Services;

public sealed class CortexDecisionService(
    CortexDbContext dbContext,
    ICortexCandidateResolutionService candidateResolutionService,
    IWorkloadSnapshotService workloadSnapshotService,
    ICortexAiAssessmentService cortexAiAssessmentService,
    ITicketRepository ticketRepository,
    ITicketRoutingRuleService ticketRoutingRuleService,
    IRealtimeEventService realtimeEventService,
    IRealtimeAudienceResolver realtimeAudienceResolver) : ICortexDecisionService
{
    private const int MeaningfulImprovementThreshold = 10;

    public async Task<CortexDecisionResult> EvaluateAssignmentAsync(
        Ticket ticket,
        CortexAiAssessment? aiAssessment = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedAssessment = aiAssessment
            ?? await TryGetAssessmentAsync(ticket, cancellationToken);
        return await EvaluateCoreAsync(ticket, forRebalance: false, resolvedAssessment, cancellationToken);
    }

    public async Task<CortexDecisionResult> EvaluateRebalanceAsync(
        Ticket ticket,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var assessment = await TryGetAssessmentAsync(ticket, cancellationToken);
            return await EvaluateCoreAsync(ticket, forRebalance: true, aiAssessment: assessment, cancellationToken);
        }
        catch
        {
            // Rebalance must stay available even if enrichment fails.
            return await EvaluateCoreAsync(ticket, forRebalance: true, aiAssessment: null, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<RebalanceSuggestion>> GetRebalanceSuggestionsAsync(
        CancellationToken cancellationToken = default)
    {
        var resolvedStatuses = TicketStatusFilters.ResolvedStatusesUpper;
        var activeTickets = await dbContext.Tickets
            .AsNoTracking()
            .Where(ticket => ticket.ApprovalStatus == ApprovalStatus.Approved)
            .Where(ticket => ticket.Status == null || !resolvedStatuses.Contains(ticket.Status.ToUpper()))
            .Where(ticket => !dbContext.ArchivedTickets.Any(archived => archived.Id == ticket.Id))
            .Where(ticket => !string.IsNullOrWhiteSpace(ticket.SynitiOwner))
            .ToListAsync(cancellationToken);
        var snapshots = await workloadSnapshotService.GetSnapshotsAsync(cancellationToken);
        var overloadSet = snapshots
            .Where(snapshot => snapshot.Status == "Overloaded")
            .Select(snapshot => snapshot.UserId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ownerAliases = OwnerFieldResolution.BuildAliasLookup(await dbContext.Users
            .AsNoTracking()
            .ToListAsync(cancellationToken));

        var suggestions = new List<RebalanceSuggestion>();
        foreach (var ticket in activeTickets)
        {
            var currentOwnerKey = OwnerFieldResolution.CanonicalizeOwnerField(ticket.SynitiOwner, ownerAliases);
            if (string.IsNullOrWhiteSpace(currentOwnerKey) || !overloadSet.Contains(currentOwnerKey))
            {
                continue;
            }

            try
            {
                var decision = await EvaluateRebalanceAsync(ticket, cancellationToken);
                if (decision.DecisionType != "RecommendRebalance"
                    || string.IsNullOrWhiteSpace(decision.RecommendedOwnerUserId)
                    || IsSameOwner(
                        currentOwnerKey,
                        decision.RecommendedOwnerUserId,
                        decision.RecommendedOwnerDisplayName))
                {
                    continue;
                }

                suggestions.Add(new RebalanceSuggestion
                {
                    TicketId = ticket.Id,
                    TicketKey = ticket.Id,
                    FromUserId = currentOwnerKey,
                    FromDisplayName = ResolveOwnerDisplayName(currentOwnerKey, ownerAliases),
                    ToUserId = decision.RecommendedOwnerUserId ?? string.Empty,
                    ToDisplayName = decision.RecommendedOwnerDisplayName ?? decision.RecommendedOwnerUserId ?? string.Empty,
                    Reason = decision.Summary,
                    AiHighRisk = string.Equals(decision.AiRiskLevel, "High", StringComparison.OrdinalIgnoreCase),
                    ExpectedImpact = ResolveExpectedImpact(decision)
                });
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Degrade gracefully: skip only this ticket, keep endpoint healthy.
                continue;
            }
        }

        return suggestions
            .OrderByDescending(suggestion => suggestion.AiHighRisk)
            .ThenBy(suggestion => suggestion.TicketId, StringComparer.Ordinal)
            .Take(5)
            .ToList();
    }

    public async Task<ExecuteRebalanceResponse> ExecuteRebalanceAsync(
        CancellationToken cancellationToken = default)
    {
        var suggestions = await GetRebalanceSuggestionsAsync(cancellationToken);
        var beforeSnapshots = await workloadSnapshotService.GetSnapshotsAsync(cancellationToken);
        var response = new ExecuteRebalanceResponse
        {
            TotalEvaluated = suggestions.Count
        };

        foreach (var suggestion in suggestions)
        {
            try
            {
                var ticket = await ticketRepository.GetTicketByIdAsync(suggestion.TicketId);
                if (ticket is null)
                {
                    response.Skipped.Add(new SkippedRebalance
                    {
                        TicketId = suggestion.TicketId,
                        Reason = "Ticket was not found."
                    });
                    continue;
                }

                var latestOverride = await ticketRoutingRuleService.GetLatestOverrideAsync(ticket.Id, cancellationToken);
                if (latestOverride is not null
                    && !string.IsNullOrWhiteSpace(latestOverride.NewSynitiOwner)
                    && string.Equals(
                        latestOverride.NewSynitiOwner.Trim(),
                        ticket.SynitiOwner?.Trim(),
                        StringComparison.OrdinalIgnoreCase))
                {
                    response.Skipped.Add(new SkippedRebalance
                    {
                        TicketId = suggestion.TicketId,
                        Reason = "Manual override exists; skipped."
                    });
                    continue;
                }

                var decision = await EvaluateRebalanceAsync(ticket, cancellationToken);
                if (!string.Equals(decision.RecommendedOwnerUserId, suggestion.ToUserId, StringComparison.OrdinalIgnoreCase))
                {
                    response.Skipped.Add(new SkippedRebalance
                    {
                        TicketId = suggestion.TicketId,
                        Reason = "Suggestion became stale after re-evaluation."
                    });
                    continue;
                }

                if (string.IsNullOrWhiteSpace(decision.RecommendedOwnerUserId))
                {
                    response.Skipped.Add(new SkippedRebalance
                    {
                        TicketId = suggestion.TicketId,
                        Reason = "No recommended owner is currently available."
                    });
                    continue;
                }

                if (string.Equals(ticket.SynitiOwner, decision.RecommendedOwnerUserId, StringComparison.OrdinalIgnoreCase))
                {
                    response.Skipped.Add(new SkippedRebalance
                    {
                        TicketId = suggestion.TicketId,
                        Reason = "Ticket is already assigned to the recommended owner."
                    });
                    continue;
                }

                var fromOwner = ticket.SynitiOwner ?? string.Empty;
                ticket.SynitiOwner = decision.RecommendedOwnerUserId;
                ticket.LastModifiedDate = DateTime.UtcNow;
                await ticketRepository.UpdateTicketAsync(ticket);
                await ticketRepository.SaveChangesAsync();

                response.Applied.Add(new AppliedRebalance
                {
                    TicketId = ticket.Id,
                    TicketKey = ticket.Id,
                    FromUserId = fromOwner,
                    ToUserId = decision.RecommendedOwnerUserId,
                    Reason = decision.Summary
                });

                var audienceUserIds = await realtimeAudienceResolver.GetAudienceUserIdsAsync(ticket, cancellationToken);
                await realtimeEventService.PublishAsync(new RealtimeEventMessage
                {
                    EventType = "ticket.updated",
                    TicketId = ticket.Id,
                    EntityId = ticket.Id,
                    AudienceUserIds = audienceUserIds
                }, cancellationToken);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                response.Skipped.Add(new SkippedRebalance
                {
                    TicketId = suggestion.TicketId,
                    Reason = "Unexpected execution error."
                });
            }
        }

        response.TotalApplied = response.Applied.Count;
        var afterSnapshots = await workloadSnapshotService.GetSnapshotsAsync(cancellationToken);
        response.ImpactDetails = BuildImpactDetails(response, suggestions, beforeSnapshots, afterSnapshots);
        response.Summary = BuildExecuteSummary(response);
        return response;
    }

    private async Task<CortexDecisionResult> EvaluateCoreAsync(
        Ticket ticket,
        bool forRebalance,
        CortexAiAssessment? aiAssessment,
        CancellationToken cancellationToken)
    {
        var candidates = (await candidateResolutionService.GetEligibleCandidatesAsync(ticket, cancellationToken))
            .Where(candidate => candidate.Eligible)
            .ToList();

        if (forRebalance && !string.IsNullOrWhiteSpace(ticket.SynitiOwner))
        {
            var currentOwner = ticket.SynitiOwner;
            candidates = candidates
                .Where(candidate => !IsSameOwner(currentOwner, candidate.UserId, candidate.DisplayName))
                .ToList();
        }

        if (candidates.Count == 0)
        {
            return new CortexDecisionResult
            {
                DecisionType = "NoEligibleOwner",
                CurrentOwnerUserId = ticket.SynitiOwner,
                Summary = "No valid owner could be determined for this ticket.",
                ConfidenceScore = 0m,
                Reasons = ["No active eligible owners matched routing criteria."],
                Warnings = ["Assign manually and review eligibility setup."],
                FactorBreakdown = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["routingRule"] = "no-eligible-candidates",
                    ["workloadComparison"] = "unavailable",
                    ["slaProtection"] = "unavailable"
                }
            };
        }

        foreach (var candidate in candidates)
        {
            var score = 100;
            score -= candidate.WorkloadScore * 5;
            score -= candidate.SlaRiskCount * 8;
            score -= candidate.HighPriorityCount * 4;
            if (candidate.RuleMatched)
            {
                score += 15;
            }
            if (candidate.PreferredByBoard)
            {
                score += 10;
            }
            if (!string.IsNullOrWhiteSpace(ticket.SynitiOwner)
                && ticket.SynitiOwner.Equals(candidate.UserId, StringComparison.OrdinalIgnoreCase))
            {
                score += 5;
            }
            if (candidate.CurrentlyOverloaded)
            {
                score -= 25;
            }

            if (aiAssessment is not null
                && string.Equals(aiAssessment.RiskLevel, "High", StringComparison.OrdinalIgnoreCase)
                && candidate.CurrentlyOverloaded)
            {
                score -= 8;
                candidate.Notes.Add("High AI risk ticket: overloaded owners receive additional penalty.");
            }

            if (aiAssessment is not null
                && !string.IsNullOrWhiteSpace(aiAssessment.RecommendedOwnerUserId)
                && candidate.UserId.Equals(
                    aiAssessment.RecommendedOwnerUserId.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                score += 10;
                candidate.Notes.Add("Soft signal: unified AI assessment favored this eligible owner (+10).");
            }

            candidate.TotalScore = score;
        }

        var ranked = candidates
            .OrderByDescending(candidate => candidate.TotalScore)
            .ThenBy(candidate => candidate.WorkloadScore)
            .ThenBy(candidate => candidate.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var winner = ranked[0];
        var runnerUp = ranked.Count > 1 ? ranked[1] : null;
        var current = ranked.FirstOrDefault(candidate =>
            !string.IsNullOrWhiteSpace(ticket.SynitiOwner)
            && IsSameOwner(ticket.SynitiOwner, candidate.UserId, candidate.DisplayName));

        var decisionType = ResolveDecisionType(ticket, winner, current, forRebalance);
        var confidence = ResolveConfidence(ranked);
        var reasons = new List<string>
        {
            $"{winner.DisplayName} has workload score {winner.WorkloadScore}."
        };
        if (current is not null && !current.UserId.Equals(winner.UserId, StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add($"{winner.DisplayName} scored {winner.TotalScore} versus {current.DisplayName} at {current.TotalScore}.");
        }
        if (winner.SlaRiskCount == 0)
        {
            reasons.Add($"{winner.DisplayName} currently has no at-risk tickets.");
        }
        if (winner.RuleMatched)
        {
            reasons.Add("Ticket matches the board routing criteria.");
        }

        if (!forRebalance && aiAssessment is not null)
        {
            reasons.Add(
                $"AI intake signals — suggested priority: {aiAssessment.RecommendedPriority}, risk: {aiAssessment.RiskLevel}.");
        }

        var warnings = new List<string>();
        if (winner.CurrentlyOverloaded)
        {
            warnings.Add("Recommended owner is nearing overload threshold.");
        }
        if (runnerUp is null)
        {
            warnings.Add("No low-pressure alternative was available.");
        }

        return new CortexDecisionResult
        {
            DecisionType = decisionType,
            RecommendedOwnerUserId = winner.UserId,
            RecommendedOwnerDisplayName = winner.DisplayName,
            CurrentOwnerUserId = ticket.SynitiOwner,
            Summary = CortexInsightNarrativeBuilder.BuildCortexInsightSummary(
                aiAssessment,
                winner,
                current,
                decisionType),
            ConfidenceScore = confidence,
            Reasons = reasons.Take(3).ToList(),
            Warnings = warnings,
            Candidates = ranked,
            AiSummary = aiAssessment?.Summary,
            AiRiskLevel = aiAssessment?.RiskLevel,
            AiConfidence = aiAssessment?.ConfidenceScore,
            AiRecommendedPriority = aiAssessment?.RecommendedPriority,
            AiRecommendedOwner = aiAssessment?.RecommendedOwnerUserId,
            FactorBreakdown = BuildFactorBreakdown(
                winner,
                current,
                forRebalance,
                aiAssessment)
        };
    }

    private static Dictionary<string, string> BuildFactorBreakdown(
        CortexDecisionCandidate winner,
        CortexDecisionCandidate? current,
        bool forRebalance,
        CortexAiAssessment? aiAssessment)
    {
        var factor = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["routingRule"] = winner.RuleMatched ? "matched" : "not-matched",
            ["workloadComparison"] = current is null || winner.WorkloadScore <= current.WorkloadScore
                ? "recommended owner has lower workload"
                : "recommended owner has higher workload",
            ["slaProtection"] = current is null || winner.SlaRiskCount <= current.SlaRiskCount
                ? "recommended owner has lower SLA exposure"
                : "recommended owner has higher SLA exposure",
        };

        if (!forRebalance && aiAssessment is not null)
        {
            factor["aiRecommendedPriority"] = string.IsNullOrWhiteSpace(aiAssessment.RecommendedPriority)
                ? "(none)"
                : aiAssessment.RecommendedPriority;
            factor["aiRiskLevel"] = string.IsNullOrWhiteSpace(aiAssessment.RiskLevel)
                ? "(none)"
                : aiAssessment.RiskLevel;
        }

        return factor;
    }

    private static bool IsSameOwner(
        string? currentOwner,
        string? candidateUserId,
        string? candidateDisplayName)
    {
        var current = NormalizeOwnerToken(currentOwner);
        if (current.Length == 0)
        {
            return false;
        }

        var byUserId = NormalizeOwnerToken(candidateUserId);
        if (byUserId.Length > 0 && string.Equals(current, byUserId, StringComparison.Ordinal))
        {
            return true;
        }

        var byDisplayName = NormalizeOwnerToken(candidateDisplayName);
        return byDisplayName.Length > 0
            && string.Equals(current, byDisplayName, StringComparison.Ordinal);
    }

    private static string NormalizeOwnerToken(string? value)
    {
        return (value ?? string.Empty).Trim().ToUpperInvariant();
    }

    private static string ResolveOwnerDisplayName(
        string ownerKey,
        IReadOnlyDictionary<string, User> ownerAliases)
    {
        var user = OwnerFieldResolution.ResolveUser(ownerKey, ownerAliases);
        if (user is null)
        {
            return ownerKey;
        }

        if (!string.IsNullOrWhiteSpace(user.DisplayName))
        {
            return user.DisplayName.Trim();
        }

        return string.IsNullOrWhiteSpace(user.Email)
            ? ownerKey
            : user.Email.Trim();
    }

    private async Task<CortexAiAssessment?> TryGetAssessmentAsync(
        Ticket ticket,
        CancellationToken cancellationToken)
    {
        try
        {
            return await cortexAiAssessmentService.AssessTicketAsync(ticket, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private static string ResolveDecisionType(
        Ticket ticket,
        CortexDecisionCandidate winner,
        CortexDecisionCandidate? current,
        bool forRebalance)
    {
        if (string.IsNullOrWhiteSpace(ticket.SynitiOwner))
        {
            return "Assign";
        }

        if (current is null || winner.TotalScore >= current.TotalScore + MeaningfulImprovementThreshold)
        {
            return forRebalance ? "RecommendRebalance" : "Assign";
        }

        return "KeepCurrentOwner";
    }

    private static decimal ResolveConfidence(IReadOnlyList<CortexDecisionCandidate> ranked)
    {
        if (ranked.Count == 0)
        {
            return 0m;
        }

        if (ranked.Count == 1)
        {
            return 0.9m;
        }

        var top = ranked[0].TotalScore;
        var second = ranked[1].TotalScore;
        var gap = Math.Max(0, top - second);
        var normalized = Math.Min(1d, gap / 40d);
        return Math.Round((decimal)normalized, 2, MidpointRounding.AwayFromZero);
    }

    private static string ResolveExpectedImpact(CortexDecisionResult decision)
    {
        if (string.Equals(decision.AiRiskLevel, "High", StringComparison.OrdinalIgnoreCase))
        {
            return "Reduces SLA risk identified by AI and moves high-risk work to available capacity.";
        }

        return decision.Candidates.FirstOrDefault()?.SlaRiskCount > 0
            ? "Lowers SLA concentration on current owner."
            : "Reduces workload imbalance.";
    }

    private static string BuildExecuteSummary(ExecuteRebalanceResponse response)
    {
        return response.TotalApplied == 0
            ? "No rebalance actions were applied."
            : $"Rebalanced {response.TotalApplied} tickets to reduce workload imbalance and SLA risk.";
    }

    private static List<string> BuildImpactDetails(
        ExecuteRebalanceResponse response,
        IReadOnlyList<RebalanceSuggestion> suggestions,
        IReadOnlyList<WorkloadSnapshot> beforeSnapshots,
        IReadOnlyList<WorkloadSnapshot> afterSnapshots)
    {
        var details = new List<string>();

        var highRiskMoved = response.Applied.Count(applied =>
            suggestions.Any(suggestion =>
                suggestion.TicketId == applied.TicketId
                && suggestion.ToUserId == applied.ToUserId
                && suggestion.AiHighRisk));
        if (highRiskMoved > 0)
        {
            details.Add($"{highRiskMoved} high-risk ticket moved off overloaded owner.");
        }

        var overloadedBefore = beforeSnapshots.Count(snapshot => snapshot.Status == "Overloaded");
        var overloadedAfter = afterSnapshots.Count(snapshot => snapshot.Status == "Overloaded");
        if (overloadedAfter < overloadedBefore)
        {
            details.Add($"Workload imbalance reduced across {overloadedBefore - overloadedAfter} users.");
        }
        else if (response.TotalApplied > 0)
        {
            details.Add("Workload redistribution applied across active owners.");
        }

        return details;
    }
}
