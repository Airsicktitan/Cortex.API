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
        var configuration = await _archiveConfigurationService.GetAsync();
        var eligibleStatuses = _archiveConfigurationService.GetEligibleStatuses(configuration);
        if (eligibleStatuses.Count == 0)
        {
            return 0;
        }

        var archiveCutoffUtc = _archiveConfigurationService.GetArchiveCutoffUtc(configuration, DateTime.UtcNow);
        var tickets = await _ticketRepository.GetArchiveCandidatesAsync(eligibleStatuses, archiveCutoffUtc);

        var archivedCount = 0;
        foreach (var ticket in tickets)
        {
            var archived = await _ticketRepository.ArchiveTicketAsync(ticket.Id, archivedBy);
            if (archived)
            {
                archivedCount += 1;
            }
        }

        return archivedCount;
    }
}
