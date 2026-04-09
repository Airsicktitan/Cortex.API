using Cortex.API.Data;

namespace Cortex.API.Services;

public class TicketArchivalService(
    ITicketRepository ticketRepository,
    IArchiveConfigurationService archiveConfigurationService) : ITicketArchivalService
{
    private readonly ITicketRepository _ticketRepository = ticketRepository;
    private readonly IArchiveConfigurationService _archiveConfigurationService = archiveConfigurationService;

    public async Task<int> ArchiveEligibleTicketsAsync(int archivedBy)
    {
        var configurations = await _archiveConfigurationService.GetAllAsync();
        if (configurations.Count == 0)
        {
            return 0;
        }

        var candidateTicketIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var utcNow = DateTime.UtcNow;

        foreach (var configuration in configurations)
        {
            var eligibleStatuses = _archiveConfigurationService.GetEligibleStatuses(configuration);
            if (eligibleStatuses.Count == 0)
            {
                continue;
            }

            var archiveCutoffUtc = _archiveConfigurationService.GetArchiveCutoffUtc(configuration, utcNow);
            var tickets = await _ticketRepository.GetArchiveCandidatesAsync(eligibleStatuses, archiveCutoffUtc);

            foreach (var ticket in tickets)
            {
                candidateTicketIds.Add(ticket.Id);
            }
        }

        var archivedCount = 0;
        foreach (var ticketId in candidateTicketIds)
        {
            var archived = await _ticketRepository.ArchiveTicketAsync(ticketId, archivedBy);
            if (archived)
            {
                archivedCount += 1;
            }
        }

        return archivedCount;
    }
}
