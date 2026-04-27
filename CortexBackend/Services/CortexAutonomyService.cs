using System.Text.Json;
using Cortex.API.Configuration;
using Cortex.API.Data;
using Cortex.API.Database;
using Cortex.API.DTO;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Services;

/// <summary>
/// Tier 8 Safe Autonomy Layer. Evaluates a ticket's latest routing recommendation,
/// records an audit row for every evaluation, and only mutates the ticket when
/// configuration explicitly permits execution AND every eligibility check passes.
///
/// Hard rules enforced here:
/// - Never mutates priority, status, SLA, or approval state.
/// - Only acts on assignment (SynitiOwner) when ShadowMode=false AND Enabled=true.
/// - Defaults to shadow-only.
/// </summary>
public sealed class CortexAutonomyService(
    ICortexAutonomySettingsService autonomySettingsService,
    ICortexDecisionService cortexDecisionService,
    ITicketRoutingRuleService ticketRoutingRuleService,
    IOperationalRiskService operationalRiskService,
    ITicketRepository ticketRepository,
    CortexDbContext dbContext,
    ILogger<CortexAutonomyService> logger) : ICortexAutonomyService
{
    public const string DecisionVersion = "autonomy-v1";

    private static readonly HashSet<string> TerminalStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Resolved",
        "Closed",
        "Done",
        "Completed",
        "Cancelled",
        "Canceled",
        "Rejected",
        "Archived",
    };

    public async Task<CortexAutonomyResultDto> EvaluateAndMaybeApplyDecisionAsync(
        Ticket ticket,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        var passedChecks = new List<string>();
        var blockedReasons = new List<string>();
        var options = await autonomySettingsService.GetEffectiveAsync(cancellationToken);

        // Load the latest Cortex routing/decision recommendation. We use the live
        // CortexDecisionService so the autonomy decision matches what the UI shows.
        CortexDecisionResult decision;
        try
        {
            decision = await cortexDecisionService.EvaluateAssignmentAsync(
                ticket,
                aiAssessment: null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Autonomy evaluation could not load a decision for ticket {TicketId}; recording shadow-only result.",
                ticket.Id);
            return await RecordAndReturnAsync(
                ticket,
                previousOwnerSnapshot: NormalizeOrNull(ticket.SynitiOwner),
                recommendedOwnerId: null,
                recommendedOwnerName: null,
                confidence: 0d,
                learningAdjustment: null,
                isEligible: false,
                wasAutoApplied: false,
                mode: ResolveBlockedMode(options),
                passedChecks: passedChecks,
                blockedReasons: ["Routing decision unavailable."],
                summary: "Cortex could not evaluate this ticket right now.",
                cancellationToken: cancellationToken);
        }

        var confidence = (double)decision.ConfidenceScore;
        var learningDelta = decision.LearningConfidenceDelta.HasValue
            ? (double?)decision.LearningConfidenceDelta.Value
            : null;
        var recommendedOwnerId = NormalizeOrNull(decision.RecommendedOwnerUserId);
        var recommendedOwnerName = NormalizeOrNull(decision.RecommendedOwnerDisplayName);
        var currentOwner = NormalizeOrNull(ticket.SynitiOwner);
        var previousOwnerSnapshot = currentOwner;

        // Each check is run independently so the UI can show every reason at once.
        EvaluateConfidence(confidence, options, passedChecks, blockedReasons);
        EvaluateRecommendedOwner(recommendedOwnerId, passedChecks, blockedReasons);
        EvaluateOwnerDifference(currentOwner, recommendedOwnerId, recommendedOwnerName, passedChecks, blockedReasons);
        EvaluateTerminalStatus(ticket, passedChecks, blockedReasons);
        EvaluateApprovalState(ticket, passedChecks, blockedReasons);
        await EvaluateRecentOverrideAsync(ticket.Id, options, passedChecks, blockedReasons, cancellationToken);
        await EvaluateRiskAsync(ticket, passedChecks, blockedReasons, cancellationToken);
        EvaluateClearWinner(decision, options, passedChecks, blockedReasons);

        var isEligible = blockedReasons.Count == 0;
        var mode = ResolveMode(options, willApply: false);
        var wasAutoApplied = false;

        if (isEligible && options.IsExecutionAllowed)
        {
            try
            {
                await ApplyAssignmentAsync(ticket, recommendedOwnerId!, recommendedOwnerName, cancellationToken);
                wasAutoApplied = true;
                mode = "AutoApplied";
                passedChecks.Add("Auto-applied assignment.");
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Autonomy auto-apply failed for ticket {TicketId}; falling back to shadow record.",
                    ticket.Id);
                blockedReasons.Add("Auto-apply failed; recorded as shadow.");
                isEligible = false;
                mode = ResolveBlockedMode(options);
            }
        }

        var summary = BuildSummary(
            mode,
            isEligible,
            wasAutoApplied,
            recommendedOwnerName ?? recommendedOwnerId,
            blockedReasons);

        return await RecordAndReturnAsync(
            ticket,
            previousOwnerSnapshot,
            recommendedOwnerId,
            recommendedOwnerName,
            confidence,
            learningDelta,
            isEligible,
            wasAutoApplied,
            mode,
            passedChecks,
            blockedReasons,
            summary,
            cancellationToken);
    }

    public async Task<CortexAutonomyResultDto?> GetLatestAsync(
        string ticketId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ticketId))
        {
            return null;
        }

        var entity = await dbContext.CortexAutonomyDecisions
            .AsNoTracking()
            .Where(d => d.TicketId == ticketId)
            .OrderByDescending(d => d.CreatedDateUtc)
            .ThenByDescending(d => d.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return entity is null ? null : MapToDto(entity);
    }

    private static void EvaluateConfidence(
        double confidence,
        CortexAutonomyOptions options,
        List<string> passedChecks,
        List<string> blockedReasons)
    {
        if (confidence + 1e-9 >= options.MinConfidence)
        {
            passedChecks.Add($"High-confidence decision ({FormatConfidence(confidence)}).");
        }
        else
        {
            blockedReasons.Add(
                $"Confidence below auto-apply threshold ({FormatConfidence(confidence)} < {FormatConfidence(options.MinConfidence)}).");
        }
    }

    private static void EvaluateRecommendedOwner(
        string? recommendedOwnerId,
        List<string> passedChecks,
        List<string> blockedReasons)
    {
        if (string.IsNullOrWhiteSpace(recommendedOwnerId))
        {
            blockedReasons.Add("No recommended owner identified.");
        }
        else
        {
            passedChecks.Add("Recommended owner is identified.");
        }
    }

    private static void EvaluateOwnerDifference(
        string? currentOwner,
        string? recommendedOwnerId,
        string? recommendedOwnerName,
        List<string> passedChecks,
        List<string> blockedReasons)
    {
        if (string.IsNullOrWhiteSpace(recommendedOwnerId))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(currentOwner))
        {
            passedChecks.Add("Ticket has no current owner.");
            return;
        }

        var currentTrim = currentOwner.Trim();
        var matchesId = string.Equals(currentTrim, recommendedOwnerId.Trim(), StringComparison.OrdinalIgnoreCase);
        var matchesName = !string.IsNullOrWhiteSpace(recommendedOwnerName)
            && string.Equals(currentTrim, recommendedOwnerName.Trim(), StringComparison.OrdinalIgnoreCase);
        if (matchesId || matchesName)
        {
            blockedReasons.Add("Recommended owner matches the current owner.");
        }
        else
        {
            passedChecks.Add("Recommended owner differs from current owner.");
        }
    }

    private static void EvaluateTerminalStatus(
        Ticket ticket,
        List<string> passedChecks,
        List<string> blockedReasons)
    {
        if (!string.IsNullOrWhiteSpace(ticket.Status) && TerminalStatuses.Contains(ticket.Status.Trim()))
        {
            blockedReasons.Add($"Ticket status '{ticket.Status}' is terminal.");
        }
        else
        {
            passedChecks.Add("Ticket is not in a terminal status.");
        }
    }

    private static void EvaluateApprovalState(
        Ticket ticket,
        List<string> passedChecks,
        List<string> blockedReasons)
    {
        switch (ticket.ApprovalStatus)
        {
            case ApprovalStatus.Rejected:
                blockedReasons.Add("Ticket has been rejected.");
                return;
            case ApprovalStatus.PendingApproval:
                blockedReasons.Add("Ticket is awaiting approval.");
                return;
            case ApprovalStatus.NeedsMoreInfo:
                blockedReasons.Add("Ticket has been returned for more detail.");
                return;
            default:
                passedChecks.Add("No approval-blocking state prevents reassignment.");
                return;
        }
    }

    private async Task EvaluateRecentOverrideAsync(
        string ticketId,
        CortexAutonomyOptions options,
        List<string> passedChecks,
        List<string> blockedReasons,
        CancellationToken cancellationToken)
    {
        try
        {
            var latestOverride = await ticketRoutingRuleService.GetLatestOverrideAsync(ticketId, cancellationToken);
            if (latestOverride is null)
            {
                passedChecks.Add("No prior human override on this ticket.");
                return;
            }

            var windowStart = DateTime.UtcNow - TimeSpan.FromHours(Math.Max(0, options.RecentOverrideWindowHours));
            if (latestOverride.CreatedDateUtc >= windowStart)
            {
                blockedReasons.Add(
                    $"Recent human override detected within last {options.RecentOverrideWindowHours} hours.");
            }
            else
            {
                passedChecks.Add("No recent human override conflict.");
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not load latest override for ticket {TicketId}; treating as no override.", ticketId);
            passedChecks.Add("No recent human override conflict.");
        }
    }

    private async Task EvaluateRiskAsync(
        Ticket ticket,
        List<string> passedChecks,
        List<string> blockedReasons,
        CancellationToken cancellationToken)
    {
        try
        {
            var risk = await operationalRiskService.EvaluateAsync(ticket, cancellationToken);
            var level = (risk.RiskLevel ?? string.Empty).Trim();
            if (level.Equals("critical", StringComparison.OrdinalIgnoreCase)
                || level.Equals("high", StringComparison.OrdinalIgnoreCase))
            {
                blockedReasons.Add($"Operational risk is {level}.");
                return;
            }

            if (risk.Reasons.Any(reason =>
                    reason.Contains("breach", StringComparison.OrdinalIgnoreCase)
                    || reason.Contains("at risk", StringComparison.OrdinalIgnoreCase)))
            {
                blockedReasons.Add("SLA risk is elevated for this ticket.");
                return;
            }

            passedChecks.Add("Low operational risk.");
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not evaluate operational risk for ticket {TicketId}; treating as low.", ticket.Id);
            passedChecks.Add("Low operational risk.");
        }
    }

    private static void EvaluateClearWinner(
        CortexDecisionResult decision,
        CortexAutonomyOptions options,
        List<string> passedChecks,
        List<string> blockedReasons)
    {
        if (!options.RequireClearWinner)
        {
            return;
        }

        var ranked = decision.Candidates
            .Where(c => c.Eligible)
            .OrderByDescending(c => c.TotalScore)
            .ToList();

        if (ranked.Count <= 1)
        {
            passedChecks.Add("Clear routing winner (no contested alternative).");
            return;
        }

        var top = (double)ranked[0].TotalScore;
        var next = (double)ranked[1].TotalScore;
        var denominator = Math.Max(1d, Math.Abs(top));
        var gap = (top - next) / denominator;
        if (gap + 1e-9 >= options.MinAlternativeGap)
        {
            passedChecks.Add($"Clear routing winner (gap {gap:P0}).");
        }
        else
        {
            blockedReasons.Add(
                $"Top recommendation is not clearly ahead of alternatives (gap {gap:P0} < {options.MinAlternativeGap:P0}).");
        }
    }

    private async Task ApplyAssignmentAsync(
        Ticket ticket,
        string recommendedOwnerId,
        string? recommendedOwnerName,
        CancellationToken cancellationToken)
    {
        ticket.SynitiOwner = recommendedOwnerId;
        ticket.LastModifiedDate = DateTime.UtcNow;
        await ticketRepository.UpdateTicketAsync(ticket);
        await ticketRepository.SaveChangesAsync();

        logger.LogInformation(
            "Cortex autonomy auto-applied assignment for ticket {TicketId} to owner {OwnerId} ({OwnerName}).",
            ticket.Id,
            recommendedOwnerId,
            recommendedOwnerName ?? recommendedOwnerId);
    }

    private async Task<CortexAutonomyResultDto> RecordAndReturnAsync(
        Ticket ticket,
        string? previousOwnerSnapshot,
        string? recommendedOwnerId,
        string? recommendedOwnerName,
        double confidence,
        double? learningAdjustment,
        bool isEligible,
        bool wasAutoApplied,
        string mode,
        List<string> passedChecks,
        List<string> blockedReasons,
        string summary,
        CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var entity = new CortexAutonomyDecision
        {
            TicketId = ticket.Id,
            RecommendedOwnerId = recommendedOwnerId,
            RecommendedOwnerName = recommendedOwnerName,
            PreviousOwnerId = previousOwnerSnapshot,
            Confidence = ToDecimal(confidence),
            LearningAdjustment = learningAdjustment.HasValue ? ToDecimal(learningAdjustment.Value) : null,
            IsEligible = isEligible,
            WasAutoApplied = wasAutoApplied,
            Mode = mode,
            PassedChecksJson = JsonSerializer.Serialize(passedChecks),
            BlockedReasonsJson = JsonSerializer.Serialize(blockedReasons),
            Summary = Truncate(summary, 2000),
            DecisionVersion = DecisionVersion,
            CreatedDateUtc = nowUtc,
            AppliedDateUtc = wasAutoApplied ? nowUtc : null,
        };

        try
        {
            dbContext.CortexAutonomyDecisions.Add(entity);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to persist autonomy decision for ticket {TicketId}; returning in-memory result.",
                ticket.Id);
        }

        return new CortexAutonomyResultDto
        {
            TicketId = ticket.Id,
            IsEligible = isEligible,
            WasAutoApplied = wasAutoApplied,
            Mode = mode,
            RecommendedOwnerId = recommendedOwnerId,
            RecommendedOwnerName = recommendedOwnerName,
            PreviousOwnerId = entity.PreviousOwnerId,
            Confidence = confidence,
            LearningAdjustment = learningAdjustment,
            DecisionVersion = DecisionVersion,
            PassedChecks = [.. passedChecks],
            BlockedReasons = [.. blockedReasons],
            Summary = entity.Summary,
            EvaluatedAtUtc = entity.CreatedDateUtc,
            AppliedAtUtc = entity.AppliedDateUtc,
        };
    }

    private static string ResolveMode(CortexAutonomyOptions options, bool willApply)
    {
        if (willApply)
        {
            return "AutoApplied";
        }

        if (!options.Enabled)
        {
            return "Disabled";
        }

        return "Shadow";
    }

    private static string ResolveBlockedMode(CortexAutonomyOptions options) => options.Enabled ? "Shadow" : "Disabled";

    private static string BuildSummary(
        string mode,
        bool isEligible,
        bool wasAutoApplied,
        string? ownerLabel,
        IReadOnlyList<string> blockedReasons)
    {
        var displayOwner = string.IsNullOrWhiteSpace(ownerLabel) ? "the recommended owner" : ownerLabel;

        if (wasAutoApplied)
        {
            return $"Cortex auto-applied this assignment to {displayOwner}.";
        }

        if (isEligible)
        {
            return mode == "Shadow"
                ? $"Cortex would safely auto-assign this ticket to {displayOwner}."
                : $"Cortex evaluated this assignment to {displayOwner} as eligible.";
        }

        if (blockedReasons.Count == 0)
        {
            return "Cortex kept this as a recommendation.";
        }

        return $"Cortex kept this as a recommendation. {blockedReasons[0]}";
    }

    private static CortexAutonomyResultDto MapToDto(CortexAutonomyDecision entity)
    {
        return new CortexAutonomyResultDto
        {
            TicketId = entity.TicketId,
            IsEligible = entity.IsEligible,
            WasAutoApplied = entity.WasAutoApplied,
            Mode = entity.Mode,
            RecommendedOwnerId = entity.RecommendedOwnerId,
            RecommendedOwnerName = entity.RecommendedOwnerName,
            PreviousOwnerId = entity.PreviousOwnerId,
            Confidence = (double)entity.Confidence,
            LearningAdjustment = entity.LearningAdjustment.HasValue
                ? (double?)entity.LearningAdjustment.Value
                : null,
            DecisionVersion = entity.DecisionVersion,
            PassedChecks = DeserializeStringList(entity.PassedChecksJson),
            BlockedReasons = DeserializeStringList(entity.BlockedReasonsJson),
            Summary = entity.Summary,
            EvaluatedAtUtc = entity.CreatedDateUtc,
            AppliedAtUtc = entity.AppliedDateUtc,
        };
    }

    private static List<string> DeserializeStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string FormatConfidence(double value) => value.ToString("0.00");

    private static decimal ToDecimal(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return 0m;
        }

        var clamped = Math.Clamp(value, -9.9999d, 9.9999d);
        return (decimal)clamped;
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value ?? string.Empty;
        }

        return value[..maxLength];
    }

    private static string? NormalizeOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
