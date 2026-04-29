using System.Text.Json;
using Cortex.API.DTO;
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
    /// <summary>Past this age in Pending Approval, intake is treated as stale (aligns with workload stale window).</summary>
    private static readonly TimeSpan StalePendingApprovalThreshold =
        TimeSpan.FromHours(WorkloadScoringPolicy.StaleTicketAgeHours);

    private const int HighRiskThreshold = 8;
    private const int MediumRiskThreshold = 4;
    private const int CommentFrictionThreshold = 4;
    private const int MaxSignals = 7;
    private const int MediumInsightConfidenceThreshold = 50;
    private const string MemoryPatternRiskSignal = "Recent similar issues required follow-up";

    public async Task<CortexSlaRiskAssessment> EvaluateRiskAsync(
        Ticket ticket,
        CancellationToken cancellationToken = default,
        CortexInsightDto? cachedInsight = null)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        var priorityMap = await slaConfigurationService.GetPriorityMapAsync().ConfigureAwait(false);
        priorityMap.TryGetValue(ticket.Priority ?? string.Empty, out var slaConfiguration);
        var slaSnapshot = TicketSlaCalculator.Calculate(ticket, slaConfiguration);
        var utcNow = DateTime.UtcNow;

        var ownerSnapshot = string.IsNullOrWhiteSpace(ticket.SynitiOwner)
            ? null
            : await workloadSnapshotService
                .GetSnapshotAsync(ticket.SynitiOwner!, cancellationToken)
                .ConfigureAwait(false);

        var signals = CollectSignals(ticket, slaSnapshot, ownerSnapshot, utcNow);
        var score = signals.Sum(signal => signal.Weight);
        var firedCount = signals.Count;

        var riskLevel = ResolveLevel(score);
        var recommendation = ResolveRecommendation(
            ticket,
            slaSnapshot,
            ownerSnapshot,
            signals,
            riskLevel,
            utcNow);

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

        if (HasMemoryPatternRisk(cachedInsight)
            && !assessment.RiskReasons.Any(reason =>
                string.Equals(reason, MemoryPatternRiskSignal, StringComparison.Ordinal)))
        {
            assessment.RiskReasons.Add(MemoryPatternRiskSignal);
        }

        if (assessment.RiskReasons.Count == 0)
        {
            assessment.RiskReasons.Add("No elevated SLA, intake, or workload signals on this ticket.");
        }

        return assessment;
    }

    private static bool HasMemoryPatternRisk(CortexInsightDto? insight)
    {
        if (insight?.Matches is not { Count: > 0 })
        {
            return false;
        }

        if (!HasMediumConfidenceMatch(insight))
        {
            return false;
        }

        return insight.LearningSignals.Any(IsRiskLearningSignal)
            || insight.Matches.Any(IsFrictionMatch);
    }

    private static bool HasMediumConfidenceMatch(CortexInsightDto insight) =>
        insight.ConfidenceScore >= MediumInsightConfidenceThreshold
        || insight.Matches.Any(match => match.ConfidenceScore >= MediumInsightConfidenceThreshold);

    private static bool IsRiskLearningSignal(CortexLearningSignalDto signal)
    {
        if (!IsMediumOrHigh(signal.Confidence))
        {
            return false;
        }

        var text = string.Join(
            ' ',
            signal.SignalType,
            signal.Title,
            signal.Description,
            string.Join(' ', signal.SupportingFacts));

        if (ContainsAny(
                text,
                "follow-up",
                "follow up",
                "clarification",
                "reassign",
                "reassignment",
                "reassigned",
                "reopened",
                "rework",
                "override",
                "overridden",
                "returned",
                "rejected",
                "needs more info",
                "more detail"))
        {
            return true;
        }

        return text.Contains("sla", StringComparison.OrdinalIgnoreCase)
            && ContainsAny(
                text,
                "breach",
                "breached",
                "elevated",
                "pressure",
                "late",
                "miss",
                "missed",
                "at risk");
    }

    private static bool IsFrictionMatch(CortexInsightSimilarTicketDto match) =>
        match.ConfidenceScore >= MediumInsightConfidenceThreshold
        && ContainsAny(
            match.Status,
            "rejected",
            "needs more info",
            "returned",
            "reopened",
            "resolved late",
            "breached");

    private static bool IsMediumOrHigh(string? confidence) =>
        confidence?.Trim().Equals("Medium", StringComparison.OrdinalIgnoreCase) == true
        || confidence?.Trim().Equals("High", StringComparison.OrdinalIgnoreCase) == true;

    private static bool ContainsAny(string? source, params string[] terms) =>
        !string.IsNullOrWhiteSpace(source)
        && terms.Any(term => source.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static List<RiskSignal> CollectSignals(
        Ticket ticket,
        TicketSlaSnapshot slaSnapshot,
        WorkloadSnapshot? ownerSnapshot,
        DateTime utcNow)
    {
        var signals = new List<RiskSignal>();
        var isIntakeGate = ticket.ApprovalStatus is ApprovalStatus.PendingApproval
            or ApprovalStatus.NeedsMoreInfo
            or ApprovalStatus.Rejected;

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
                if (!isIntakeGate)
                {
                    signals.Add(new RiskSignal(5, "SLA target has already been breached."));
                }

                break;
            case "At Risk":
                if (!isIntakeGate)
                {
                    signals.Add(new RiskSignal(3, "Within the SLA warning window — deadline is close."));
                }

                break;
            case "Pending Approval":
                if (IsStalePendingApproval(ticket, utcNow))
                {
                    signals.Add(new RiskSignal(
                        4,
                        "Approval has been pending longer than the usual intake window."));
                }

                // Informational only — does not contribute to score; normal workflow, not operational risk.
                signals.Add(new RiskSignal(0, "Awaiting approval; SLA clock has not started."));
                break;
            case "Needs More Info":
                signals.Add(new RiskSignal(2, "Returned for more detail; SLA paused but progress is blocked."));
                break;
        }

        AddAiTriageMissingDetailSignal(signals, ticket, slaSnapshot);

        if (!isIntakeGate)
        {
            if (string.Equals(ticket.AiTriagePotentialSlaRisk, "High", StringComparison.OrdinalIgnoreCase))
            {
                signals.Add(new RiskSignal(2, "AI triage flagged this ticket as high SLA pressure."));
            }
            else if (string.Equals(ticket.AiTriagePotentialSlaRisk, "Medium", StringComparison.OrdinalIgnoreCase))
            {
                signals.Add(new RiskSignal(1, "AI triage flagged this ticket as moderate SLA pressure."));
            }
        }

        // Until intake completes, SynitiOwner can be tentative — avoid treating workload as operational risk here.
        if (ownerSnapshot is not null && ticket.ApprovalStatus != ApprovalStatus.PendingApproval)
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

    private static bool IsStalePendingApproval(Ticket ticket, DateTime utcNow)
    {
        if (ticket.ApprovalStatus != ApprovalStatus.PendingApproval)
        {
            return false;
        }

        var createdUtc = ToUtcAssumeUnspecifiedUtc(ticket.CreatedDate);
        return createdUtc <= utcNow - StalePendingApprovalThreshold;
    }

    private static DateTime ToUtcAssumeUnspecifiedUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

    /// <summary>
    /// AI triage missing-detail payloads: weighted by priority/intake gate so intake + low priority
    /// does not read as elevated operational risk. Skips duplication when SLA status is already "Needs More Info".
    /// </summary>
    private static void AddAiTriageMissingDetailSignal(
        List<RiskSignal> signals,
        Ticket ticket,
        TicketSlaSnapshot slaSnapshot)
    {
        if (!HasMissingDetails(ticket))
        {
            return;
        }

        if (string.Equals(slaSnapshot.Status, "Needs More Info", StringComparison.Ordinal))
        {
            return;
        }

        if (ticket.ApprovalStatus == ApprovalStatus.PendingApproval)
        {
            if (IsHighOrCritical(ticket.Priority))
            {
                signals.Add(new RiskSignal(
                    3,
                    "AI triage flagged missing details — confirm scope before approval."));
            }
            else
            {
                signals.Add(new RiskSignal(
                    1,
                    "Optional refinement points noted by AI triage; review can proceed."));
            }

            return;
        }

        signals.Add(new RiskSignal(2, "AI triage flagged missing details on this ticket."));
    }

    private (CortexRiskRecommendation Action, string Reason) ResolveRecommendation(
        Ticket ticket,
        TicketSlaSnapshot slaSnapshot,
        WorkloadSnapshot? ownerSnapshot,
        IReadOnlyList<RiskSignal> signals,
        CortexRiskLevel riskLevel,
        DateTime utcNow)
    {
        if (ticket.ApprovalStatus == ApprovalStatus.NeedsMoreInfo)
        {
            return (
                CortexRiskRecommendation.RequestMoreDetail,
                "Ticket was returned for more detail — address gaps before work proceeds.");
        }

        if (HasMissingDetails(ticket) && ticket.ApprovalStatus == ApprovalStatus.PendingApproval)
        {
            if (IsHighOrCritical(ticket.Priority))
            {
                return (
                    CortexRiskRecommendation.RequestMoreDetail,
                    "High-impact priority with flagged gaps — confirm scope before approving.");
            }

            if (IsStalePendingApproval(ticket, utcNow))
            {
                return (
                    CortexRiskRecommendation.KeepOnCurrentPath,
                    "Approval has been pending longer than usual — follow up with reviewers when appropriate.");
            }

            return (
                CortexRiskRecommendation.KeepOnCurrentPath,
                "Proceed through normal review; optional refinements can follow approval.");
        }

        var hasMissingDetails = HasMissingDetails(ticket);
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
