using Cortex.API.DTO;
using Cortex.API.Models;

namespace Cortex.API.Services;

public interface ISapTicketReferenceDetectionService
{
    /// <summary>
    /// Detects SAP table/field references in ticket text using enabled SAP reference sources only.
    /// </summary>
    Task<SapTicketReferenceContextDto> DetectSapReferencesForTicketAsync(
        Ticket ticket,
        CancellationToken cancellationToken = default);
}
