using Cortex.API.Database;
using Cortex.API.DTO;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Services;

public sealed class DecisionImpactService(
    CortexDbContext dbContext,
    IOperationalRiskService operationalRiskService,
    IOwnerWorkloadScoringService ownerWorkloadScoringService) : IDecisionImpactService
{
    private readonly CortexDbContext _dbContext = dbContext;
    private readonly IOperationalRiskService _operationalRiskService = operationalRiskService;
    private readonly IOwnerWorkloadScoringService _ownerWorkloadScoringService = ownerWorkloadScoringService;

    public async Task<DecisionImpactResponse?> EvaluateAsync(
        Ticket ticket,
        CancellationToken cancellationToken = default)
    {
        var batch = await EvaluateBatchAsync([ticket], cancellationToken);
        return batch.TryGetValue(ticket.Id, out var impact)
            ? impact
            : null;
    }

    public async Task<IReadOnlyDictionary<string, DecisionImpactResponse>> EvaluateBatchAsync(
        IEnumerable<Ticket> tickets,
        CancellationToken cancellationToken = default)
    {
        var ticketList = tickets.ToList();
        if (ticketList.Count == 0)
        {
            return new Dictionary<string, DecisionImpactResponse>(StringComparer.Ordinal);
        }

        var ticketIds = ticketList
            .Select(ticket => ticket.Id)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var snapshots = await _dbContext.TicketRoutingOverrides
            .AsNoTracking()
            .Where(@override => ticketIds.Contains(@override.TicketId))
            .Where(@override => @override.DecisionImpactAppliedAtUtc.HasValue)
            .OrderByDescending(@override => @override.CreatedDateUtc)
            .ThenByDescending(@override => @override.Id)
            .ToListAsync(cancellationToken);
        var snapshotByTicketId = snapshots
            .GroupBy(snapshot => snapshot.TicketId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        if (snapshotByTicketId.Count == 0)
        {
            return new Dictionary<string, DecisionImpactResponse>(StringComparer.Ordinal);
        }

        var riskByTicketId = await _operationalRiskService.EvaluateBatchAsync(
            ticketList,
            cancellationToken);
        var currentOwnerKeys = ticketList
            .Select(ticket => ResolveCurrentOwner(ticket, snapshotByTicketId))
            .Where(ownerKey => !string.IsNullOrWhiteSpace(ownerKey))
            .Select(ownerKey => ownerKey!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var currentScores = await _ownerWorkloadScoringService.GetScoresAsync(
            currentOwnerKeys,
            excludeTicketId: null,
            respectCurrentVisibility: true,
            cancellationToken);
        var currentScoreByOwner = currentScores.ToDictionary(
            score => score.OwnerKey,
            score => score.WorkloadScore,
            StringComparer.Ordinal);

        var output = new Dictionary<string, DecisionImpactResponse>(StringComparer.Ordinal);
        foreach (var ticket in ticketList)
        {
            if (!snapshotByTicketId.TryGetValue(ticket.Id, out var snapshot)
                || !riskByTicketId.TryGetValue(ticket.Id, out var currentRisk))
            {
                continue;
            }

            var currentOwner = ResolveCurrentOwner(ticket, snapshot);
            var currentOwnerWorkload = currentOwner is not null
                && currentScoreByOwner.TryGetValue(currentOwner, out var score)
                    ? score
                    : 0;
            var currentPressure = ToPressureLevel(currentOwnerWorkload);
            var previousRisk = NormalizeLevel(snapshot.DecisionImpactPreviousRiskLevel, "low");
            var currentRiskLevel = NormalizeLevel(currentRisk.RiskLevel, "low");
            var previousPressure = NormalizeLevel(snapshot.DecisionImpactPreviousPressureLevel, "low");
            var previousWorkload = snapshot.DecisionImpactPreviousOwnerWorkload ?? 0;
            var riskImproved = RiskRank(previousRisk) > RiskRank(currentRiskLevel);
            var workloadImproved = previousWorkload > currentOwnerWorkload;
            var pressureImproved = PressureRank(previousPressure) > PressureRank(currentPressure);

            output[ticket.Id] = new DecisionImpactResponse
            {
                HasImpact = true,
                PreviousRiskLevel = previousRisk,
                CurrentRiskLevel = currentRiskLevel,
                RiskImproved = riskImproved,
                PreviousOwnerWorkload = previousWorkload,
                CurrentOwnerWorkload = currentOwnerWorkload,
                WorkloadImproved = workloadImproved,
                PreviousPressureLevel = previousPressure,
                CurrentPressureLevel = currentPressure,
                PressureImproved = pressureImproved,
                Summary = BuildSummary(
                    previousRisk,
                    currentRiskLevel,
                    riskImproved,
                    previousPressure,
                    currentPressure,
                    pressureImproved,
                    workloadImproved),
                AppliedAtUtc = snapshot.DecisionImpactAppliedAtUtc!.Value,
                Source = string.IsNullOrWhiteSpace(snapshot.DecisionImpactSource)
                    ? "cortex_recommendation_review"
                    : snapshot.DecisionImpactSource.Trim(),
            };
        }

        return output;
    }

    private static string? ResolveCurrentOwner(
        Ticket ticket,
        IReadOnlyDictionary<string, TicketRoutingOverride> snapshotByTicketId)
    {
        return snapshotByTicketId.TryGetValue(ticket.Id, out var snapshot)
            ? ResolveCurrentOwner(ticket, snapshot)
            : null;
    }

    private static string? ResolveCurrentOwner(Ticket ticket, TicketRoutingOverride snapshot)
    {
        return snapshot.DecisionImpactAssignmentField switch
        {
            "synitiOwner" => NormalizeOptional(ticket.SynitiOwner),
            "businessOwner" => NormalizeOptional(ticket.BusinessOwner),
            _ => NormalizeOptional(ticket.SynitiOwner) ?? NormalizeOptional(ticket.BusinessOwner),
        };
    }

    private static string BuildSummary(
        string previousRisk,
        string currentRisk,
        bool riskImproved,
        string previousPressure,
        string currentPressure,
        bool pressureImproved,
        bool workloadImproved)
    {
        if (riskImproved)
        {
            return $"Risk reduced from {FormatLevel(previousRisk)} to {FormatLevel(currentRisk)}";
        }

        if (pressureImproved)
        {
            return $"Owner pressure improved from {FormatLevel(previousPressure)} to {FormatLevel(currentPressure)}";
        }

        if (workloadImproved)
        {
            return "Reassigned to lower workload owner";
        }

        return "No significant improvement detected";
    }

    private static string ToPressureLevel(int workloadScore)
    {
        if (workloadScore >= 31)
        {
            return "critical";
        }
        if (workloadScore >= 21)
        {
            return "high";
        }
        if (workloadScore >= 11)
        {
            return "moderate";
        }
        return "low";
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeLevel(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToLowerInvariant();

    private static int RiskRank(string? riskLevel) =>
        riskLevel?.ToLowerInvariant() switch
        {
            "critical" => 3,
            "high" => 2,
            "moderate" => 1,
            _ => 0,
        };

    private static int PressureRank(string? pressureLevel) =>
        pressureLevel?.ToLowerInvariant() switch
        {
            "critical" => 3,
            "high" => 2,
            "moderate" => 1,
            _ => 0,
        };

    private static string FormatLevel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Low";
        }

        var normalized = value.Trim().ToLowerInvariant();
        return char.ToUpperInvariant(normalized[0]) + normalized[1..];
    }
}
