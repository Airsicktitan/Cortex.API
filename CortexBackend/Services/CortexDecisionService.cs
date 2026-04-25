using System.Diagnostics;
using System.Text.Json;
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
    ISlaConfigurationService slaConfigurationService,
    IRebalanceAiAdvisoryService rebalanceAiAdvisoryService,
    ICortexAiAssessmentService cortexAiAssessmentService,
    ITicketRepository ticketRepository,
    ITicketRoutingRuleService ticketRoutingRuleService,
    IRealtimeEventService realtimeEventService,
    IRealtimeAudienceResolver realtimeAudienceResolver,
    ILogger<CortexDecisionService> logger) : ICortexDecisionService
{
    private const int MeaningfulImprovementThreshold = 10;
    private const decimal RebalanceIncomingPenalty = 18m;
    private const decimal RebalanceProgressiveIncomingPenalty = 6m;
    private const decimal RebalanceProjectedWorkloadPenaltyMultiplier = 5m;
    private const decimal RebalanceProjectedOverloadPenalty = 16m;
    private const string RebalanceAppliedEventType = "rebalance_applied";
    private static readonly TimeSpan RebalanceCooldownWindow = TimeSpan.FromHours(24);

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
        var priorityMap = await slaConfigurationService.GetPriorityMapAsync();
        var nowUtc = DateTime.UtcNow;
        var projectedLoads = BuildProjectedOwnerLoads(snapshots);
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
        var recentRebalanceMoves = await LoadRecentRebalanceMovesAsync(activeTicketIds, cancellationToken);
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
        var advisoryPackets = new List<RebalanceAiDecisionPacket>();
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

            if (recentRebalanceMoves.ContainsKey(ticket.Id))
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

                var ticketSignals = WorkloadScoringPolicy.EvaluateTicket(ticket, priorityMap, nowUtc);
                var selection = SelectDistributionAwareCandidate(
                    ticket,
                    decision.Candidates,
                    projectedLoads,
                    ticketSignals);
                if (selection.Selected is null
                    || IsSameOwner(
                        currentOwnerKey,
                        selection.Selected.Candidate.UserId,
                        selection.Selected.Candidate.DisplayName))
                {
                    continue;
                }

                var selectedScore = selection.Selected!;
                var selectedCandidate = selectedScore.Candidate;
                decision.RecommendedOwnerUserId = selectedCandidate.UserId;
                decision.RecommendedOwnerDisplayName = selectedCandidate.DisplayName;
                decision.ConfidenceScore = selection.ConfidenceScore;

                var currentOwnerSnapshot = ResolveSnapshot(currentOwnerKey, snapshots);
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

                var whyTicketBullets = BuildWhyTicketBullets(
                    ticket,
                    sourceDisplayName,
                    currentOwnerSnapshot,
                    decision);
                var whyOwnerBullets = BuildWhyOwnerBullets(selection);
                var impactBullets = BuildImpactPreview(
                    sourceDisplayName,
                    currentOwnerSnapshot,
                    decision,
                    selectedCandidate,
                    selection);
                var tradeoffBullets = BuildTradeoffBullets(selection);
                var safetyNotes = BuildSafetyNotes(isBlockedByManualOverride);

                var suggestion = new RebalanceSuggestion
                {
                    TicketId = ticket.Id,
                    TicketKey = ticket.Id,
                    TicketTitle = ResolveTicketTitle(ticket),
                    FromUserId = currentOwnerKey,
                    FromDisplayName = sourceDisplayName,
                    ToUserId = selectedCandidate.UserId,
                    ToDisplayName = selectedCandidate.DisplayName,
                    SelectedOwnerName = selectedCandidate.DisplayName,
                    PreviousOwnerName = sourceDisplayName,
                    Reason = selection.SelectionReason,
                    SelectionReason = selection.SelectionReason,
                    ConfidenceScore = selection.ConfidenceScore,
                    RecommendationStrength = ResolveRecommendationStrength(selection.ConfidenceScore),
                    Rationale = whyTicketBullets.Concat(whyOwnerBullets).Take(4).ToList(),
                    ImpactPreview = impactBullets,
                    WhyTicketBullets = whyTicketBullets,
                    WhyOwnerBullets = whyOwnerBullets,
                    ExpectedImpactBullets = impactBullets,
                    TradeoffBullets = tradeoffBullets,
                    SafetyNotes = safetyNotes,
                    AlternativeOwners = BuildSuggestionAlternatives(selection),
                    DiversificationApplied = selection.DiversificationApplied,
                    RawTopCandidateName = selection.RawTop?.Candidate.DisplayName ?? selectedCandidate.DisplayName,
                    FinalCandidateName = selectedCandidate.DisplayName,
                    CandidateRankBeforeDiversification = selectedScore.RawRank,
                    CandidateRankAfterDiversification = selectedScore.DistributionRank,
                    AiHighRisk = string.Equals(decision.AiRiskLevel, "High", StringComparison.OrdinalIgnoreCase),
                    ExpectedImpact = impactBullets.FirstOrDefault() ?? expectedImpact,
                    IsBlockedByManualOverride = isBlockedByManualOverride,
                    BlockedReason = isBlockedByManualOverride
                        ? "Manual override exists and currently controls ticket ownership."
                        : null
                };

                suggestions.Add(suggestion);
                advisoryPackets.Add(BuildAiDecisionPacket(
                    ticket,
                    suggestion,
                    selection,
                    currentOwnerSnapshot,
                    ticketSignals));
                ApplyProjectedRebalance(
                    currentOwnerKey,
                    sourceDisplayName,
                    selectedCandidate,
                    projectedLoads,
                    ticketSignals);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Degrade gracefully: skip only this ticket, keep endpoint healthy.
                continue;
            }
        }
        var evaluationMs = ElapsedMilliseconds(evaluationStarted);
        await ApplyAiAdvisoriesAsync(suggestions, advisoryPackets, cancellationToken);

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
        var recentRebalanceMoves = await LoadRecentRebalanceMovesAsync(
            suggestions.Select(suggestion => suggestion.TicketId),
            cancellationToken);
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
                        Reason = "Blocked: Rule conflict."
                    });
                    continue;
                }

                var fromOwner = ticket.SynitiOwner ?? string.Empty;
                if (!IsSameOwner(fromOwner, suggestion.FromUserId, suggestion.FromDisplayName))
                {
                    response.Skipped.Add(new SkippedRebalance
                    {
                        TicketId = suggestion.TicketId,
                        Reason = "Stale: Ticket changed since suggestion was generated."
                    });
                    continue;
                }

                if (recentRebalanceMoves.TryGetValue(ticket.Id, out var recentMove))
                {
                    response.Skipped.Add(new SkippedRebalance
                    {
                        TicketId = suggestion.TicketId,
                        Reason = IsSameOwner(suggestion.ToUserId, recentMove.FromOwner, null)
                            ? "Blocked: Recent rebalance would move this ticket back to its previous owner."
                            : "Blocked: Move would create workload ping-pong."
                    });
                    continue;
                }

                var decision = await EvaluateRebalanceDeterministicAsync(ticket, cancellationToken);

                if (decision.Candidates.Count == 0)
                {
                    response.Skipped.Add(new SkippedRebalance
                    {
                        TicketId = suggestion.TicketId,
                        Reason = "Blocked: Missing required data."
                    });
                    continue;
                }

                // Validate that the submitted target is still in the eligible candidate
                // pool. Rank drift does not make an explicit recommendation stale; only
                // reject when the displayed target can no longer be applied safely.
                var targetIsInPool = decision.Candidates.Any(c =>
                    IsSameOwner(suggestion.ToUserId, c.UserId, c.DisplayName));

                if (!targetIsInPool)
                {
                    response.Skipped.Add(new SkippedRebalance
                    {
                        TicketId = suggestion.TicketId,
                        Reason = "Blocked: Owner no longer eligible."
                    });
                    continue;
                }

                if (IsSameOwner(fromOwner, suggestion.ToUserId, suggestion.ToDisplayName))
                {
                    response.Skipped.Add(new SkippedRebalance
                    {
                        TicketId = suggestion.TicketId,
                        Reason = "Stale: Ticket already moved to the recommended owner."
                    });
                    continue;
                }

                if (!dryRun)
                {
                    ticket.SynitiOwner = suggestion.ToUserId;
                    ticket.LastModifiedDate = DateTime.UtcNow;
                    await ticketRepository.UpdateTicketAsync(ticket);
                    dbContext.WorkflowMetricEvents.Add(new WorkflowMetricEvent
                    {
                        EventType = RebalanceAppliedEventType,
                        OccurredUtc = DateTime.UtcNow,
                        TicketId = ticket.Id,
                        ActorUserId = null,
                        PayloadJson = JsonSerializer.Serialize(new RebalanceMovePayload
                        {
                            FromOwner = fromOwner,
                            ToOwner = suggestion.ToUserId
                        })
                    });
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

    private static Dictionary<string, ProjectedOwnerLoad> BuildProjectedOwnerLoads(
        IReadOnlyList<WorkloadSnapshot> snapshots)
    {
        var loads = new Dictionary<string, ProjectedOwnerLoad>(StringComparer.OrdinalIgnoreCase);
        foreach (var snapshot in snapshots)
        {
            var load = new ProjectedOwnerLoad
            {
                UserId = snapshot.UserId,
                DisplayName = snapshot.DisplayName,
                ActiveTicketCount = snapshot.ActiveTicketCount,
                HighPriorityCount = snapshot.HighPriorityCount,
                OverdueTicketCount = snapshot.OverdueTicketCount,
                SlaRiskCount = snapshot.SlaRiskCount,
                StaleTicketCount = snapshot.StaleTicketCount,
                WorkloadScore = snapshot.WorkloadScore,
            };
            AddProjectedLoadAlias(loads, snapshot.UserId, load);
            AddProjectedLoadAlias(loads, snapshot.DisplayName, load);
        }

        return loads;
    }

    private static RebalanceCandidateSelection SelectDistributionAwareCandidate(
        Ticket ticket,
        IReadOnlyList<CortexDecisionCandidate> candidates,
        IDictionary<string, ProjectedOwnerLoad> projectedLoads,
        TicketWorkloadSignals ticketSignals)
    {
        var rawRankedCandidates = candidates
            .Where(candidate => candidate.Eligible)
            .OrderByDescending(candidate => candidate.TotalScore)
            .ThenBy(candidate => candidate.WorkloadScore)
            .ThenBy(candidate => candidate.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (rawRankedCandidates.Count == 0)
        {
            return new RebalanceCandidateSelection();
        }

        var scored = rawRankedCandidates
            .Select((candidate, index) => ScoreDistributionCandidate(
                candidate,
                rawRank: index + 1,
                projectedLoads,
                ticketSignals))
            .ToList();

        var distributionRanked = scored
            .OrderByDescending(candidate => candidate.DistributionScore)
            .ThenBy(candidate => candidate.ProjectedWorkloadScore)
            .ThenBy(candidate => candidate.Candidate.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        for (var i = 0; i < distributionRanked.Count; i++)
        {
            distributionRanked[i].DistributionRank = i + 1;
        }

        var selected = distributionRanked[0];
        var rawTop = scored[0];
        var diversificationApplied = !IsSameOwner(
            rawTop.Candidate.UserId,
            selected.Candidate.UserId,
            selected.Candidate.DisplayName);

        return new RebalanceCandidateSelection
        {
            Selected = selected,
            RawTop = rawTop,
            DistributionRanked = distributionRanked,
            DiversificationApplied = diversificationApplied,
            ConfidenceScore = ResolveDistributionConfidence(distributionRanked),
            SelectionReason = BuildSelectionReason(ticket, selected, rawTop, distributionRanked, diversificationApplied),
        };
    }

    private static DistributionCandidateScore ScoreDistributionCandidate(
        CortexDecisionCandidate candidate,
        int rawRank,
        IDictionary<string, ProjectedOwnerLoad> projectedLoads,
        TicketWorkloadSignals ticketSignals)
    {
        var projected = ResolveProjectedLoad(candidate, projectedLoads);
        var projectedWorkloadScore = CalculateWorkloadScoreAfterAdding(projected, ticketSignals);
        var workloadDelta = projectedWorkloadScore - candidate.WorkloadScore;
        var incomingPenalty = CalculateIncomingPenalty(projected.IncomingRecommendationCount);
        var projectedOverloadPenalty =
            !candidate.CurrentlyOverloaded && WorkloadScoringPolicy.IsOverloaded(projectedWorkloadScore)
                ? RebalanceProjectedOverloadPenalty
                : 0m;
        var addedTicketRiskPenalty = 0m;
        if (ticketSignals.IsHighPriority)
        {
            addedTicketRiskPenalty += 4m;
        }
        if (ticketSignals.IsOverdue || ticketSignals.IsSlaRisk)
        {
            addedTicketRiskPenalty += 8m;
        }

        return new DistributionCandidateScore
        {
            Candidate = candidate,
            RawRank = rawRank,
            DistributionScore = candidate.TotalScore
                - (workloadDelta * RebalanceProjectedWorkloadPenaltyMultiplier)
                - incomingPenalty
                - projectedOverloadPenalty
                - addedTicketRiskPenalty,
            ProjectedWorkloadScore = projectedWorkloadScore,
            IncomingRecommendationCount = projected.IncomingRecommendationCount,
            IncomingPenalty = incomingPenalty,
            ProjectedOverload = WorkloadScoringPolicy.IsOverloaded(projectedWorkloadScore),
        };
    }

    private static decimal CalculateIncomingPenalty(int incomingRecommendationCount)
    {
        if (incomingRecommendationCount <= 0)
        {
            return 0m;
        }

        return (incomingRecommendationCount * RebalanceIncomingPenalty)
            + (incomingRecommendationCount * incomingRecommendationCount * RebalanceProgressiveIncomingPenalty);
    }

    private static decimal CalculateWorkloadScoreAfterAdding(
        ProjectedOwnerLoad load,
        TicketWorkloadSignals signals)
    {
        return WorkloadScoringPolicy.CalculateScore(
            load.ActiveTicketCount + 1,
            load.HighPriorityCount + (signals.IsHighPriority ? 1 : 0),
            load.OverdueTicketCount + (signals.IsOverdue ? 1 : 0),
            load.SlaRiskCount + (signals.IsSlaRisk ? 1 : 0),
            load.StaleTicketCount + (signals.IsStale ? 1 : 0));
    }

    private static ProjectedOwnerLoad ResolveProjectedLoad(
        CortexDecisionCandidate candidate,
        IDictionary<string, ProjectedOwnerLoad> projectedLoads)
    {
        var load = ResolveProjectedLoad(
            candidate.UserId,
            candidate.DisplayName,
            projectedLoads);
        if (load.ActiveTicketCount == 0
            && load.HighPriorityCount == 0
            && load.OverdueTicketCount == 0
            && load.SlaRiskCount == 0
            && load.StaleTicketCount == 0
            && candidate.WorkloadScore > 0)
        {
            load.ActiveTicketCount = candidate.ActiveTicketCount;
            load.HighPriorityCount = candidate.HighPriorityCount;
            load.OverdueTicketCount = candidate.OverdueTicketCount;
            load.SlaRiskCount = candidate.SlaRiskCount;
            load.StaleTicketCount = candidate.StaleTicketCount;
            load.WorkloadScore = candidate.WorkloadScore;
        }

        return load;
    }

    private static ProjectedOwnerLoad ResolveProjectedLoad(
        string ownerKey,
        string displayName,
        IDictionary<string, ProjectedOwnerLoad> projectedLoads)
    {
        if (!string.IsNullOrWhiteSpace(ownerKey)
            && projectedLoads.TryGetValue(ownerKey, out var byUserId))
        {
            return byUserId;
        }

        if (!string.IsNullOrWhiteSpace(displayName)
            && projectedLoads.TryGetValue(displayName, out var byDisplayName))
        {
            return byDisplayName;
        }

        var load = new ProjectedOwnerLoad
        {
            UserId = ownerKey,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? ownerKey : displayName,
        };
        AddProjectedLoadAlias(projectedLoads, ownerKey, load);
        AddProjectedLoadAlias(projectedLoads, displayName, load);
        return load;
    }

    private static void AddProjectedLoadAlias(
        IDictionary<string, ProjectedOwnerLoad> projectedLoads,
        string? alias,
        ProjectedOwnerLoad load)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            return;
        }

        var key = alias.Trim();
        if (!projectedLoads.ContainsKey(key))
        {
            projectedLoads.Add(key, load);
        }
    }

    private static void ApplyProjectedRebalance(
        string sourceOwnerKey,
        string sourceDisplayName,
        CortexDecisionCandidate selectedCandidate,
        IDictionary<string, ProjectedOwnerLoad> projectedLoads,
        TicketWorkloadSignals ticketSignals)
    {
        var source = ResolveProjectedLoad(sourceOwnerKey, sourceDisplayName, projectedLoads);
        ApplyProjectedTicketDelta(source, ticketSignals, direction: -1);

        var target = ResolveProjectedLoad(selectedCandidate, projectedLoads);
        ApplyProjectedTicketDelta(target, ticketSignals, direction: 1);
        target.IncomingRecommendationCount++;
    }

    private static void ApplyProjectedTicketDelta(
        ProjectedOwnerLoad load,
        TicketWorkloadSignals signals,
        int direction)
    {
        load.ActiveTicketCount = Math.Max(0, load.ActiveTicketCount + direction);
        if (signals.IsHighPriority)
        {
            load.HighPriorityCount = Math.Max(0, load.HighPriorityCount + direction);
        }
        if (signals.IsOverdue)
        {
            load.OverdueTicketCount = Math.Max(0, load.OverdueTicketCount + direction);
        }
        if (signals.IsSlaRisk)
        {
            load.SlaRiskCount = Math.Max(0, load.SlaRiskCount + direction);
        }
        if (signals.IsStale)
        {
            load.StaleTicketCount = Math.Max(0, load.StaleTicketCount + direction);
        }

        load.WorkloadScore = WorkloadScoringPolicy.CalculateScore(
            load.ActiveTicketCount,
            load.HighPriorityCount,
            load.OverdueTicketCount,
            load.SlaRiskCount,
            load.StaleTicketCount);
    }

    private static decimal ResolveDistributionConfidence(
        IReadOnlyList<DistributionCandidateScore> distributionRanked)
    {
        if (distributionRanked.Count == 0)
        {
            return 0m;
        }

        if (distributionRanked.Count == 1)
        {
            return 0.9m;
        }

        var gap = Math.Max(0m, distributionRanked[0].DistributionScore - distributionRanked[1].DistributionScore);
        return Math.Round(Math.Min(1m, gap / 40m), 2, MidpointRounding.AwayFromZero);
    }

    private static string BuildSelectionReason(
        Ticket ticket,
        DistributionCandidateScore selected,
        DistributionCandidateScore rawTop,
        IReadOnlyList<DistributionCandidateScore> distributionRanked,
        bool diversificationApplied)
    {
        if (diversificationApplied)
        {
            return $"Selected {selected.Candidate.DisplayName} after projected workload review; {rawTop.Candidate.DisplayName} was the isolated top scorer but already had {rawTop.IncomingRecommendationCount} incoming recommendation{Pluralize(rawTop.IncomingRecommendationCount)} in this run.";
        }

        if (distributionRanked.Count == 1)
        {
            return $"{selected.Candidate.DisplayName} is the only eligible lower-pressure candidate for {ResolveTicketTitle(ticket)}.";
        }

        if (selected.IncomingRecommendationCount > 0)
        {
            return $"{selected.Candidate.DisplayName} remains the strongest fit after projected workload penalties; alternatives are higher pressure or lower fit.";
        }

        return $"{selected.Candidate.DisplayName} has the best deterministic fit after workload, SLA, priority, routing, and projected batch pressure are considered.";
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

    private static List<string> BuildWhyTicketBullets(
        Ticket ticket,
        string sourceDisplayName,
        WorkloadSnapshot? sourceSnapshot,
        CortexDecisionResult decision)
    {
        var bullets = new List<string>
        {
            BuildSourcePressureRationale(sourceDisplayName, sourceSnapshot),
            BuildTicketCandidateRationale(ticket, decision),
        };

        if (!string.IsNullOrWhiteSpace(ticket.AiTriagePotentialSlaRisk))
        {
            bullets.Add($"AI advisory risk signal: {ticket.AiTriagePotentialSlaRisk.Trim()}.");
        }

        return bullets.Distinct(StringComparer.Ordinal).Take(4).ToList();
    }

    private static List<string> BuildWhyOwnerBullets(RebalanceCandidateSelection selection)
    {
        if (selection.Selected is null)
        {
            return [];
        }

        var selected = selection.Selected;
        var bullets = new List<string>
        {
            BuildTargetFitRationale(selected.Candidate),
            $"Projected workload after this recommendation is {selected.ProjectedWorkloadScore} with {selected.IncomingRecommendationCount} earlier incoming recommendation{Pluralize(selected.IncomingRecommendationCount)} in this run.",
        };

        if (selection.DiversificationApplied && selection.RawTop is not null)
        {
            bullets.Add($"Diversified from raw top candidate {selection.RawTop.Candidate.DisplayName} to avoid concentrating new work on one owner.");
        }
        else if (selection.DistributionRanked.Count == 1)
        {
            bullets.Add("Only eligible candidate surfaced for this ticket.");
        }
        else if (selected.IncomingRecommendationCount > 0)
        {
            bullets.Add("Repeated recommendation remains justified after projected workload and alternatives were compared.");
        }

        return bullets.Distinct(StringComparer.Ordinal).Take(4).ToList();
    }

    private static List<string> BuildTradeoffBullets(RebalanceCandidateSelection selection)
    {
        if (selection.Selected is null)
        {
            return [];
        }

        var selected = selection.Selected;
        var bullets = new List<string>();
        var bestAlternative = selection.DistributionRanked
            .FirstOrDefault(candidate => !IsSameOwner(
                selected.Candidate.UserId,
                candidate.Candidate.UserId,
                candidate.Candidate.DisplayName));

        if (bestAlternative is null)
        {
            bullets.Add("Only eligible candidate surfaced, so Cortex cannot diversify this ticket further.");
        }
        else if (selection.DiversificationApplied && selection.RawTop is not null)
        {
            bullets.Add($"{selection.RawTop.Candidate.DisplayName} had the strongest isolated score; {selected.Candidate.DisplayName} keeps the batch better distributed after projected workload.");
        }
        else if (selected.IncomingRecommendationCount > 0)
        {
            bullets.Add($"{selected.Candidate.DisplayName} already has {selected.IncomingRecommendationCount} incoming recommendation{Pluralize(selected.IncomingRecommendationCount)}, but the next viable option has weaker workload or routing fit.");
        }
        else
        {
            bullets.Add($"{bestAlternative.Candidate.DisplayName} was considered but ranked lower after projected workload and routing fit.");
        }

        if (selected.ProjectedOverload)
        {
            bullets.Add("Target owner could reach high pressure after this move, so execution remains scoped to this ticket.");
        }

        return bullets.Distinct(StringComparer.Ordinal).Take(3).ToList();
    }

    private static List<string> BuildSafetyNotes(bool isBlockedByManualOverride)
    {
        var notes = new List<string>
        {
            "Execution applies this displayed owner only after current ownership and eligibility checks pass.",
        };

        if (isBlockedByManualOverride)
        {
            notes.Add("Manual override currently controls this ticket; applying requires explicit override confirmation.");
        }

        return notes;
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
        CortexDecisionCandidate? recommendedCandidate,
        RebalanceCandidateSelection? selection = null)
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

        if (selection?.DiversificationApplied == true && selection.RawTop is not null)
        {
            preview.Add($"Avoids creating a new bottleneck by not adding this recommendation to {selection.RawTop.Candidate.DisplayName}.");
        }

        preview.Add("Keeps the correction scoped to a specific ticket.");

        return preview.Distinct(StringComparer.Ordinal).Take(4).ToList();
    }

    private static List<RebalanceSuggestionAlternative> BuildSuggestionAlternatives(
        RebalanceCandidateSelection selection)
    {
        if (selection.Selected is null)
        {
            return [];
        }

        return selection.DistributionRanked
            .Where(candidate => !IsSameOwner(
                selection.Selected.Candidate.UserId,
                candidate.Candidate.UserId,
                candidate.Candidate.DisplayName))
            .Take(3)
            .Select(candidate => new RebalanceSuggestionAlternative
            {
                UserId = candidate.Candidate.UserId,
                DisplayName = candidate.Candidate.DisplayName,
                WorkloadScore = candidate.Candidate.WorkloadScore,
                ProjectedWorkloadScore = candidate.ProjectedWorkloadScore,
                TotalScore = candidate.DistributionScore,
                PressureLevel = WorkloadScoringPolicy.ToPressureLevel(candidate.ProjectedWorkloadScore),
                IncomingRecommendationCount = candidate.IncomingRecommendationCount,
                RankBeforeDiversification = candidate.RawRank,
                RankAfterDiversification = candidate.DistributionRank,
                ReasonNotSelected = BuildAlternativeReason(selection, candidate),
            })
            .ToList();
    }

    private static string BuildAlternativeReason(
        RebalanceCandidateSelection selection,
        DistributionCandidateScore alternative)
    {
        if (selection.Selected is null)
        {
            return "Not selected because no final candidate was available.";
        }

        if (selection.DiversificationApplied
            && selection.RawTop is not null
            && IsSameOwner(
                selection.RawTop.Candidate.UserId,
                alternative.Candidate.UserId,
                alternative.Candidate.DisplayName))
        {
            return "Raw top scorer, but projected batch pressure favored spreading this recommendation.";
        }

        if (alternative.Candidate.CurrentlyOverloaded || alternative.ProjectedOverload)
        {
            return "Higher projected workload pressure after this move.";
        }

        if (alternative.IncomingRecommendationCount > selection.Selected.IncomingRecommendationCount)
        {
            return "Already has more incoming recommendations in this rebalance run.";
        }

        if (alternative.Candidate.SlaRiskCount > selection.Selected.Candidate.SlaRiskCount)
        {
            return "Carries more current SLA-risk work than the selected owner.";
        }

        return "Lower deterministic fit after workload, routing, and projected distribution were compared.";
    }

    private static RebalanceAiDecisionPacket BuildAiDecisionPacket(
        Ticket ticket,
        RebalanceSuggestion suggestion,
        RebalanceCandidateSelection selection,
        WorkloadSnapshot? currentOwnerSnapshot,
        TicketWorkloadSignals ticketSignals)
    {
        var selected = selection.Selected;
        return new RebalanceAiDecisionPacket
        {
            TicketId = suggestion.TicketId,
            TicketTitle = suggestion.TicketTitle,
            TicketSummary = TruncateForAdvisory(ticket.Description, 500),
            Priority = ticket.Priority,
            Status = ticket.Status,
            TicketSignals = BuildTicketSignalLabels(ticketSignals),
            CurrentOwner = new RebalanceAiOwnerSnapshot
            {
                UserId = suggestion.FromUserId,
                DisplayName = suggestion.FromDisplayName,
                ActiveTicketCount = currentOwnerSnapshot?.ActiveTicketCount ?? 0,
                SlaRiskCount = currentOwnerSnapshot?.SlaRiskCount ?? 0,
                HighPriorityCount = currentOwnerSnapshot?.HighPriorityCount ?? 0,
                StaleTicketCount = currentOwnerSnapshot?.StaleTicketCount ?? 0,
                WorkloadScore = currentOwnerSnapshot?.WorkloadScore ?? 0m,
                ProjectedWorkloadScore = currentOwnerSnapshot?.WorkloadScore ?? 0m,
            },
            SelectedOwner = selected is null
                ? new RebalanceAiOwnerSnapshot
                {
                    UserId = suggestion.ToUserId,
                    DisplayName = suggestion.ToDisplayName,
                }
                : new RebalanceAiOwnerSnapshot
                {
                    UserId = selected.Candidate.UserId,
                    DisplayName = selected.Candidate.DisplayName,
                    ActiveTicketCount = selected.Candidate.ActiveTicketCount,
                    SlaRiskCount = selected.Candidate.SlaRiskCount,
                    HighPriorityCount = selected.Candidate.HighPriorityCount,
                    StaleTicketCount = selected.Candidate.StaleTicketCount,
                    WorkloadScore = selected.Candidate.WorkloadScore,
                    ProjectedWorkloadScore = selected.ProjectedWorkloadScore,
                    IncomingRecommendationCount = selected.IncomingRecommendationCount,
                },
            RawTopCandidateName = suggestion.RawTopCandidateName,
            FinalCandidateName = suggestion.FinalCandidateName,
            DiversificationApplied = suggestion.DiversificationApplied,
            DeterministicReasons = suggestion.WhyTicketBullets
                .Concat(suggestion.WhyOwnerBullets)
                .Concat(suggestion.TradeoffBullets)
                .Take(8)
                .ToList(),
            CandidateOptions = selection.DistributionRanked
                .Take(5)
                .Select(candidate => new RebalanceAiCandidateOption
                {
                    UserId = candidate.Candidate.UserId,
                    DisplayName = candidate.Candidate.DisplayName,
                    WorkloadScore = candidate.Candidate.WorkloadScore,
                    ProjectedWorkloadScore = candidate.ProjectedWorkloadScore,
                    PressureLevel = WorkloadScoringPolicy.ToPressureLevel(candidate.ProjectedWorkloadScore),
                    RankBeforeDiversification = candidate.RawRank,
                    RankAfterDiversification = candidate.DistributionRank,
                    Outcome = selected is not null && IsSameOwner(
                        selected.Candidate.UserId,
                        candidate.Candidate.UserId,
                        candidate.Candidate.DisplayName)
                        ? "selected"
                        : BuildAlternativeReason(selection, candidate),
                })
                .ToList(),
        };
    }

    private async Task ApplyAiAdvisoriesAsync(
        IReadOnlyList<RebalanceSuggestion> suggestions,
        IReadOnlyList<RebalanceAiDecisionPacket> advisoryPackets,
        CancellationToken cancellationToken)
    {
        if (suggestions.Count == 0 || advisoryPackets.Count == 0)
        {
            return;
        }

        try
        {
            var advisories = await rebalanceAiAdvisoryService.GenerateAdvisoriesAsync(
                advisoryPackets,
                cancellationToken);
            if (advisories.Count == 0)
            {
                return;
            }

            foreach (var suggestion in suggestions)
            {
                if (!advisories.TryGetValue(suggestion.TicketId, out var advisory))
                {
                    continue;
                }

                suggestion.AiAdvisorySummary = advisory.Rationale;
                suggestion.AiRiskSummary = advisory.RiskSummary;
                suggestion.AiTradeoffSummary = advisory.TradeoffSummary;
                suggestion.AiConfidenceWording = advisory.ConfidenceWording;
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Rebalance AI advisory failed; deterministic suggestions were returned.");
        }
    }

    private static List<string> BuildTicketSignalLabels(TicketWorkloadSignals signals)
    {
        var labels = new List<string>();
        if (signals.IsHighPriority)
        {
            labels.Add("high-priority");
        }
        if (signals.IsOverdue)
        {
            labels.Add("sla-breached");
        }
        if (signals.IsSlaRisk)
        {
            labels.Add("sla-at-risk");
        }
        if (signals.IsStale)
        {
            labels.Add("stale");
        }

        return labels.Count == 0 ? ["standard-active-work"] : labels;
    }

    private static string TruncateForAdvisory(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var clean = value.Trim().ReplaceLineEndings(" ");
        return clean.Length <= maxLength ? clean : clean[..maxLength].TrimEnd();
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

    private async Task<Dictionary<string, RebalanceMovePayload>> LoadRecentRebalanceMovesAsync(
        IEnumerable<string> ticketIds,
        CancellationToken cancellationToken)
    {
        var ticketIdSet = ticketIds
            .Where(ticketId => !string.IsNullOrWhiteSpace(ticketId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (ticketIdSet.Count == 0)
        {
            return new Dictionary<string, RebalanceMovePayload>(StringComparer.OrdinalIgnoreCase);
        }

        var cutoffUtc = DateTime.UtcNow.Subtract(RebalanceCooldownWindow);
        var events = await dbContext.WorkflowMetricEvents
            .AsNoTracking()
            .Where(metric => metric.EventType == RebalanceAppliedEventType)
            .Where(metric => metric.OccurredUtc >= cutoffUtc)
            .Where(metric => metric.TicketId != null && ticketIdSet.Contains(metric.TicketId))
            .OrderByDescending(metric => metric.OccurredUtc)
            .ToListAsync(cancellationToken);

        var recentMoves = new Dictionary<string, RebalanceMovePayload>(StringComparer.OrdinalIgnoreCase);
        foreach (var metric in events)
        {
            if (string.IsNullOrWhiteSpace(metric.TicketId) || recentMoves.ContainsKey(metric.TicketId))
            {
                continue;
            }

            var payload = TryReadRebalanceMovePayload(metric.PayloadJson);
            if (payload is null
                || string.IsNullOrWhiteSpace(payload.FromOwner)
                || string.IsNullOrWhiteSpace(payload.ToOwner))
            {
                continue;
            }

            recentMoves[metric.TicketId] = payload;
        }

        return recentMoves;
    }

    private static RebalanceMovePayload? TryReadRebalanceMovePayload(string payloadJson)
    {
        try
        {
            return JsonSerializer.Deserialize<RebalanceMovePayload>(payloadJson);
        }
        catch (JsonException)
        {
            return null;
        }
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

    private sealed class ProjectedOwnerLoad
    {
        public string UserId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int ActiveTicketCount { get; set; }
        public int HighPriorityCount { get; set; }
        public int OverdueTicketCount { get; set; }
        public int SlaRiskCount { get; set; }
        public int StaleTicketCount { get; set; }
        public decimal WorkloadScore { get; set; }
        public int IncomingRecommendationCount { get; set; }
    }

    private sealed class DistributionCandidateScore
    {
        public CortexDecisionCandidate Candidate { get; set; } = new();
        public int RawRank { get; set; }
        public int DistributionRank { get; set; }
        public decimal DistributionScore { get; set; }
        public decimal ProjectedWorkloadScore { get; set; }
        public int IncomingRecommendationCount { get; set; }
        public decimal IncomingPenalty { get; set; }
        public bool ProjectedOverload { get; set; }
    }

    private sealed class RebalanceCandidateSelection
    {
        public DistributionCandidateScore? Selected { get; set; }
        public DistributionCandidateScore? RawTop { get; set; }
        public List<DistributionCandidateScore> DistributionRanked { get; set; } = [];
        public bool DiversificationApplied { get; set; }
        public decimal ConfidenceScore { get; set; }
        public string SelectionReason { get; set; } = string.Empty;
    }

    private sealed class RebalanceMovePayload
    {
        public string FromOwner { get; set; } = string.Empty;
        public string ToOwner { get; set; } = string.Empty;
    }
}
