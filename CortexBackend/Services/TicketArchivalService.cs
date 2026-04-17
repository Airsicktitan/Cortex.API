using Cortex.API.Data;
using Cortex.API.DTO;

namespace Cortex.API.Services;

public class TicketArchivalService(
    ITicketRepository ticketRepository,
    IArchiveConfigurationService archiveConfigurationService,
    IUserRepository userRepository,
    ITicketAuditService ticketAuditService,
    INotificationService notificationService,
    IRealtimeEventService realtimeEventService,
    IRealtimeAudienceResolver realtimeAudienceResolver,
    IResponseMappingContextFactory mappingContextFactory) : ITicketArchivalService
{
    private readonly ITicketRepository _ticketRepository = ticketRepository;
    private readonly IArchiveConfigurationService _archiveConfigurationService = archiveConfigurationService;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly ITicketAuditService _ticketAuditService = ticketAuditService;
    private readonly INotificationService _notificationService = notificationService;
    private readonly IRealtimeEventService _realtimeEventService = realtimeEventService;
    private readonly IRealtimeAudienceResolver _realtimeAudienceResolver = realtimeAudienceResolver;
    private readonly IResponseMappingContextFactory _mappingContextFactory = mappingContextFactory;

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
        var archivedByUser = await _userRepository.GetByIdAsync(archivedBy);
        foreach (var ticketId in candidateTicketIds)
        {
            var ticket = await _ticketRepository.GetTicketByIdAsync(ticketId);
            if (ticket is null)
            {
                continue;
            }

            var archived = await _ticketRepository.ArchiveTicketAsync(ticketId, archivedBy);
            if (archived)
            {
                archivedCount += 1;

                if (archivedByUser is not null)
                {
                    await _ticketAuditService.RecordTicketArchivedAsync(
                        ticket,
                        archivedByUser,
                        "Archived automatically by archive policy.");
                    await _notificationService.CreateArchiveNotificationsAsync(
                        ticket,
                        archivedByUser,
                        ticketIsArchived: true);
                }

                var archivedTicket = await _ticketRepository.GetArchivedTicketByIdAsync(ticketId);
                if (archivedTicket is null)
                {
                    continue;
                }

                var mappingContext = await _mappingContextFactory.CreateAsync(
                    [archivedTicket.CreatedBy, archivedTicket.ArchivedBy],
                    null,
                    [archivedTicket.BoardId]);
                var archivedTicketResponse = archivedTicket.ToResponse(mappingContext);
                var audienceUserIds = await _realtimeAudienceResolver.GetAudienceUserIdsAsync(archivedTicket);

                await _realtimeEventService.PublishAsync(new RealtimeEventMessage
                {
                    EventType = "ticket.archived",
                    TicketId = ticket.Id,
                    EntityId = ticket.Id,
                    AudienceUserIds = audienceUserIds,
                    ArchivedTicket = archivedTicketResponse
                });
            }
        }

        return archivedCount;
    }
}
