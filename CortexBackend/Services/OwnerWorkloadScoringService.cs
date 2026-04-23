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
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedOwnerKeys.Count == 0)
        {
            return [];
        }

        var users = await dbContext.Users
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var ownerAliases = OwnerFieldResolution.BuildAliasLookup(users);
        var ownerRequests = normalizedOwnerKeys
            .Select(ownerKey => BuildOwnerWorkloadRequest(ownerKey, ownerAliases))
            .ToList();
        var ownerKeysByMatchKey = BuildOwnerKeysByMatchKey(ownerRequests);

        var visibility = respectCurrentVisibility
            ? await ticketVisibilityService.GetCurrentVisibilityAsync()
            : null;
        var priorityMap = await slaConfigurationService.GetPriorityMapAsync();
        var nowUtc = DateTime.UtcNow;
        var normalizedExcludeId = string.IsNullOrWhiteSpace(excludeTicketId)
            ? null
            : excludeTicketId.Trim();

        var tickets = await dbContext.Tickets
            .AsNoTracking()
            .Where(ticket => ticket.ApprovalStatus == ApprovalStatus.Approved)
            .Where(ticket => !dbContext.ArchivedTickets.Any(archived => archived.Id == ticket.Id))
            .Where(ticket => ticket.SynitiOwner != null || ticket.BusinessOwner != null)
            .ToListAsync(cancellationToken);

        var activeVisibleTickets = tickets
            .Where(ticket => normalizedExcludeId is null || !string.Equals(ticket.Id, normalizedExcludeId, StringComparison.Ordinal))
            .Where(ticket => !TicketSlaCalculator.IsResolvedStatus(ticket.Status))
            .Where(ticket => visibility is null || visibility.CanView(ticket))
            .ToList();

        var metricsByOwnerKey = ownerRequests.ToDictionary(
            ownerRequest => ownerRequest.OwnerKey,
            _ => new WorkloadMetricAccumulator(),
            StringComparer.OrdinalIgnoreCase);
        foreach (var ticket in activeVisibleTickets)
        {
            var matchedOwnerKeys = ResolveMatchedOwnerKeys(ticket, ownerKeysByMatchKey);
            if (matchedOwnerKeys.Count == 0)
            {
                continue;
            }

            var signals = WorkloadScoringPolicy.EvaluateTicket(ticket, priorityMap, nowUtc);
            foreach (var ownerKey in matchedOwnerKeys)
            {
                metricsByOwnerKey[ownerKey].Add(signals);
            }
        }

        var scores = new List<OwnerWorkloadScoreSnapshot>(normalizedOwnerKeys.Count);

        foreach (var ownerRequest in ownerRequests)
        {
            var metrics = metricsByOwnerKey[ownerRequest.OwnerKey];
            scores.Add(new OwnerWorkloadScoreSnapshot(
                OwnerKey: ownerRequest.OwnerKey,
                ActiveTicketCount: metrics.OpenTicketCount,
                HighPriorityTicketCount: metrics.HighPriorityTicketCount,
                AtRiskTicketCount: metrics.SlaRiskTicketCount,
                OutsideSlaOpenCount: metrics.OverdueTicketCount,
                SlaRiskTicketCount: metrics.SlaRiskTicketCount,
                WorkloadScore: WorkloadScoringPolicy.CalculateScore(
                    metrics.OpenTicketCount,
                    metrics.HighPriorityTicketCount,
                    metrics.OverdueTicketCount,
                    metrics.SlaRiskTicketCount,
                    metrics.StaleTicketCount),
                StaleTicketCount: metrics.StaleTicketCount));
        }

        return scores;
    }

    private static OwnerWorkloadRequest BuildOwnerWorkloadRequest(
        string ownerKey,
        IReadOnlyDictionary<string, User> ownerAliases)
    {
        var matchKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ownerKey
        };

        var resolvedUser = OwnerFieldResolution.ResolveUser(ownerKey, ownerAliases);
        if (resolvedUser is not null)
        {
            AddMatchKey(matchKeys, $"{OwnerFieldResolution.UserIdTokenPrefix}{resolvedUser.Id}");
            AddMatchKey(matchKeys, resolvedUser.Email);
            AddMatchKey(matchKeys, resolvedUser.DisplayName);
            AddMatchKey(matchKeys, resolvedUser.NickName);
        }

        return new OwnerWorkloadRequest(ownerKey, matchKeys);
    }

    private static void AddMatchKey(ISet<string> matchKeys, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            matchKeys.Add(value.Trim());
        }
    }

    private static Dictionary<string, List<string>> BuildOwnerKeysByMatchKey(
        IEnumerable<OwnerWorkloadRequest> ownerRequests)
    {
        var lookup = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var ownerRequest in ownerRequests)
        {
            foreach (var matchKey in ownerRequest.MatchKeys)
            {
                if (!lookup.TryGetValue(matchKey, out var ownerKeys))
                {
                    ownerKeys = [];
                    lookup[matchKey] = ownerKeys;
                }

                if (!ownerKeys.Contains(ownerRequest.OwnerKey, StringComparer.OrdinalIgnoreCase))
                {
                    ownerKeys.Add(ownerRequest.OwnerKey);
                }
            }
        }

        return lookup;
    }

    private static HashSet<string> ResolveMatchedOwnerKeys(
        Ticket ticket,
        IReadOnlyDictionary<string, List<string>> ownerKeysByMatchKey)
    {
        var matchedOwnerKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddMatchedOwnerKeys(ticket.SynitiOwner, ownerKeysByMatchKey, matchedOwnerKeys);
        AddMatchedOwnerKeys(ticket.BusinessOwner, ownerKeysByMatchKey, matchedOwnerKeys);
        return matchedOwnerKeys;
    }

    private static void AddMatchedOwnerKeys(
        string? storedOwner,
        IReadOnlyDictionary<string, List<string>> ownerKeysByMatchKey,
        ISet<string> matchedOwnerKeys)
    {
        if (string.IsNullOrWhiteSpace(storedOwner)
            || !ownerKeysByMatchKey.TryGetValue(storedOwner.Trim(), out var ownerKeys))
        {
            return;
        }

        foreach (var ownerKey in ownerKeys)
        {
            matchedOwnerKeys.Add(ownerKey);
        }
    }

    private sealed record OwnerWorkloadRequest(
        string OwnerKey,
        IReadOnlySet<string> MatchKeys);

    private sealed class WorkloadMetricAccumulator
    {
        public int OpenTicketCount { get; private set; }
        public int HighPriorityTicketCount { get; private set; }
        public int OverdueTicketCount { get; private set; }
        public int SlaRiskTicketCount { get; private set; }
        public int StaleTicketCount { get; private set; }

        public void Add(TicketWorkloadSignals signals)
        {
            OpenTicketCount++;
            if (signals.IsHighPriority)
            {
                HighPriorityTicketCount++;
            }
            if (signals.IsOverdue)
            {
                OverdueTicketCount++;
            }
            if (signals.IsSlaRisk)
            {
                SlaRiskTicketCount++;
            }
            if (signals.IsStale)
            {
                StaleTicketCount++;
            }
        }
    }
}
