using Cortex.API.Database;
using Cortex.API.DTO;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Services;

public sealed class OwnerWorkloadPreviewService(
    CortexDbContext dbContext,
    ITicketVisibilityService ticketVisibilityService,
    ISlaConfigurationService slaConfigurationService) : IOwnerWorkloadPreviewService
{
    private const int MaxOwnerKeys = 10;

    public async Task<OwnerWorkloadPreviewResponse> GetSummariesAsync(
        OwnerWorkloadPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var rawKeys = request.OwnerKeys?
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key!.Trim())
            .Distinct(StringComparer.Ordinal)
            .Take(MaxOwnerKeys)
            .ToList() ?? [];

        if (rawKeys.Count == 0)
        {
            return new OwnerWorkloadPreviewResponse();
        }

        var visibility = await ticketVisibilityService.GetCurrentVisibilityAsync();
        var priorityMap = await slaConfigurationService.GetPriorityMapAsync();
        var excludeId = string.IsNullOrWhiteSpace(request.ExcludeTicketId)
            ? null
            : request.ExcludeTicketId.Trim();

        var summaries = new List<OwnerWorkloadSummaryDto>();

        foreach (var ownerKey in rawKeys)
        {
            // Active queue only: archived rows live in ArchivedTickets and must not be counted.
            // Also excludes any inconsistent row still present in Tickets but with a matching ArchivedTickets id.
            var tickets = await dbContext.Tickets
                .AsNoTracking()
                .Where(t =>
                    t.SynitiOwner == ownerKey ||
                    t.BusinessOwner == ownerKey)
                .Where(t => t.ApprovalStatus == ApprovalStatus.Approved)
                .Where(t => !dbContext.ArchivedTickets.Any(a => a.Id == t.Id))
                .ToListAsync(cancellationToken);

            var activeOpen = new List<Ticket>();
            foreach (var ticket in tickets)
            {
                if (excludeId is not null && ticket.Id == excludeId)
                {
                    continue;
                }

                if (TicketSlaCalculator.IsResolvedStatus(ticket.Status))
                {
                    continue;
                }

                if (!visibility.CanView(ticket))
                {
                    continue;
                }

                activeOpen.Add(ticket);
            }

            var atRisk = 0;
            var breachedOpen = 0;

            foreach (var ticket in activeOpen)
            {
                priorityMap.TryGetValue(ticket.Priority ?? string.Empty, out var configuration);
                var snapshot = TicketSlaCalculator.Calculate(ticket, configuration);

                if (snapshot.Status == "At Risk")
                {
                    atRisk++;
                }
                else if (snapshot.Status == "Breached")
                {
                    breachedOpen++;
                }
            }

            summaries.Add(new OwnerWorkloadSummaryDto
            {
                OwnerKey = ownerKey!,
                ActiveTicketCount = activeOpen.Count,
                AtRiskTicketCount = atRisk,
                OutsideSlaOpenCount = breachedOpen
            });
        }

        return new OwnerWorkloadPreviewResponse { Summaries = summaries };
    }
}
