using Cortex.API.DTO;
using Cortex.API.Models;

namespace Cortex.API.Services;

public sealed class OperationalRiskService(
    IOwnerWorkloadScoringService ownerWorkloadScoringService,
    ISlaConfigurationService slaConfigurationService) : IOperationalRiskService
{
    private readonly IOwnerWorkloadScoringService _ownerWorkloadScoringService = ownerWorkloadScoringService;
    private readonly ISlaConfigurationService _slaConfigurationService = slaConfigurationService;

    public async Task<OperationalRiskResponse> EvaluateAsync(
        Ticket ticket,
        CancellationToken cancellationToken = default)
    {
        var single = await EvaluateBatchAsync([ticket], cancellationToken);
        return single.TryGetValue(ticket.Id, out var assessment)
            ? assessment
            : BuildFallback();
    }

    public async Task<IReadOnlyDictionary<string, OperationalRiskResponse>> EvaluateBatchAsync(
        IEnumerable<Ticket> tickets,
        CancellationToken cancellationToken = default)
    {
        var list = tickets.ToList();
        if (list.Count == 0)
        {
            return new Dictionary<string, OperationalRiskResponse>(StringComparer.Ordinal);
        }

        var slaPriorityMap = await _slaConfigurationService.GetPriorityMapAsync();
        var ownerKeys = list
            .SelectMany(CollectAssignedOwners)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var ownerScores = await _ownerWorkloadScoringService.GetScoresAsync(
            ownerKeys,
            excludeTicketId: null,
            respectCurrentVisibility: true,
            cancellationToken);
        var ownerScoreLookup = ownerScores.ToDictionary(
            score => score.OwnerKey,
            score => score,
            StringComparer.Ordinal);

        var output = new Dictionary<string, OperationalRiskResponse>(StringComparer.Ordinal);
        foreach (var ticket in list)
        {
            output[ticket.Id] = EvaluateInternal(ticket, slaPriorityMap, ownerScoreLookup);
        }

        return output;
    }

    private static OperationalRiskResponse EvaluateInternal(
        Ticket ticket,
        IReadOnlyDictionary<string, SlaConfiguration> slaPriorityMap,
        IReadOnlyDictionary<string, OwnerWorkloadScoreSnapshot> ownerScoreLookup)
    {
        if (TicketSlaCalculator.IsResolvedStatus(ticket.Status))
        {
            return BuildFallback();
        }

        var assignedOwners = CollectAssignedOwners(ticket).ToList();
        var uniqueOwners = assignedOwners
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var combinedWorkloadScore = uniqueOwners.Sum(ownerKey =>
            ownerScoreLookup.TryGetValue(ownerKey, out var score) ? score.WorkloadScore : 0);
        var pressureLevel = ToPressureLevel(combinedWorkloadScore);
        var ownerOverloaded = pressureLevel is "high" or "critical";

        slaPriorityMap.TryGetValue(ticket.Priority ?? string.Empty, out var configuration);
        var slaSnapshot = TicketSlaCalculator.Calculate(ticket, configuration);
        var slaBreached = slaSnapshot.Status == "Breached" || slaSnapshot.IsBreached;
        var slaAtRisk = slaSnapshot.Status == "At Risk";
        var highPriority = IsHighOrCriticalPriority(ticket.Priority);

        var missingSynitiOwner = string.IsNullOrWhiteSpace(ticket.SynitiOwner);
        var missingBusinessOwner = string.IsNullOrWhiteSpace(ticket.BusinessOwner);
        var ownershipIncomplete = missingSynitiOwner || missingBusinessOwner;

        var score = 0;
        var reasons = new List<string>();

        if (slaBreached)
        {
            score += 5;
            reasons.Add("SLA is breached.");
        }
        else if (slaAtRisk)
        {
            score += 3;
            reasons.Add("SLA is at risk.");
        }

        if (highPriority)
        {
            score += 2;
            reasons.Add(string.Equals(ticket.Priority, "Critical", StringComparison.OrdinalIgnoreCase)
                ? "Priority is critical."
                : "Priority is high.");
        }

        if (ownerOverloaded)
        {
            score += 2;
            reasons.Add("Assigned owner workload pressure is high.");
        }

        if (ownershipIncomplete)
        {
            score += 4;
            if (missingSynitiOwner && missingBusinessOwner)
            {
                reasons.Add("Both Syniti and Business owners are missing.");
            }
            else if (missingSynitiOwner)
            {
                reasons.Add("Syniti owner is missing.");
            }
            else
            {
                reasons.Add("Business owner is missing.");
            }
        }

        var riskLevel = ToRiskLevel(score);
        var recommendedAction = RecommendAction(
            missingSynitiOwner,
            missingBusinessOwner,
            slaBreached,
            slaAtRisk,
            ownerOverloaded,
            highPriority,
            riskLevel);

        return new OperationalRiskResponse
        {
            OperationalRiskScore = score,
            RiskLevel = riskLevel,
            Reasons = reasons,
            RecommendedAction = recommendedAction,
            OwnerPressure = new OwnerPressureResponse
            {
                WorkloadScore = combinedWorkloadScore,
                PressureLevel = pressureLevel,
            },
            IsAssignmentSafe = riskLevel is "low" && !ownerOverloaded && !ownershipIncomplete,
            IsOwnerOverloaded = ownerOverloaded,
            IsOwnershipComplete = !ownershipIncomplete,
        };
    }

    private static IEnumerable<string> CollectAssignedOwners(Ticket ticket)
    {
        if (!string.IsNullOrWhiteSpace(ticket.SynitiOwner))
        {
            yield return ticket.SynitiOwner.Trim();
        }
        if (!string.IsNullOrWhiteSpace(ticket.BusinessOwner))
        {
            yield return ticket.BusinessOwner.Trim();
        }
    }

    private static bool IsHighOrCriticalPriority(string? priority)
    {
        return priority is not null
            && (priority.Equals("High", StringComparison.OrdinalIgnoreCase)
                || priority.Equals("Critical", StringComparison.OrdinalIgnoreCase));
    }

    private static string ToRiskLevel(int score)
    {
        if (score >= 9)
        {
            return "critical";
        }
        if (score >= 6)
        {
            return "high";
        }
        if (score >= 3)
        {
            return "moderate";
        }
        return "low";
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

    private static string RecommendAction(
        bool missingSynitiOwner,
        bool missingBusinessOwner,
        bool slaBreached,
        bool slaAtRisk,
        bool ownerOverloaded,
        bool highPriority,
        string riskLevel)
    {
        if (missingSynitiOwner && missingBusinessOwner)
        {
            return "Add missing Syniti and Business owners.";
        }
        if (missingBusinessOwner)
        {
            return "Add missing Business Owner.";
        }
        if (missingSynitiOwner)
        {
            return "Add missing Syniti Owner.";
        }
        if (slaBreached)
        {
            return "Escalate due to SLA risk.";
        }
        if (slaAtRisk && ownerOverloaded)
        {
            return "Review assignment or escalate within 1 hour.";
        }
        if (ownerOverloaded)
        {
            return "Reassign to lower workload owner.";
        }
        if (slaAtRisk)
        {
            return "Review assignment due to SLA risk.";
        }
        if (highPriority && riskLevel is "moderate" or "high" or "critical")
        {
            return "Review immediately.";
        }
        return "No immediate intervention required.";
    }

    private static OperationalRiskResponse BuildFallback()
    {
        return new OperationalRiskResponse
        {
            OperationalRiskScore = 0,
            RiskLevel = "low",
            Reasons = [],
            RecommendedAction = "No immediate intervention required.",
            OwnerPressure = new OwnerPressureResponse
            {
                WorkloadScore = 0,
                PressureLevel = "low",
            },
            IsAssignmentSafe = true,
            IsOwnerOverloaded = false,
            IsOwnershipComplete = true,
        };
    }
}
