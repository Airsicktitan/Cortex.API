using Cortex.API.DTO;
using Cortex.API.Models;

namespace Cortex.API.Services;

/// <summary>
/// Tier 8 Safe Autonomy Layer. Decides whether a routing recommendation
/// is safe to auto-apply, and (only when explicitly enabled in configuration)
/// applies the assignment. Always shadow-only by default.
/// </summary>
public interface ICortexAutonomyService
{
    /// <summary>
    /// Evaluate the latest routing decision for the ticket. Persists an audit row
    /// regardless of outcome; mutates the ticket only when explicit auto-apply
    /// configuration is enabled and every eligibility check passes.
    /// </summary>
    Task<CortexAutonomyResultDto> EvaluateAndMaybeApplyDecisionAsync(
        Ticket ticket,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the most recent persisted autonomy evaluation for a ticket.</summary>
    Task<CortexAutonomyResultDto?> GetLatestAsync(
        string ticketId,
        CancellationToken cancellationToken = default);
}
