using Cortex.API.DTO;
using Cortex.API.Models;

namespace Cortex.API.Services;

/// <summary>Resolves SAP reference context for reviewer display from ticket content and linked integrations.</summary>
public interface ISapReferenceContextService
{
    Task<SapTicketReferenceContextDto> DetectSapReferencesForTicketAsync(
        Ticket ticket,
        CancellationToken cancellationToken = default);
}
