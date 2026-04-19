using Cortex.API.Models;

namespace Cortex.API.Services;

public sealed record TicketSlaSnapshot(
    DateTime TargetDateUtc,
    DateTime? CompletedDateUtc,
    string Status,
    int RemainingMinutes,
    bool IsBreached);

public static class TicketSlaCalculator
{
    public static TicketSlaSnapshot Calculate(
        Ticket ticket,
        SlaConfiguration? configuration = null,
        DateTime? nowUtc = null)
    {
        if (ticket.ApprovalStatus != ApprovalStatus.Approved)
        {
            var createdDateUtc = EnsureUtc(ticket.CreatedDate);
            var label = ticket.ApprovalStatus switch
            {
                ApprovalStatus.PendingApproval => "Pending Approval",
                ApprovalStatus.NeedsMoreInfo => "Needs More Info",
                ApprovalStatus.Rejected => "Rejected",
                _ => "Not Active"
            };
            return new TicketSlaSnapshot(createdDateUtc, null, label, 0, false);
        }

        var createdDateUtcActive = EnsureUtc(ticket.CreatedDate);
        var effectiveConfiguration = configuration ?? GetDefaultPolicy(ticket.Priority);
        var targetDateUtc = createdDateUtcActive.Add(TimeSpan.FromHours(effectiveConfiguration.TargetHours));
        var completedDateUtc = GetCompletedDate(ticket);
        var comparisonDateUtc = completedDateUtc ?? nowUtc ?? DateTime.UtcNow;
        var remainingMinutes = ToWholeMinutes(targetDateUtc - comparisonDateUtc);

        var status = completedDateUtc switch
        {
            not null when completedDateUtc <= targetDateUtc => "Met",
            not null => "Resolved Late",
            _ when comparisonDateUtc > targetDateUtc => "Breached",
            _ when targetDateUtc - comparisonDateUtc <= TimeSpan.FromHours(effectiveConfiguration.WarningHours) => "At Risk",
            _ => "On Track"
        };

        var isBreached = status is "Breached" or "Resolved Late";

        return new TicketSlaSnapshot(
            targetDateUtc,
            completedDateUtc,
            status,
            remainingMinutes,
            isBreached);
    }

    public static bool IsResolvedStatus(string? status)
    {
        return status is not null &&
            (status.Equals("Resolved", StringComparison.OrdinalIgnoreCase) ||
             status.Equals("Closed", StringComparison.OrdinalIgnoreCase));
    }

    public static IReadOnlyList<SlaConfiguration> GetDefaultPolicies()
    {
        return
        [
            new SlaConfiguration { Priority = "Critical", TargetHours = 4, WarningHours = 1 },
            new SlaConfiguration { Priority = "High", TargetHours = 8, WarningHours = 2 },
            new SlaConfiguration { Priority = "Medium", TargetHours = 24, WarningHours = 8 },
            new SlaConfiguration { Priority = "Low", TargetHours = 72, WarningHours = 24 }
        ];
    }

    private static DateTime? GetCompletedDate(Ticket ticket)
    {
        if (!IsResolvedStatus(ticket.Status))
        {
            return null;
        }

        return ticket.LastModifiedDate is { } lastModifiedDate
            ? EnsureUtc(lastModifiedDate)
            : EnsureUtc(ticket.CreatedDate);
    }

    private static SlaConfiguration GetDefaultPolicy(string? priority)
    {
        return GetDefaultPolicies()
            .FirstOrDefault(policy =>
                policy.Priority.Equals(priority?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? GetDefaultPolicies().First(policy => policy.Priority == "Medium");
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static int ToWholeMinutes(TimeSpan value)
    {
        return (int)Math.Round(value.TotalMinutes, MidpointRounding.AwayFromZero);
    }
}
