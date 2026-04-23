using System.Diagnostics;
using Cortex.API.Database;
using Cortex.API.Data;
using Cortex.API.DTO;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cortex.API.Services;

public sealed class CortexDecisionService(
    CortexDbContext dbContext,
    ICortexCandidateResolutionService candidateResolutionService,
    IWorkloadSnapshotService workloadSnapshotService,
    ICortexAiAssessmentService cortexAiAssessmentService,
    ITicketRepository ticketRepository,
    ITicketRoutingRuleService ticketRoutingRuleService,
    IRealtimeEventService realtimeEventService,
    IRealtimeAudienceResolver realtimeAudienceResolver,
    ILogger<CortexDecisionService> logger) : ICortexDecisionService
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
        var totalStarted = Stopwatch.GetTimestamp();
        var resolvedStatuses = TicketStatusFilters.ResolvedStatusesUpper;
        var ticketLoadStarted = Stopwatch.GetTimestamp();
        var activeTickets = await dbContext.Tickets
            .AsNoTracking()
            .Where(ticket => ticket.ApprovalStatus == ApprovalStatus.Approved)
            .Where(ticket => ticket.Status == null || !resolvedStatuses.Contains(ticket.Status.ToUpper()))
            .Where(ticket => !dbContext.ArchivedTickets.Any(archived => archived.Id == ticket.Id))
            .Where(ticket => !string.IsNullOrWhiteSpace(ticket.SynitiOwner))
            .OrderBy(ticket => ticket.Id)
            .ToListAsync(cancellationToken);
        var ticketLoadMs = ElapsedMilliseconds(ticketLoadStarted);

        var snapshotLoadStarted = Stopwatch.GetTimestamp();
        var snapshots = await workloadSnapshotService.GetSnapshotsAsync(cancellationToken);
        var snapshotLoadMs = ElapsedMilliseconds(snapshotLoadStarted);
        var overloadSet = snapshots
            .Where(snapshot => snapshot.Status == "Overloaded")
            .Select(snapshot => snapshot.UserId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ownerAliasLoadStarted = Stopwatch.GetTimestamp();
        var ownerAliases = OwnerFieldResolution.BuildAliasLookup(await dbContext.Users
            .AsNoTracking()
            .ToListAsync(cancellationToken));
        var ownerAliasLoadMs = ElapsedMilliseconds(ownerAliasLoadStarted);
        var activeTicketIds = activeTickets
            .Select(ticket => ticket.Id)
            .ToList();
        var relevantOverrides = await dbContext.TicketRoutingOverrides
            .AsNoTracking()
            .Where(overrideEntry => activeTicketIds.Contains(overrideEntry.TicketId))
            .ToListAsync(cancellationToken);
        var latestOverridesByTicketId = relevantOverrides
            .GroupBy(overrideEntry => overrideEntry.TicketId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(overrideEntry => overrideEntry.CreatedDateUtc)
                .First())
            .ToDictionary(
                overrideEntry => overrideEntry.TicketId,
                overrideEntry => overrideEntry,
                StringComparer.OrdinalIgnoreCase);

        var suggestions = new List<RebalanceSuggestion>();
        var overloadedTicketCount = 0;
        var evaluatedTicketCount = 0;
        var evaluationStarted = Stopwatch.GetTimestamp();
        foreach (var ticket in activeTickets)
        {
            var currentOwnerKey = OwnerFieldResolution.CanonicalizeOwnerField(ticket.SynitiOwner, ownerAliases);
            if (string.IsNullOrWhiteSpace(currentOwnerKey) || !overloadSet.Contains(currentOwnerKey))
            {
                continue;
            }

            overloadedTicketCount++;
            try
            {
                var decision = await EvaluateRebalanceDeterministicAsync(ticket, cancellationToken);
                evaluatedTicketCount++;
                if (decision.DecisionType != "RecommendRebalance"
                    || string.IsNullOrWhiteSpace(decision.RecommendedOwnerUserId)
                    || IsSameOwner(
                        currentOwnerKey,
                        decision.RecommendedOwnerUserId,
                        decision.RecommendedOwnerDisplayName))
                {
                    continue;
                }

                var currentOwnerSnapshot = ResolveSnapshot(currentOwnerKey, snapshots);
                var recommendedCandidate = ResolveRecommendedCandidate(decision);
                var expectedImpact = ResolveExpectedImpact(decision);
                var sourceDisplayName = ResolveOwnerDisplayName(currentOwnerKey, ownerAliases);
                var latestOverride = latestOverridesByTicketId.GetValueOrDefault(ticket.Id);
                var isBlockedByManualOverride =
                    latestOverride is not null
                    && !string.IsNullOrWhiteSpace(latestOverride.NewSynitiOwner)
                    && string.Equals(
                        latestOverride.NewSynitiOwner.Trim(),
                        ticket.SynitiOwner?.Trim(),
                        StringComparison.OrdinalIgnoreCase);

                suggestions.Add(new RebalanceSuggestion
                {
                    TicketId = ticket.Id,
                    TicketKey = ticket.Id,
                    TicketTitle = ResolveTicketTitle(ticket),
                    FromUserId = currentOwnerKey,
                    FromDisplayName = sourceDisplayName,
                    ToUserId = decision.RecommendedOwnerUserId ?? string.Empty,
                    ToDisplayName = decision.RecommendedOwnerDisplayName ?? decision.RecommendedOwnerUserId ?? string.Empty,
                    Reason = decision.Summary,
                    ConfidenceScore = decision.ConfidenceScore,
                    RecommendationStrength = ResolveRecommendationStrength(decision.ConfidenceScore),
                    Rationale = BuildRecommendationRationale(
                        ticket,
                        sourceDisplayName,
                        currentOwnerSnapshot,
                        decision,
                        recommendedCandidate),
                    ImpactPreview = BuildImpactPreview(
                        sourceDisplayName,
                        currentOwnerSnapshot,
                        decision,
                        recommendedCandidate),
                    AlternativeOwners = decision.Candidates
                        .Where(candidate => !string.Equals(
                            candidate.UserId,
                            decision.RecommendedOwnerUserId,
                            StringComparison.OrdinalIgnoreCase))
                        .Take(2)
                        .Select(candidate => new RebalanceSuggestionAlternative
                        {
                            UserId = candidate.UserId,
                            DisplayName = candidate.DisplayName,
                            WorkloadScore = candidate.WorkloadScore,
                            PressureLevel = WorkloadScoringPolicy.ToPressureLevel(candidate.WorkloadScore),
                        })
                        .ToList(),
                    AiHighRisk = string.Equals(decision.AiRiskLevel, "High", StringComparison.OrdinalIgnoreCase),
                    ExpectedImpact = expectedImpact,
                    IsBlockedByManualOverride = isBlockedByManualOverride,
                    BlockedReason = isBlockedByManualOverride
                        ? "Manual override exists and currently controls ticket ownership."
                        : null
                });
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Degrade gracefully: skip only this ticket, keep endpoint healthy.
                continue;
            }
        }
        var evaluationMs = ElapsedMilliseconds(evaluationStarted);

        logger.LogInformation(
            "Rebalance suggestions generated in {TotalElapsedMs}ms (tickets {TicketLoadMs}ms, snapshots {SnapshotLoadMs}ms, owner aliases {OwnerAliasLoadMs}ms, evaluations {EvaluationMs}ms). ActiveTickets={ActiveTicketCount}, OverloadedTickets={OverloadedTicketCount}, EvaluatedTickets={EvaluatedTicketCount}, Suggestions={SuggestionCount}.",
            ElapsedMilliseconds(totalStarted),
            ticketLoadMs,
            snapshotLoadMs,
            ownerAliasLoadMs,
            evaluationMs,
            activeTickets.Count,
            overloadedTicketCount,
            evaluatedTicketCount,
            suggestions.Count);

        return suggestions
            .OrderByDescending(suggestion => suggestion.AiHighRisk)
            .ThenBy(suggestion => suggestion.TicketId, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<ExecuteRebalanceResponse> ExecuteRebalanceAsync(
        IReadOnlyList<RebalanceSuggestion>? requestedSuggestions = null,
        IReadOnlySet<string>? confirmedManualOverrideTicketIds = null,
        bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        var suggestions = requestedSuggestions is { Count: > 0 }
            ? requestedSuggestions
                .Where(suggestion =>
                    !string.IsNullOrWhiteSpace(suggestion.TicketId)
                    && !string.IsNullOrWhiteSpace(suggestion.ToUserId))
                .GroupBy(suggestion => suggestion.TicketId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList()
            : await GetRebalanceSuggestionsAsync(cancellationToken);
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
                var manualOverrideConfirmed =
                    confirmedManualOverrideTicketIds?.Contains(ticket.Id) == true;
                if (latestOverride is not null
                    && !string.IsNullOrWhiteSpace(latestOverride.NewSynitiOwner)
                    && string.Equals(
                        latestOverride.NewSynitiOwner.Trim(),
                        ticket.SynitiOwner?.Trim(),
                        StringComparison.OrdinalIgnoreCase)
                    && !manualOverrideConfirmed)
                {
                    response.Skipped.Add(new SkippedRebalance
                    {
                        TicketId = suggestion.TicketId,
                        Reason = "Manual override exists; skipped."
                    });
                    continue;
                }

                var fromOwner = ticket.SynitiOwner ?? string.Empty;
                if (!dryRun)
                {
                    var decision = await EvaluateRebalanceDeterministicAsync(ticket, cancellationToken);

                    if (decision.Candidates.Count == 0)
                    {
                        response.Skipped.Add(new SkippedRebalance
                        {
                            TicketId = suggestion.TicketId,
                            Reason = "No recommended owner is currently available."
                        });
                        continue;
                    }

                    // Validate that the submitted target is still in the eligible candidate
                    // pool. Rank drift — a different candidate winning due to minor workload
                    // score changes between requests — does not make an explicit override
                    // stale. Only reject when the requested target has become ineligible.
                    var targetIsInPool = decision.Candidates.Any(c =>
                        string.Equals(c.UserId, suggestion.ToUserId, StringComparison.OrdinalIgnoreCase));

                    if (!targetIsInPool)
                    {
                        response.Skipped.Add(new SkippedRebalance
                        {
                            TicketId = suggestion.TicketId,
                            Reason = "Suggestion became stale after re-evaluation."
                        });
                        continue;
                    }

                    if (string.Equals(ticket.SynitiOwner, suggestion.ToUserId, StringComparison.OrdinalIgnoreCase))
                    {
                        response.Skipped.Add(new SkippedRebalance
                        {
                            TicketId = suggestion.TicketId,
                            Reason = "Ticket is already assigned to the recommended owner."
                        });
                        continue;
                    }

                    ticket.SynitiOwner = suggestion.ToUserId;
                    ticket.LastModifiedDate = DateTime.UtcNow;
                    await ticketRepository.UpdateTicketAsync(ticket);
                    await ticketRepository.SaveChangesAsync();

                    response.Applied.Add(new AppliedRebalance
                    {
                        TicketId = ticket.Id,
                        TicketKey = ticket.Id,
                        FromUserId = fromOwner,
                        ToUserId = suggestion.ToUserId,
                        Reason = suggestion.Reason
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
                else
                {
                    response.Applied.Add(new AppliedRebalance
                    {
                        TicketId = ticket.Id,
                        TicketKey = ticket.Id,
                        FromUserId = fromOwner,
                        ToUserId = suggestion.ToUserId,
                        Reason = suggestion.Reason
                    });
                }
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
        if (dryRun)
        {
            response.ImpactDetails = [];
            response.Summary = response.TotalApplied == 0
                ? "No executable rebalance actions are currently available."
                : $"{response.TotalApplied} executable rebalance action{(response.TotalApplied == 1 ? string.Empty : "s")} ready.";
        }
        else
        {
            var afterSnapshots = await workloadSnapshotService.GetSnapshotsAsync(cancellationToken);
            response.ImpactDetails = BuildImpactDetails(response, suggestions, beforeSnapshots, afterSnapshots);
            response.Summary = BuildExecuteSummary(response);
        }
        return response;
    }

    private Task<CortexDecisionResult> EvaluateRebalanceDeterministicAsync(
        Ticket ticket,
        CancellationToken cancellationToken)
    {
        return EvaluateCoreAsync(ticket, forRebalance: true, aiAssessment: null, cancellationToken);
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
            var score = 100m;
            score -= candidate.WorkloadScore * 5m;
            score -= candidate.SlaRiskCount * 8;
            score -= candidate.HighPriorityCount * 4;
            if (candidate.RuleMatched)
            {
                score += 15m;
            }
            if (candidate.PreferredByBoard)
            {
                score += 10m;
            }
            if (!string.IsNullOrWhiteSpace(ticket.SynitiOwner)
                && ticket.SynitiOwner.Equals(candidate.UserId, StringComparison.OrdinalIgnoreCase))
            {
                score += 5m;
            }
            if (candidate.CurrentlyOverloaded)
            {
                score -= 25m;
            }

            if (aiAssessment is not null
                && string.Equals(aiAssessment.RiskLevel, "High", StringComparison.OrdinalIgnoreCase)
                && candidate.CurrentlyOverloaded)
            {
                score -= 8m;
                candidate.Notes.Add("High AI risk ticket: overloaded owners receive additional penalty.");
            }

            if (aiAssessment is not null
                && !string.IsNullOrWhiteSpace(aiAssessment.RecommendedOwnerUserId)
                && candidate.UserId.Equals(
                    aiAssessment.RecommendedOwnerUserId.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                score += 10m;
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
            var workloadDelta = winner.WorkloadScore - current.WorkloadScore;
            reasons.Add(
                $"Workload difference vs current owner: {workloadDelta:+0.##;-0.##;0} ({winner.DisplayName}: {winner.WorkloadScore}, {current.DisplayName}: {current.WorkloadScore}).");
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

    private static string ResolveTicketTitle(Ticket ticket)
    {
        return string.IsNullOrWhiteSpace(ticket.Title)
            ? ticket.Id
            : ticket.Title.Trim();
    }

    private static WorkloadSnapshot? ResolveSnapshot(
        string ownerKey,
        IReadOnlyList<WorkloadSnapshot> snapshots)
    {
        return snapshots.FirstOrDefault(snapshot =>
            IsSameOwner(ownerKey, snapshot.UserId, snapshot.DisplayName));
    }

    private static CortexDecisionCandidate? ResolveRecommendedCandidate(
        CortexDecisionResult decision)
    {
        if (decision.Candidates.Count == 0)
        {
            return null;
        }

        return decision.Candidates.FirstOrDefault(candidate =>
                IsSameOwner(
                    decision.RecommendedOwnerUserId,
                    candidate.UserId,
                    candidate.DisplayName))
            ?? decision.Candidates[0];
    }

    private static string ResolveRecommendationStrength(decimal confidenceScore)
    {
        if (confidenceScore >= 0.7m)
        {
            return "Strong fit";
        }

        if (confidenceScore >= 0.35m)
        {
            return "Good fit";
        }

        return "Limited fit";
    }

    private static List<string> BuildRecommendationRationale(
        Ticket ticket,
        string sourceDisplayName,
        WorkloadSnapshot? sourceSnapshot,
        CortexDecisionResult decision,
        CortexDecisionCandidate? recommendedCandidate)
    {
        var rationale = new List<string>
        {
            BuildSourcePressureRationale(sourceDisplayName, sourceSnapshot),
            BuildTicketCandidateRationale(ticket, decision),
        };

        if (recommendedCandidate is not null)
        {
            rationale.Add(BuildTargetFitRationale(recommendedCandidate));
        }
        else if (!string.IsNullOrWhiteSpace(decision.RecommendedOwnerDisplayName))
        {
            rationale.Add($"{decision.RecommendedOwnerDisplayName} is the best available owner from the current routing evaluation.");
        }

        return rationale.Take(4).ToList();
    }

    private static string BuildSourcePressureRationale(
        string sourceDisplayName,
        WorkloadSnapshot? sourceSnapshot)
    {
        if (sourceSnapshot is null)
        {
            return $"{sourceDisplayName} is currently marked overloaded.";
        }

        var pressureSignals = new List<string>();
        if (sourceSnapshot.ActiveTicketCount > 0)
        {
            pressureSignals.Add($"{sourceSnapshot.ActiveTicketCount} active ticket{Pluralize(sourceSnapshot.ActiveTicketCount)}");
        }
        if (sourceSnapshot.SlaRiskCount > 0)
        {
            pressureSignals.Add($"{sourceSnapshot.SlaRiskCount} at SLA risk");
        }
        if (sourceSnapshot.HighPriorityCount > 0)
        {
            pressureSignals.Add($"{sourceSnapshot.HighPriorityCount} high priority");
        }

        if (pressureSignals.Count == 0 && sourceSnapshot.WorkloadScore == 0)
        {
            return $"{sourceDisplayName} is marked overloaded in the current workload snapshot.";
        }

        pressureSignals.Add($"workload score {sourceSnapshot.WorkloadScore}");
        return $"{sourceDisplayName} is overloaded: {string.Join(", ", pressureSignals)}.";
    }

    private static string BuildTicketCandidateRationale(
        Ticket ticket,
        CortexDecisionResult decision)
    {
        if (string.Equals(decision.AiRiskLevel, "High", StringComparison.OrdinalIgnoreCase))
        {
            return "Ticket carries elevated delivery risk and should not remain concentrated on an overloaded owner.";
        }

        if (string.Equals(ticket.Priority, "Critical", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ticket.Priority, "High", StringComparison.OrdinalIgnoreCase))
        {
            return $"{ticket.Priority} priority ticket is active work on an overloaded owner with a better owner fit available.";
        }

        return "Ticket is active work on an overloaded owner with a material owner-fit improvement available.";
    }

    private static string BuildTargetFitRationale(CortexDecisionCandidate recommendedCandidate)
    {
        var fitSignals = new List<string>
        {
            $"workload score {recommendedCandidate.WorkloadScore}",
        };

        if (recommendedCandidate.SlaRiskCount == 0)
        {
            fitSignals.Add("no current SLA-risk tickets");
        }
        else
        {
            fitSignals.Add($"{recommendedCandidate.SlaRiskCount} current SLA-risk ticket{Pluralize(recommendedCandidate.SlaRiskCount)}");
        }

        if (recommendedCandidate.RuleMatched)
        {
            fitSignals.Add("matches routing criteria");
        }

        return $"{recommendedCandidate.DisplayName} is an appropriate target: {string.Join(", ", fitSignals)}.";
    }

    private static List<string> BuildImpactPreview(
        string sourceDisplayName,
        WorkloadSnapshot? sourceSnapshot,
        CortexDecisionResult decision,
        CortexDecisionCandidate? recommendedCandidate)
    {
        var preview = new List<string>();

        if (sourceSnapshot?.SlaRiskCount > 0)
        {
            preview.Add($"Reduces SLA concentration on {sourceDisplayName}.");
        }

        if (string.Equals(decision.AiRiskLevel, "High", StringComparison.OrdinalIgnoreCase))
        {
            preview.Add("Lowers high-risk workload on an overloaded owner.");
        }

        if (sourceSnapshot is not null
            && recommendedCandidate is not null
            && recommendedCandidate.WorkloadScore < sourceSnapshot.WorkloadScore)
        {
            preview.Add(
                $"Moves work to lower-pressure capacity ({sourceSnapshot.WorkloadScore} to {recommendedCandidate.WorkloadScore} workload score).");
        }
        else
        {
            preview.Add("Moves work to a lower-pressure owner.");
        }

        preview.Add("Keeps the correction scoped to a specific ticket.");

        return preview.Distinct(StringComparer.Ordinal).Take(4).ToList();
    }

    private static string Pluralize(int count) => count == 1 ? string.Empty : "s";

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
        var gap = Math.Max(0m, top - second);
        var normalized = Math.Min(1m, gap / 40m);
        return Math.Round(normalized, 2, MidpointRounding.AwayFromZero);
    }

    private static double ElapsedMilliseconds(long startTimestamp) =>
        Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;

    private static string ResolveExpectedImpact(CortexDecisionResult decision)
    {
        if (string.Equals(decision.AiRiskLevel, "High", StringComparison.OrdinalIgnoreCase))
        {
            return "Lowers high-risk workload on overloaded owner and moves it to available capacity.";
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
