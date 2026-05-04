using Cortex.API.DTO;

namespace Cortex.API.Services;

/// <summary>
/// Creates Cortex tickets using the same workflow as <c>POST /api/tickets</c>.
/// </summary>
public interface ITicketCreationApplicationService
{
    Task<TicketResponse> CreateTicketAsync(
        CreateTicketRequest request,
        CancellationToken cancellationToken = default);
}
