using Cortex.API.Database;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Services;

public sealed class WorkloadSnapshotService(
    CortexDbContext dbContext,
    ISlaConfigurationService slaConfigurationService) : IWorkloadSnapshotService
{
    public async Task<IReadOnlyList<WorkloadSnapshot>> GetSnapshotsAsync(CancellationToken cancellationToken = default)
    {
        var resolvedStatuses = TicketStatusFilters.ResolvedStatusesUpper;
        var users = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.IsActive && user.IsSynitiOwnerEligible)
            .ToListAsync(cancellationToken);
        var ownerAliases = OwnerFieldResolution.BuildAliasLookup(users);
        var priorityMap = await slaConfigurationService.GetPriorityMapAsync();
        var nowUtc = DateTime.UtcNow;
        var tickets = await dbContext.Tickets
            .AsNoTracking()
            .Where(ticket => ticket.ApprovalStatus == ApprovalStatus.Approved)
            .Where(ticket => ticket.Status == null || !resolvedStatuses.Contains(ticket.Status.ToUpper()))
            .Where(ticket => !dbContext.ArchivedTickets.Any(archived => archived.Id == ticket.Id))
            .ToListAsync(cancellationToken);

        var ticketGroups = tickets
            .Select(ticket => new
            {
                Ticket = ticket,
                OwnerKey = CanonicalizeOwnerKey(ticket.SynitiOwner, ownerAliases)
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.OwnerKey))
            .GroupBy(item => item.OwnerKey!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Ticket).ToList(), StringComparer.OrdinalIgnoreCase);

        var snapshots = new List<WorkloadSnapshot>(users.Count);
        foreach (var user in users)
        {
            var ownerKey = OwnerFieldResolution.ToCanonicalOwnerKey(user);

            var ownerTickets = ticketGroups.TryGetValue(ownerKey, out var matches)
                ? matches
                : [];

            var highPriorityCount = 0;
            var overdueTicketCount = 0;
            var slaRiskCount = 0;
            var staleTicketCount = 0;

            foreach (var ticket in ownerTickets)
            {
                var signals = WorkloadScoringPolicy.EvaluateTicket(ticket, priorityMap, nowUtc);
                if (signals.IsHighPriority)
                {
                    highPriorityCount++;
                }
                if (signals.IsOverdue)
                {
                    overdueTicketCount++;
                }
                if (signals.IsSlaRisk)
                {
                    slaRiskCount++;
                }
                if (signals.IsStale)
                {
                    staleTicketCount++;
                }
            }

            var workloadScore = WorkloadScoringPolicy.CalculateScore(
                ownerTickets.Count,
                highPriorityCount,
                overdueTicketCount,
                slaRiskCount,
                staleTicketCount);

            snapshots.Add(new WorkloadSnapshot
            {
                UserId = ownerKey,
                DisplayName = string.IsNullOrWhiteSpace(user.DisplayName)
                    ? user.Email ?? string.Empty
                    : user.DisplayName.Trim(),
                ActiveTicketCount = ownerTickets.Count,
                HighPriorityCount = highPriorityCount,
                OverdueTicketCount = overdueTicketCount,
                SlaRiskCount = slaRiskCount,
                StaleTicketCount = staleTicketCount,
                WorkloadScore = workloadScore,
                Status = WorkloadScoringPolicy.ToSnapshotStatus(workloadScore)
            });
        }

        return snapshots
            .OrderBy(snapshot => snapshot.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<WorkloadSnapshot?> GetSnapshotAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var normalized = userId.Trim();
        var snapshots = await GetSnapshotsAsync(cancellationToken);
        return snapshots.FirstOrDefault(snapshot =>
            snapshot.UserId.Equals(normalized, StringComparison.OrdinalIgnoreCase)
            || snapshot.DisplayName.Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static string? CanonicalizeOwnerKey(
        string? ownerKey,
        IReadOnlyDictionary<string, User> ownerAliases)
    {
        return OwnerFieldResolution.CanonicalizeOwnerField(ownerKey, ownerAliases);
    }
}
