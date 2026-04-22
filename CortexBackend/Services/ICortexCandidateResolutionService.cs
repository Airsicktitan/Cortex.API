using Cortex.API.Models;

namespace Cortex.API.Services;

public interface ICortexCandidateResolutionService
{
    Task<IReadOnlyList<CortexDecisionCandidate>> GetEligibleCandidatesAsync(
        Ticket ticket,
        CancellationToken cancellationToken = default);
}
