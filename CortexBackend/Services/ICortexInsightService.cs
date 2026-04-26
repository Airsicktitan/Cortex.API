using Cortex.API.DTO;
using Cortex.API.Models;

namespace Cortex.API.Services;

public interface ICortexInsightService
{
    Task<CortexInsightDto> GetInsightAsync(
        Ticket currentTicket,
        TicketVisibilityContext visibilityContext,
        CancellationToken cancellationToken = default);

    Task<CortexInsightDto> GenerateInsightAsync(
        Ticket currentTicket,
        IReadOnlyList<CortexInsightSimilarTicketDto> similarTickets,
        CancellationToken cancellationToken = default);
}
