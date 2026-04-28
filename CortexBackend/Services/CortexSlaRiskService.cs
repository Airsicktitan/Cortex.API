using System.Text.Json;
using Cortex.API.Models;

namespace Cortex.API.Services;

/// <summary>
/// Tier 9 — predictive + prescriptive risk surface for active tickets.
/// Deterministic scoring over existing Cortex signals; no ML, no automation.
/// Inputs only — never mutates ticket state, never bypasses Tier 8 autonomy gates.
/// </summary>
public sealed class CortexSlaRiskService(
    ISlaConfigurationService slaConfigurationService,
    IWorkloadSnapshotService workloadSnapshotService) : ICortexSlaRiskService
{
    private const int HighRiskThreshold = 8;
    private const int MediumRiskThreshold = 4;
    private const int CommentFrictionThreshold = 4;
    private const int MaxSignals = 7;

    public async Task<CortexSlaRiskAssessment> EvaluateRiskAsync(
        Ticket ticket,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        var priorityMap = await slaConfigurationService.GetPriorityMapAsync().ConfigureAwait(false);
        priorityMap.TryGetValue(ticket.Priority ?? string.Empty, out var slaConfiguration);
        var slaSnapshot = TicketSlaCalculator.Calculate(ticket, slaConfiguration);

        var ownerSnapshot = string.IsNullOrWhiteSpace(ticket.SynitiOwner)
            ? null
            : await workloadSnapshotService
                .GetSnapshotAsync(ticket.SynitiOwner!, cancellationToken)
                .ConfigureAwait(false);

        var signals = CollectSignals(ticket, slaSnapshot, ownerSnapshot);
        var score = signals.Sum(signal => signal.Weight);
        var firedCount = signals.Count;

        var riskLevel = ResolveLevel(score);
        var recommendation = ResolveRecommendation(
            ticket,
            slaSnapshot,
            ownerSnapshot,
            signals,
            riskLevel);

        var assessment = new CortexSlaRiskAssessment
        {
            RiskLevel = riskLevel,
            RiskReasons = signals.Select(signal => signal.Reason).ToList(),
            Recommendation = recommendation.Action,
            RecommendationReason = recommendation.Reason,
            Score = score,
            SlaStatus = slaSnapshot.Status,
            Confidence = ComputeConfidence(firedCount, riskLevel),
        };

        if (assessment.RiskReasons.Count == 0)
        {
            assessment.RiskReasons.Add("No elevated SLA, intake, or workload signals on this ticket.");
        }

        return assessment;
    }

    private static List<RiskSignal> CollectSignals(
        Ticket ticket,
        TicketSlaSnapshot slaSnapshot,
        WorkloadSnapshot? ownerSnapshot)
    {
        var signals = new List<RiskSignal>();

        if (IsHighOrCritical(ticket.Priority))
        {
            signals.Add(new RiskSignal(
                Weight: IsCritical(ticket.Priority) ? 3 : 2,
                Reason: $"{ticket.Priority} priority raises baseline urgency."));
        }

        switch (slaSnapshot.Status)
        {
            case "Breached":
            case "Resolved Late":
                signals.Add(new RiskSignal(5, "SLA target has already been breached."));
                break;
            case "At Risk":
                signals.Add(new RiskSignal(3, "Within the SLA warning window — deadline is close."));
                break;
            case "Pending Approval":
                signals.Add(new RiskSignal(1, "Awaiting approval; SLA clock has not started."));
                break;
            case "Needs More Info":
                signals.Add(new RiskSignal(2, "Returned for more detail; SLA paused but progress is blocked."));
                break;
        }

        if (HasMissingDetails(ticket))
        {
            signals.Add(new RiskSignal(2, "AI triage flagged missing details on this ticket."));
        }

        if (string.Equals(ticket.AiTriagePotentialSlaRisk, "High", StringComparison.OrdinalIgnoreCase))
        {
            signals.Add(new RiskSignal(2, "AI triage flagged this ticket as high SLA pressure."));
        }
        else if (string.Equals(ticket.AiTriagePotentialSlaRisk, "Medium", StringComparison.OrdinalIgnoreCase))
        {
            signals.Add(new RiskSignal(1, "AI triage flagged this ticket as moderate SLA pressure."));
        }

        if (ownerSnapshot is not null)
        {
            if (string.Equals(ownerSnapshot.Status, "Overloaded", StringComparison.OrdinalIgnoreCase))
            {
                signals.Add(new RiskSignal(3, $"Current owner {ownerSnapshot.DisplayName} is overloaded."));
            }
            else if (ownerSnapshot.SlaRiskCount > 0 || ownerSnapshot.OverdueTicketCount > 0)
            {
                signals.Add(new RiskSignal(
                    1,
                    $"Current owner already has {ownerSnapshot.SlaRiskCount + ownerSnapshot.OverdueTicketCount} SLA-pressured tickets."));
            }
        }

        if (ticket.Comments?.Count >= CommentFrictionThreshold)
        {
            signals.Add(new RiskSignal(
                1,
                $"{ticket.Comments.Count} comments suggest active back-and-forth and possible friction."));
        }

        return signals;
    }

    private (CortexRiskRecommendation Action, string Reason) ResolveRecommendation(
        Ticket ticket,
        TicketSlaSnapshot slaSnapshot,
        WorkloadSnapshot? ownerSnapshot,
        IReadOnlyList<RiskSignal> signals,
        CortexRiskLevel riskLevel)
    {
        var hasMissingDetails = HasMissingDetails(ticket)
            || ticket.ApprovalStatus == ApprovalStatus.NeedsMoreInfo;
        if (hasMissingDetails)
        {
            return (
                CortexRiskRecommendation.RequestMoreDetail,
                "Cortex flagged missing detail — clarify scope before SLA pressure grows.");
        }

        var slaPressed = slaSnapshot.Status is "At Risk" or "Breached" or "Resolved Late";
        var ownerOverloaded = ownerSnapshot is not null
            && string.Equals(ownerSnapshot.Status, "Overloaded", StringComparison.OrdinalIgnoreCase);

        if (slaPressed && ownerOverloaded)
        {
            return (
                CortexRiskRecommendation.Reassign,
                "SLA is under pressure and current owner is overloaded — consider reassigning to balance load.");
        }

        if (riskLevel == CortexRiskLevel.High && IsCritical(ticket.Priority))
        {
            return (
                CortexRiskRecommendation.Escalate,
                "Critical priority with elevated risk signals — escalate to keep this on track.");
        }

        if (riskLevel == CortexRiskLevel.High)
        {
            return (
                CortexRiskRecommendation.Escalate,
                "Multiple risk signals fired — bring leadership awareness in case of escalation.");
        }

        return (
            CortexRiskRecommendation.KeepOnCurrentPath,
            signals.Count == 0
                ? "No elevated risk signals detected; keep on current path."
                : "Risk signals are within normal range; keep on current path.");
    }

    private static CortexRiskLevel ResolveLevel(int score)
    {
        if (score >= HighRiskThreshold) return CortexRiskLevel.High;
        if (score >= MediumRiskThreshold) return CortexRiskLevel.Medium;
        return CortexRiskLevel.Low;
    }

    private static decimal ComputeConfidence(int firedSignals, CortexRiskLevel level)
    {
        // Confidence is the share of available signals that fired, slightly boosted when
        // the level is High (multiple corroborating signals), slightly damped when Low.
        var baseRatio = Math.Min(1m, (decimal)firedSignals / MaxSignals);
        var adjusted = level switch
        {
            CortexRiskLevel.High => baseRatio + 0.1m,
            CortexRiskLevel.Low => baseRatio - 0.1m,
            _ => baseRatio
        };
        return Math.Round(Math.Clamp(adjusted, 0m, 1m), 2);
    }

    private static bool HasMissingDetails(Ticket ticket)
    {
        if (string.IsNullOrWhiteSpace(ticket.AiTriageMissingDetailsJson))
        {
            return false;
        }

        try
        {
            var items = JsonSerializer.Deserialize<List<string>>(ticket.AiTriageMissingDetailsJson);
            return items is { Count: > 0 } && items.Any(item => !string.IsNullOrWhiteSpace(item));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsHighOrCritical(string? priority) =>
        WorkloadScoringPolicy.IsHighPriority(priority);

    private static bool IsCritical(string? priority) =>
        priority is not null
        && priority.Equals("Critical", StringComparison.OrdinalIgnoreCase);

    private sealed record RiskSignal(int Weight, string Reason);
}
