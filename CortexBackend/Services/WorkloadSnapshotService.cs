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

            var highPriorityCount = ownerTickets.Count(ticket =>
                ticket.Priority.Equals("High", StringComparison.OrdinalIgnoreCase)
                || ticket.Priority.Equals("Critical", StringComparison.OrdinalIgnoreCase));

            var slaRiskCount = ownerTickets.Count(ticket => IsSlaRiskTicket(ticket, priorityMap));
            var workloadScore = ownerTickets.Count + (highPriorityCount * 2) + (slaRiskCount * 3);

            snapshots.Add(new WorkloadSnapshot
            {
                UserId = ownerKey,
                DisplayName = string.IsNullOrWhiteSpace(user.DisplayName)
                    ? user.Email ?? string.Empty
                    : user.DisplayName.Trim(),
                ActiveTicketCount = ownerTickets.Count,
                HighPriorityCount = highPriorityCount,
                SlaRiskCount = slaRiskCount,
                WorkloadScore = workloadScore,
                Status = ResolveWorkloadStatus(workloadScore)
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

    private static bool IsSlaRiskTicket(
        Ticket ticket,
        IReadOnlyDictionary<string, SlaConfiguration> priorityMap)
    {
        priorityMap.TryGetValue(ticket.Priority ?? string.Empty, out var configuration);
        var snapshot = TicketSlaCalculator.Calculate(ticket, configuration);
        if (snapshot.IsBreached)
        {
            return true;
        }

        return snapshot.TargetDateUtc <= DateTime.UtcNow.AddHours(24);
    }

    private static string ResolveWorkloadStatus(int workloadScore)
    {
        if (workloadScore <= 5)
        {
            return "Available";
        }

        if (workloadScore <= 10)
        {
            return "Balanced";
        }

        return "Overloaded";
    }

    private static string? CanonicalizeOwnerKey(
        string? ownerKey,
        IReadOnlyDictionary<string, User> ownerAliases)
    {
        return OwnerFieldResolution.CanonicalizeOwnerField(ownerKey, ownerAliases);
    }
}
