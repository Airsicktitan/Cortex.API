using Cortex.API.Models;

namespace Cortex.API.Services;

public readonly record struct TicketWorkloadSignals(
    bool IsHighPriority,
    bool IsOverdue,
    bool IsSlaRisk,
    bool IsStale);

public static class WorkloadScoringPolicy
{
    public const decimal OpenTicketWeight = 1.0m;
    public const decimal HighPriorityTicketWeight = 2.0m;
    public const decimal OverdueTicketWeight = 3.0m;
    public const decimal SlaRiskTicketWeight = 2.5m;
    public const decimal StaleTicketWeight = 1.5m;
    public const int StaleTicketAgeHours = 48;

    public static decimal CalculateScore(
        int openTickets,
        int highPriorityTickets,
        int overdueTickets,
        int slaRiskTickets,
        int staleTickets)
    {
        var rawScore = (openTickets * OpenTicketWeight)
            + (highPriorityTickets * HighPriorityTicketWeight)
            + (overdueTickets * OverdueTicketWeight)
            + (slaRiskTickets * SlaRiskTicketWeight)
            + (staleTickets * StaleTicketWeight);
        return NormalizeScore(rawScore);
    }

    public static decimal NormalizeScore(decimal workloadScore) =>
        workloadScore < 0m ? 0m : workloadScore;

    public static TicketWorkloadSignals EvaluateTicket(
        Ticket ticket,
        IReadOnlyDictionary<string, SlaConfiguration> priorityMap,
        DateTime nowUtc)
    {
        priorityMap.TryGetValue(ticket.Priority ?? string.Empty, out var configuration);
        var slaSnapshot = TicketSlaCalculator.Calculate(ticket, configuration);
        var isOverdue = slaSnapshot.Status == "Breached" || slaSnapshot.IsBreached;

        return new TicketWorkloadSignals(
            IsHighPriority: IsHighPriority(ticket.Priority),
            IsOverdue: isOverdue,
            IsSlaRisk: !isOverdue && slaSnapshot.Status == "At Risk",
            IsStale: IsStale(ticket, nowUtc));
    }

    public static string ToPressureLevel(decimal workloadScore)
    {
        workloadScore = NormalizeScore(workloadScore);
        if (workloadScore >= 31m)
        {
            return "critical";
        }
        if (workloadScore >= 21m)
        {
            return "high";
        }
        if (workloadScore >= 11m)
        {
            return "moderate";
        }
        return "low";
    }

    public static bool IsOverloaded(decimal workloadScore)
    {
        var pressureLevel = ToPressureLevel(workloadScore);
        return pressureLevel is "high" or "critical";
    }

    public static string ToSnapshotStatus(decimal workloadScore)
    {
        if (IsOverloaded(workloadScore))
        {
            return "Overloaded";
        }

        return ToPressureLevel(workloadScore) == "moderate"
            ? "Balanced"
            : "Available";
    }

    public static bool IsHighPriority(string? priority)
    {
        return priority is not null
            && (priority.Equals("High", StringComparison.OrdinalIgnoreCase)
                || priority.Equals("Critical", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsStale(Ticket ticket, DateTime nowUtc)
    {
        var lastActivityUtc = ticket.LastModifiedDate ?? ticket.CreatedDate;
        return lastActivityUtc <= nowUtc.AddHours(-StaleTicketAgeHours);
    }
}
