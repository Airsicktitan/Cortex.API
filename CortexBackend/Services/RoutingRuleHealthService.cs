using Cortex.API.Database;
using Cortex.API.DTO;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Services;

/// <summary>
/// Tier 11 read-only aggregates. Reuses <see cref="ICortexLearningService.GetRoutingRuleEffectivenessAsync"/> for parity
/// with Cortex learning signals, then supplements with explicit outcome counts aligned to those tickets.
///
/// LIMITATION (documented): Outcomes keyed only to tickets surfaced through routing decisions for this rule; if
/// TicketOutcome.MatchedRuleId diverges mid-lifecycle the ticket set still aligns with Tier 6 learning aggregates.
/// </summary>
public sealed class RoutingRuleHealthService(
    CortexDbContext db,
    ICortexLearningService cortexLearningService,
    ITicketRoutingRuleService routingRuleService) : IRoutingRuleHealthService
{
    public const string HealthHealthy = "Healthy";
    public const string HealthWatch = "Watch";
    public const string HealthNeedsReview = "NeedsReview";
    public const string HealthInsufficientData = "InsufficientData";

    public async Task<RoutingRuleHealthOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var ruleEntities = await routingRuleService.GetAllAsync();
        var boardLookup = await db.TicketBoardDefinitions
            .AsNoTracking()
            .ToDictionaryAsync(b => b.Id.ToString(), b => b.Name, StringComparer.Ordinal, cancellationToken);

        var rows = new List<RoutingRuleHealthRowDto>();

        foreach (var rule in ruleEntities.OrderBy(r => r.RulePriority).ThenBy(r => r.Id))
        {
            var eff = await cortexLearningService.GetRoutingRuleEffectivenessAsync(rule.Id, cancellationToken);

            var ticketIds = await db.TicketRoutingDecisions.AsNoTracking()
                .Where(d => d.MatchedRuleId == rule.Id)
                .Select(d => d.TicketId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var returnedForDetailCount = 0;

            DateTime? lastMatched = null;

            if (ticketIds.Count > 0)
            {
                lastMatched = await db.TicketRoutingDecisions.AsNoTracking()
                    .Where(d => d.MatchedRuleId == rule.Id)
                    .MaxAsync(d => (DateTime?)d.CreatedDateUtc, cancellationToken);

                returnedForDetailCount = await db.TicketOutcomes.AsNoTracking()
                    .Where(o => ticketIds.Contains(o.TicketId) && o.WasReturnedForDetail)
                    .CountAsync(cancellationToken);
            }

            var matchCount = eff.TotalDecisions;
            var terminalCount = eff.OutcomeSampleCount;
            var overridePct = matchCount == 0 ? 0 : eff.OverridePercent;
            var slaSuccessPct = eff.SlaSuccessPercent;
            var reassignPct = matchCount == 0 ? 0 : eff.ReassignmentPercent;

            var (health, summary, recommendation) = ClassifyHealth(
                matchCount,
                terminalCount,
                overridePct,
                slaSuccessPct,
                reassignPct,
                returnedForDetailCount);

            rows.Add(new RoutingRuleHealthRowDto
            {
                RuleId = rule.Id,
                RuleName = BuildRuleDisplayName(rule),
                BoardName = ResolveBoardDisplay(rule.BoardId, boardLookup),
                PriorityName = rule.Priority?.Trim() ?? string.Empty,
                IsEnabled = rule.IsEnabled,
                MatchCount = matchCount,
                SampleSize = terminalCount,
                OverrideCount = eff.OverrideCount,
                OverridePercent = Math.Round(overridePct, 1),
                SlaBreachedCount = eff.SlaBreachedCount,
                SlaSuccessPercent = Math.Round(slaSuccessPct, 1),
                ReturnedForDetailCount = returnedForDetailCount,
                ReassignedCount = eff.ReassignmentCount,
                LastMatchedAtUtc = lastMatched,
                HealthStatus = health,
                HealthSummary = summary,
                RecommendedAction = recommendation,
            });
        }

        return new RoutingRuleHealthOverviewDto { Rules = rows };
    }

    private static (string Status, string Summary, string Action) ClassifyHealth(
        int matchCount,
        int terminalCount,
        double overridePct,
        double slaSuccessPct,
        double reassignmentPct,
        int returnedForDetailCount)
    {
        if (matchCount < 3 || terminalCount < 3)
        {
            return (
                HealthInsufficientData,
                "Insufficient history yet — Cortex needs more routing matches and terminal outcomes.",
                "Gather enough volume before tightening or retiring this rule.");
        }

        // NeedsReview — highest urgency.
        var needsReviewForOverride = overridePct >= 40;
        var needsReviewForSla = terminalCount >= 5 && slaSuccessPct < 60;
        var needsReviewForReassign = matchCount >= 5 && reassignmentPct >= 35;

        if (needsReviewForOverride || needsReviewForSla || needsReviewForReassign)
        {
            var reasons = new List<string>();
            if (needsReviewForOverride)
            {
                reasons.Add($"{Math.Round(overridePct)}% overrides");
            }

            if (needsReviewForSla)
            {
                reasons.Add($"SLA success {Math.Round(slaSuccessPct)}% over {terminalCount} completions");
            }

            if (needsReviewForReassign)
            {
                reasons.Add($"{Math.Round(reassignmentPct)}% reassignment churn");
            }

            return (
                HealthNeedsReview,
                string.Join("; ", reasons),
                "Review rule criteria versus actual handling; consider narrower matches or refreshed owners.");
        }

        // Watch.
        var watchOverride = overridePct >= 20;
        var watchSlaSoft = terminalCount >= 3 && slaSuccessPct < 80;
        var rfThreshold = Math.Max(3, (int)Math.Ceiling(matchCount * 0.25));
        var watchReturn = returnedForDetailCount >= rfThreshold;

        if (watchOverride || watchSlaSoft || watchReturn)
        {
            var notes = new List<string>();
            if (watchOverride)
            {
                notes.Add("elevated overrides");
            }

            if (watchSlaSoft)
            {
                notes.Add("SLA softness");
            }

            if (watchReturn)
            {
                notes.Add("returns for detail trending up");
            }

            return (
                HealthWatch,
                string.Join(", ", notes),
                "Schedule a rule review soon; metrics are early-warning only.");
        }

        return (
            HealthHealthy,
            "Assignments have trended steady with workable override and SLA patterns for this volume.",
            "Keep monitoring periodically — no Cortex action suggested.");
    }

    private static string ResolveBoardDisplay(string? boardKey, IReadOnlyDictionary<string, string> lookup)
    {
        if (string.IsNullOrWhiteSpace(boardKey))
        {
            return "—";
        }

        var k = boardKey.Trim();
        return lookup.TryGetValue(k, out var name) ? name : $"Board #{k}";
    }

    private static string BuildRuleDisplayName(TicketRoutingRule rule)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(rule.TitleContains))
        {
            parts.Add($"Title \"{rule.TitleContains.Trim()}\"");
        }

        if (!string.IsNullOrWhiteSpace(rule.RequesterDepartment))
        {
            parts.Add($"Dept {rule.RequesterDepartment.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(rule.Department))
        {
            parts.Add(rule.Department.Trim());
        }

        if (!string.IsNullOrWhiteSpace(rule.RequesterRole))
        {
            parts.Add($"Role {rule.RequesterRole.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(rule.Priority))
        {
            parts.Add(rule.Priority.Trim());
        }

        return parts.Count > 0
            ? string.Join(" · ", parts)
            : $"Rule #{rule.Id}";
    }
}
