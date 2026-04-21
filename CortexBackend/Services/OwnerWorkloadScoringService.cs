using Cortex.API.Database;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Services;

public sealed class OwnerWorkloadScoringService(
    CortexDbContext dbContext,
    ITicketVisibilityService ticketVisibilityService,
    ISlaConfigurationService slaConfigurationService) : IOwnerWorkloadScoringService
{
    public async Task<IReadOnlyList<OwnerWorkloadScoreSnapshot>> GetScoresAsync(
        IEnumerable<string> ownerKeys,
        string? excludeTicketId = null,
        bool respectCurrentVisibility = true,
        CancellationToken cancellationToken = default)
    {
        var normalizedOwnerKeys = ownerKeys
            .Where(ownerKey => !string.IsNullOrWhiteSpace(ownerKey))
            .Select(ownerKey => ownerKey.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (normalizedOwnerKeys.Count == 0)
        {
            return [];
        }

        var visibility = respectCurrentVisibility
            ? await ticketVisibilityService.GetCurrentVisibilityAsync()
            : null;
        var priorityMap = await slaConfigurationService.GetPriorityMapAsync();
        var normalizedExcludeId = string.IsNullOrWhiteSpace(excludeTicketId)
            ? null
            : excludeTicketId.Trim();

        var tickets = await dbContext.Tickets
            .AsNoTracking()
            .Where(ticket => ticket.ApprovalStatus == ApprovalStatus.Approved)
            .Where(ticket => !dbContext.ArchivedTickets.Any(archived => archived.Id == ticket.Id))
            .Where(ticket =>
                normalizedOwnerKeys.Contains(ticket.SynitiOwner!) ||
                normalizedOwnerKeys.Contains(ticket.BusinessOwner!))
            .ToListAsync(cancellationToken);

        var activeVisibleTickets = tickets
            .Where(ticket => normalizedExcludeId is null || !string.Equals(ticket.Id, normalizedExcludeId, StringComparison.Ordinal))
            .Where(ticket => !TicketSlaCalculator.IsResolvedStatus(ticket.Status))
            .Where(ticket => visibility is null || visibility.CanView(ticket))
            .ToList();

        var scores = new List<OwnerWorkloadScoreSnapshot>(normalizedOwnerKeys.Count);

        foreach (var ownerKey in normalizedOwnerKeys)
        {
            var ownerTickets = activeVisibleTickets
                .Where(ticket => MatchesOwner(ticket.SynitiOwner, ownerKey) || MatchesOwner(ticket.BusinessOwner, ownerKey))
                .ToList();

            var highPriorityTicketCount = ownerTickets.Count(ticket => IsHighPriority(ticket.Priority));
            var atRiskTicketCount = 0;
            var outsideSlaOpenCount = 0;

            foreach (var ticket in ownerTickets)
            {
                priorityMap.TryGetValue(ticket.Priority ?? string.Empty, out var configuration);
                var snapshot = TicketSlaCalculator.Calculate(ticket, configuration);

                if (snapshot.Status == "At Risk")
                {
                    atRiskTicketCount++;
                }
                else if (snapshot.Status == "Breached")
                {
                    outsideSlaOpenCount++;
                }
            }

            var slaRiskTicketCount = atRiskTicketCount + outsideSlaOpenCount;
            var workloadScore = ownerTickets.Count + (highPriorityTicketCount * 2) + (slaRiskTicketCount * 3);

            scores.Add(new OwnerWorkloadScoreSnapshot(
                OwnerKey: ownerKey,
                ActiveTicketCount: ownerTickets.Count,
                HighPriorityTicketCount: highPriorityTicketCount,
                AtRiskTicketCount: atRiskTicketCount,
                OutsideSlaOpenCount: outsideSlaOpenCount,
                SlaRiskTicketCount: slaRiskTicketCount,
                WorkloadScore: workloadScore));
        }

        return scores;
    }

    private static bool MatchesOwner(string? storedOwner, string ownerKey)
    {
        return string.Equals(storedOwner?.Trim(), ownerKey, StringComparison.Ordinal);
    }

    private static bool IsHighPriority(string? priority)
    {
        return priority is not null &&
            (priority.Equals("High", StringComparison.OrdinalIgnoreCase) ||
             priority.Equals("Critical", StringComparison.OrdinalIgnoreCase));
    }
}
